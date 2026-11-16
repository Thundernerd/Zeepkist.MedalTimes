using System;
using System.Linq;
using System.Threading;
using BepInEx.Configuration;
using BepInEx.Logging;
using TNRD.Zeepkist.MedalTimes.GraphQL;
using TNRD.Zeepkist.MedalTimes.Medals;
using TNRD.Zeepkist.MedalTimes.Patches;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Level;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.MedalTimes.UI;

public sealed class MedalDrawer : MonoBehaviour
{
    private readonly LevelMedalSession session = new();
    private readonly MedalVisibility visibility = new();
    private readonly MedalAssets assets = new();
    private readonly ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource("MedalTimes");
    private MedalTargetSource source;
    private ConfigFile settings;
    private GameObject canvas;
    private CanvasGroup canvasGroup;
    private TMP_FontAsset font;
    private Material fontMaterial;
    private FontStyles fontStyle;
    private FontWeight fontWeight;
    private bool missingFontLogged;
    private LevelScriptableObject loadedLevel;
    private bool stopped;

    public void Initialize(MedalTargetSource targetSource, ConfigFile config)
    {
        source = targetSource;
        // Prime events invalidate old results before SDK computes the new trusted hash.
        GeneralLevelLoader_PrimeForGameplay.PostfixEvent += OnPrimed;
        GeneralLevelLoader_PrimeForGameplay_v15.PostfixEvent += OnPrimed;
        RacingApi.LevelLoaded += OnLevelLoaded;
        RacingApi.PlayerSpawned += OnLevelLoaded;
        RacingApi.Quit += OnQuit;
        MultiplayerApi.DisconnectedFromGame += OnQuit;
        PauseMenu_DoQuitGameplay.PostfixEvent += OnQuit;
        PauseMenu_DoQuitAdventureMap.PostfixEvent += OnQuit;
        OnlineChatUI_EnableBigBox.PostfixEvent += OnBigChat;
        OnlineChatUI_EnableSmallBox.PostfixEvent += OnSmallChat;
        settings = config;
        settings.SettingChanged += OnSettingChanged;
    }

    private void Update()
    {
        if (visibility.Active && !visibility.ChatOpen && Input.GetKeyDown(Plugin.ToggleMedalTimes.Value))
        {
            visibility.UserVisible = !visibility.UserVisible;
            ApplyVisibility();
        }
    }

    private static LevelGraphqlIdentity Identity()
    {
        var level = LevelApi.CurrentLevel;
        var hash = LevelApi.CurrentHashV2;
        return LevelGraphqlIdentity.FromValues(hash?.Hash, hash?.ZeepHash,
            level?.IsAdventureLevel ?? false, level?.UseAvonturenLevel ?? false);
    }

    private void OnLevelLoaded()
    {
        if (stopped) return;
        var level = LevelApi.CurrentLevel;
        if (level == null) return;
        var identity = Identity();
        var local = new MedalTargets(level.TimeBronze, level.TimeSilver, level.TimeGold, level.TimeAuthor);
        bool newEntry = !visibility.Active || level != loadedLevel;
        if (!session.Begin(level, identity.CacheKey, local))
        {
            // HUD assets can become available after the first level callback.
            if (canvas == null) Render();
            return;
        }
        loadedLevel = level;
        visibility.Active = true;
        visibility.TestLevel = level.IsTestLevel;
        if (newEntry) visibility.UserVisible = true;
        Render();
        if (identity.IsAvailable && !level.IsTestLevel)
            Fetch(identity, level, session.Generation, session.Token).Forget();
    }

    private async UniTaskVoid Fetch(LevelGraphqlIdentity identity, LevelScriptableObject level,
        int generation, CancellationToken token)
    {
        try
        {
            var targets = await source.FetchAsync(identity, token);
            await UniTask.SwitchToMainThread();
            if (stopped || level != LevelApi.CurrentLevel || identity.CacheKey != Identity().CacheKey) return;
            if (session.Apply(generation, identity.CacheKey, targets)) Render();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            logger.LogWarning("Unable to fetch medal times: " + exception.GetType().Name);
        }
    }

    public string GetMessage(MedalTier tier) => visibility.Active && session.Targets != null
        ? session.Targets.Message(tier, DateTimeOffset.UtcNow)
        : tier + " time unavailable";

