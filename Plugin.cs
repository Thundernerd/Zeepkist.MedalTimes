using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using StrawberryShake;
using TNRD.Zeepkist.MedalTimes.Commands;
using TNRD.Zeepkist.MedalTimes.GraphQL;
using TNRD.Zeepkist.MedalTimes.UI;
using UnityEngine;
using ZeepSDK.ChatCommands;

namespace TNRD.Zeepkist.MedalTimes
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("ZeepSDK", "2.7.5")]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony harmony;
        private ServiceProvider services;
        private readonly AuthorTimeChatCommand authorCommand = new();
        private readonly ApexTimeChatCommand apexCommand = new();
        private readonly ExpertTimeChatCommand expertCommand = new();
        public static MedalDrawer Drawer { get; private set; }

        public static ConfigEntry<bool> ShowMedalTimes { get; private set; }
        public static ConfigEntry<bool> ShowExpertTime { get; private set; }
        public static ConfigEntry<bool> ShowApexTime { get; private set; }
        public static ConfigEntry<bool> ShowAuthorTime { get; private set; }
        public static ConfigEntry<bool> ShowGoldTime { get; private set; }
        public static ConfigEntry<bool> ShowSilverTime { get; private set; }
        public static ConfigEntry<bool> ShowBronzeTime { get; private set; }
        public static ConfigEntry<KeyCode> ToggleMedalTimes { get; private set; }
        public static ConfigEntry<int> HorizontalOffset { get; private set; }
        public static ConfigEntry<int> VerticalOffset { get; private set; }
        public static ConfigEntry<string> AnchorPoint { get; private set; }
        private void Awake()
        {
            harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            ShowMedalTimes = Config.Bind("General", "Show Medal Times", true, "Show the medal times UI");

            ShowAuthorTime = Config.Bind("UI", "Show Author Time", true, "Show the author time");
            ShowExpertTime = Config.Bind("UI", "Show Expert Time", true, "Show Expert time; fall back to Author when selected alone and unavailable");
            ShowApexTime = Config.Bind("UI", "Show Apex Time", true, "Show Apex time; fall back to Author when selected alone and unavailable");
            ShowGoldTime = Config.Bind("UI", "Show Gold Time", true, "Show the gold time");
            ShowSilverTime = Config.Bind("UI", "Show Silver Time", true, "Show the silver time");
            ShowBronzeTime = Config.Bind("UI", "Show Bronze Time", true, "Show the bronze time");

            HorizontalOffset = Config.Bind("Positioning",
                "Horizontal Offset",
                0,
                "Horizontal offset of the medal times UI");
            VerticalOffset = Config.Bind("Positioning",
                "Vertical Offset",
                0,
                "Vertical offset of the medal times UI");
            AnchorPoint = Config.Bind("Positioning",
                "Anchor Point",
                "MiddleLeft",
                new ConfigDescription("Anchor point of the medal times UI",
                    new AcceptableValueList<string>(Enum.GetNames(typeof(TextAnchor)))));

            ToggleMedalTimes = Config.Bind("Keybinds", "Toggle Medal Times", KeyCode.PageUp, "Toggle visibility");

            var collection = new ServiceCollection();
            collection.AddMedalTimesClient(ExecutionStrategy.NetworkOnly).ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://graphql.zeepki.st");
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("MedalTimes/" + MyPluginInfo.PLUGIN_VERSION);
            });
            services = collection.BuildServiceProvider();
            Drawer = gameObject.AddComponent<MedalDrawer>();
            Drawer.Initialize(new MedalTargetSource(services.GetRequiredService<IMedalTimesClient>()), Config);

            ChatCommandApi.RegisterLocalChatCommand(authorCommand);
            ChatCommandApi.RegisterLocalChatCommand(apexCommand);
            ChatCommandApi.RegisterLocalChatCommand(expertCommand);

            // Plugin startup logic
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        }

        private void OnDestroy()
        {
            Drawer?.Shutdown();
            if (Drawer != null) Destroy(Drawer);
            Drawer = null;
            ChatCommandApi.UnregisterLocalChatCommand(authorCommand);
            ChatCommandApi.UnregisterLocalChatCommand(apexCommand);
            ChatCommandApi.UnregisterLocalChatCommand(expertCommand);
            services?.Dispose();
            harmony?.UnpatchSelf();
        }
    }
}
