using System;
using System.Collections.Generic;
using System.IO;
using TNRD.Zeepkist.MedalTimes.Medals;
using UnityEngine;

namespace TNRD.Zeepkist.MedalTimes.UI;

internal sealed class MedalAssets : IDisposable
{
    private readonly Dictionary<MedalTier, Sprite> embedded = new();

    public Sprite Get(MedalTier tier)
    {
        var player = PlayerManager.Instance;
        switch (tier)
        {
            case MedalTier.Bronze: return player.bronzeMedal;
            case MedalTier.Silver: return player.silverMedal;
            case MedalTier.Gold: return player.goldMedal;
            case MedalTier.Author: return player.authorMedal;
        }
        if (embedded.TryGetValue(tier, out var sprite)) return sprite;
        string name = $"TNRD.Zeepkist.MedalTimes.RuntimeAssets.Medals.MED_{tier}.png";
        using var stream = typeof(MedalAssets).Assembly.GetManifestResourceStream(name);
        if (stream == null) throw new FileNotFoundException("Embedded medal missing", name);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!ImageConversion.LoadImage(texture, buffer.ToArray())) throw new InvalidDataException("Invalid medal PNG");
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            if (SystemInfo.SupportsTextureFormat(TextureFormat.DXT5)) texture.Compress(true);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            embedded.Add(tier, sprite);
            return sprite;
        }
        catch { UnityEngine.Object.Destroy(texture); throw; }
    }

    public void Dispose()
    {
        foreach (var sprite in embedded.Values)
        {
            UnityEngine.Object.Destroy(sprite.texture);
            UnityEngine.Object.Destroy(sprite);
        }
        embedded.Clear();
    }
}