    private void Render()
    {
        if (session.Targets == null || !visibility.Active) { ApplyVisibility(); return; }
        if (!LoadGameTextStyle())
        {
            if (!missingFontLogged) logger.LogWarning("Waiting for Zeepkist HUD font and material for medal times");
            missingFontLogged = true;
            return;
        }
        missingFontLogged = false;
        ClearCanvas();
        canvas = new GameObject("MedalCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvas.layer = LayerMask.NameToLayer("UI");
        var overlay = canvas.GetComponent<Canvas>();
        overlay.sortingOrder = -1;
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
        canvas.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasGroup = canvas.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        var group = new GameObject("Group", typeof(RectTransform), typeof(VerticalLayoutGroup));
        group.transform.SetParent(canvas.transform, false);
        var rect = group.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = new Vector2(Plugin.HorizontalOffset.Value, Plugin.VerticalOffset.Value);
        var layout = group.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = (TextAnchor)Enum.Parse(typeof(TextAnchor), Plugin.AnchorPoint.Value);
        layout.spacing = 10;
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        foreach (var tier in session.Targets.Select(Plugin.ShowBronzeTime.Value, Plugin.ShowSilverTime.Value,
            Plugin.ShowGoldTime.Value, Plugin.ShowAuthorTime.Value, Plugin.ShowExpertTime.Value, Plugin.ShowApexTime.Value))
            CreateRow(group.transform, tier, session.Targets[tier].Value);
        ApplyVisibility();
    }

    private bool LoadGameTextStyle()
    {
        // TMP's default font is not Zeepkist's font. The HUD material also owns the
        // outline/underlay, so copying only TMP_FontAsset loses the game's appearance.
        TMP_Text template = PlayerManager.Instance?.currentMaster?.PlayerScreensUI?.PlayerScreens?
            .Select(screen => screen.elapsedTime)
            .FirstOrDefault(text => text != null && text.font != null && text.fontSharedMaterial != null);
        if (template == null)
        {
            var timer = PlayerManager.Instance?.currentMaster?.OnlineGameplayUI?.TimeLeftText;
            if (timer != null && timer.font != null && timer.fontSharedMaterial != null) template = timer;
        }
        if (template == null)
        {
            template = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
                .FirstOrDefault(text => text != null && text.font != null && text.fontSharedMaterial != null &&
                    text.font.name.StartsWith("ComicHelvetic", StringComparison.OrdinalIgnoreCase) &&
                    (canvas == null || !text.transform.IsChildOf(canvas.transform)));
        }
        if (template != null)
        {
            font = template.font;
            fontMaterial = template.fontSharedMaterial;
            fontStyle = template.fontStyle;
            fontWeight = template.fontWeight;
        }
        return font != null && fontMaterial != null;
    }

    private void CreateRow(Transform parent, MedalTier tier, double time)
    {
        var row = new GameObject(tier.ToString(), typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);
        var layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(row.transform, false);
        var image = icon.GetComponent<Image>();
        try { image.sprite = assets.Get(tier); }
        catch (Exception exception) { logger.LogWarning("Unable to load medal icon: " + exception.GetType().Name); }
        image.preserveAspect = true;
        image.raycastTarget = false;
        var size = icon.GetComponent<LayoutElement>();
        size.preferredWidth = size.minWidth = 30;
        size.preferredHeight = size.minHeight = 30;
        var text = new GameObject("Time", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(row.transform, false);
        var label = text.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSharedMaterial = fontMaterial;
        label.fontStyle = fontStyle;
        label.fontWeight = fontWeight;
        label.fontSize = 30;
        label.text = MedalTargets.Format(time);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
    }

    private void OnSettingChanged(object sender, EventArgs args) => Render();
    private void ApplyVisibility()
    {
        if (canvasGroup != null) canvasGroup.alpha = visibility.IsVisible(Plugin.ShowMedalTimes.Value) ? 1 : 0;
    }
    private void OnBigChat() { visibility.ChatOpen = true; ApplyVisibility(); }
    private void OnSmallChat() { visibility.ChatOpen = false; ApplyVisibility(); }
    private void OnPrimed() { session.Dispose(); visibility.Active = false; ClearCanvas(); }
    private void OnQuit() { OnPrimed(); visibility.ChatOpen = false; }
    private void ClearCanvas()
    {
        if (canvas != null) { canvas.SetActive(false); Destroy(canvas); }
        canvas = null;
        canvasGroup = null;
    }

    public void Shutdown()
    {
        if (stopped) return;
        stopped = true;
        GeneralLevelLoader_PrimeForGameplay.PostfixEvent -= OnPrimed;
        GeneralLevelLoader_PrimeForGameplay_v15.PostfixEvent -= OnPrimed;
        RacingApi.LevelLoaded -= OnLevelLoaded;
        RacingApi.PlayerSpawned -= OnLevelLoaded;
        RacingApi.Quit -= OnQuit;
        MultiplayerApi.DisconnectedFromGame -= OnQuit;
        PauseMenu_DoQuitGameplay.PostfixEvent -= OnQuit;
        PauseMenu_DoQuitAdventureMap.PostfixEvent -= OnQuit;
        OnlineChatUI_EnableBigBox.PostfixEvent -= OnBigChat;
        OnlineChatUI_EnableSmallBox.PostfixEvent -= OnSmallChat;
        if (settings != null) settings.SettingChanged -= OnSettingChanged;
        session.Dispose();
        ClearCanvas();
        assets.Dispose();
    }
    private void OnDestroy() => Shutdown();
}
