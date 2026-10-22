using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace HeritageXR
{
    public sealed class MuseumLocalization
    {
        [Serializable] sealed class Entry { public string key, fr, en, ar; }
        [Serializable] sealed class Strings { public Entry[] entries; }
        [Serializable] public sealed class ArtworkText { public string title, summary, description; }
        [Serializable] sealed class ArtworkEntry { public string id; public ArtworkText fr, en, ar; }
        [Serializable] sealed class Artworks { public ArtworkEntry[] artworks; }
        readonly Dictionary<string, Entry> strings = new();
        readonly Dictionary<string, ArtworkEntry> artworks = new();
        public string Language { get; private set; }
        public bool IsArabic => Language == "ar";

        public MuseumLocalization()
        {
            var ui = Resources.Load<UnityEngine.TextAsset>("Localization/MuseumStrings");
            var descriptions = Resources.Load<UnityEngine.TextAsset>("Localization/ArtworkTexts");
            if (ui == null || descriptions == null) throw new InvalidOperationException("Missing museum language resources.");
            foreach (var entry in JsonUtility.FromJson<Strings>(ui.text).entries) strings.Add(entry.key, entry);
            foreach (var entry in JsonUtility.FromJson<Artworks>(descriptions.text).artworks) artworks.Add(entry.id, entry);
            string saved = PlayerPrefs.GetString("heritage.language", "fr");
            Language = saved == "ar" || saved == "en" ? saved : "fr";
        }

        public void SetLanguage(string language)
        {
            if (language != "fr" && language != "en" && language != "ar") return;
            Language = language;
            PlayerPrefs.SetString("heritage.language", language);
            PlayerPrefs.Save();
        }
        public string Get(string key)
        {
            if (!strings.TryGetValue(key, out var entry)) return key;
            return Language == "ar" ? entry.ar : Language == "en" ? entry.en : entry.fr;
        }
        public ArtworkText Artwork(MuseumArtwork artwork)
        {
            if (artwork != null && artworks.TryGetValue(artwork.id, out var entry))
                return Language == "ar" ? entry.ar : Language == "en" ? entry.en : entry.fr;
            return new ArtworkText { title = artwork?.title ?? "", summary = artwork?.summary ?? "", description = artwork?.description ?? "" };
        }
    }

    public static class MuseumFonts
    {
        static FontAsset body, display, arabic;
        public static FontAsset Body => body ??= Create("NotoSans-Regular");
        public static FontAsset Display => display ??= Create("NotoSerif-Regular");
        public static FontAsset Arabic => arabic ??= Create("NotoSansArabic-Regular");
        public static void Initialize()
        {
            Body.fallbackFontAssetTable = new List<FontAsset>();
            Display.fallbackFontAssetTable = new List<FontAsset> { Body };
            Arabic.fallbackFontAssetTable = new List<FontAsset> { Body };
        }
        static FontAsset Create(string name)
        {
            var font = Resources.Load<Font>("Fonts/" + name);
            if (font == null) throw new InvalidOperationException("Missing embedded font: " + name);
            var asset = FontAsset.CreateFontAsset(font, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = name + " Runtime";
            asset.hideFlags = HideFlags.DontSave;
            return asset;
        }
    }
}
