using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace ResolutionAdjust
{
    // v81 has a "Pixel Resolution" dropdown (Default / Performance / Retro ...).
    // This replaces its entries with real resolutions. While the game runs,
    // Settings.pixelRes holds an index into Presets; the chosen width is kept
    // in our config, and the game's own save file only ever gets a vanilla 0-3.
    internal static class ResolutionMenu
    {
        internal class Preset
        {
            public int W, H;
            public string Label;
        }

        internal static readonly List<Preset> Presets = new List<Preset>();

        const int VanillaW = 860, VanillaH = 520;
        static int monitorWidth;
        static bool settingsLoaded;

        // ---------- preset list ----------

        internal static void Build()
        {
            Presets.Clear();

            try { monitorWidth = Display.main != null ? Display.main.systemWidth : 0; } catch { }
            if (monitorWidth <= 0) try { monitorWidth = Screen.currentResolution.width; } catch { }
            bool hide = Plugin.HideAboveMonitor.Value && monitorWidth > 0;

            foreach (var (w, tag) in new[] { (3840, "4K"), (2560, "1440p"), (1920, "1080p"), (1280, "720p") })
                if (!hide || w <= monitorWidth + 1) AddWide(w, tag);
            Add(860, 520, "Vanilla");
            Add(620, 364, "Low");
            Add(400, 260, "Retro");
            Add(186, 104, "Very Retro");

            if (monitorWidth > VanillaW && Find(monitorWidth) < 0) AddWide(monitorWidth, "Native");

            int cw = Plugin.Width.Value;
            if (cw > 0 && Find(cw) < 0) AddWide(cw, "Custom");

            Presets.Sort((a, b) => b.W.CompareTo(a.W));
        }

        static void AddWide(int w, string tag) =>
            Add(w, (int)Math.Round(w * (double)VanillaH / VanillaW), tag);

        static void Add(int w, int h, string tag) =>
            Presets.Add(new Preset { W = w, H = h, Label = $"{w}x{h} ({tag})" });

        static int Find(int width)
        {
            for (int i = 0; i < Presets.Count; i++)
                if (Math.Abs(Presets[i].W - width) <= 1) return i;
            return -1;
        }

        static int ConfigIndex()
        {
            int i = Find(Plugin.Width.Value);
            return i >= 0 ? i : DefaultIndex();
        }

        // 1080p, or the biggest one that fits the monitor.
        static int DefaultIndex()
        {
            int i = Find(1920);
            if (i >= 0) return i;
            for (i = 0; i < Presets.Count; i++)
                if (Presets[i].W <= 1920) return i;
            return 0;
        }

        static int Clamp(int i) => Mathf.Clamp(i, 0, Presets.Count - 1);

        static int VanillaEquivalent(Preset p)
        {
            if (p.W >= 860) return 0;
            if (p.W >= 620) return 1;
            if (p.W >= 400) return 2;
            return 3;
        }

        // ---------- applying ----------

        static void Apply(Preset p, RenderTexture tex)
        {
            Resize(tex, p.W, p.H);

            // The local player's camera normally renders into the same texture,
            // but resize it too in case another mod swapped it.
            try
            {
                PlayerControllerB local = StartOfRound.Instance != null ? StartOfRound.Instance.localPlayerController : null;
                RenderTexture camTex = local != null && local.gameplayCamera != null ? local.gameplayCamera.targetTexture : null;
                if (camTex != null && camTex != tex) Resize(camTex, p.W, p.H);
            }
            catch { }
        }

        static void Resize(RenderTexture tex, int w, int h)
        {
            if (tex == null || (tex.width == w && tex.height == h)) return;
            tex.Release();
            tex.width = w;
            tex.height = h;
        }

        static void ApplySaved()
        {
            IngamePlayerSettings s = IngamePlayerSettings.Instance;
            if (s == null || !settingsLoaded) return;
            Apply(Presets[Clamp(s.settings.pixelRes)], s.playerGameplayScreenTex);
        }

        // ---------- game patches ----------

        // Fresh / reset settings start at the default resolution.
        [HarmonyPatch(typeof(IngamePlayerSettings.Settings), MethodType.Constructor, new[] { typeof(bool), typeof(bool) })]
        [HarmonyPostfix]
        static void SettingsCtorPostfix(IngamePlayerSettings.Settings __instance)
        {
            if (Presets.Count > 0) __instance.pixelRes = DefaultIndex();
        }

        // The saved PixelRes is a vanilla index; ours comes from the config.
        [HarmonyPatch(typeof(IngamePlayerSettings), "LoadSettingsFromPrefs")]
        [HarmonyPostfix]
        static void LoadPostfix(IngamePlayerSettings __instance)
        {
            int i = ConfigIndex();
            __instance.settings.pixelRes = i;
            __instance.unsavedSettings.pixelRes = i;
            settingsLoaded = true;
        }

        [HarmonyPatch(typeof(IngamePlayerSettings), "SetPixelResolution")]
        [HarmonyPrefix]
        static bool SetPixelResolutionPrefix(int value, IngamePlayerSettings __instance)
        {
            value = Clamp(value);
            __instance.unsavedSettings.pixelRes = value;
            Apply(Presets[value], __instance.playerGameplayScreenTex);
            return false;
        }

        // Save the choice to our config, and keep the game's save file vanilla.
        [HarmonyPatch(typeof(IngamePlayerSettings), "SaveSettingsToPrefs")]
        [HarmonyPrefix]
        static void SavePrefix(IngamePlayerSettings __instance, out int __state)
        {
            __state = -1;
            if (!settingsLoaded) return;
            Preset p = Presets[Clamp(__instance.settings.pixelRes)];
            if (Find(Plugin.Width.Value) != Presets.IndexOf(p)) Plugin.Width.Value = p.W;
            __state = __instance.settings.pixelRes;
            __instance.settings.pixelRes = VanillaEquivalent(p);
        }

        [HarmonyPatch(typeof(IngamePlayerSettings), "SaveSettingsToPrefs")]
        [HarmonyPostfix]
        static void SavePostfix(IngamePlayerSettings __instance, int __state)
        {
            if (__state >= 0) __instance.settings.pixelRes = __state;
        }

        // Other mods (e.g. HDLethalCompany) may resize the camera when a player spawns;
        // put ours back afterwards.
        [HarmonyPatch(typeof(PlayerControllerB), "Start")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        static void PlayerStartPostfix() => ApplySaved();

        [HarmonyPatch(typeof(SettingsOption), "OnEnable")]
        [HarmonyPostfix]
        static void OptionEnablePostfix(SettingsOption __instance) => FillDropdown(__instance);

        [HarmonyPatch(typeof(SettingsOption), "SetValueToMatchSettings")]
        [HarmonyPostfix]
        static void OptionMatchPostfix(SettingsOption __instance) => FillDropdown(__instance);

        // Long labels shrink to fit on one line instead of wrapping over each other.
        static void FitText(TMP_Text t)
        {
            if (t == null || t.enableAutoSizing) return;
            float size = t.fontSize;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.fontSizeMax = size;
            t.fontSizeMin = size * 0.55f;
            t.enableAutoSizing = true;
        }

        static void FillDropdown(SettingsOption option)
        {
            if (option.optionType != SettingsOptionType.PixelRes) return;
            TMP_Dropdown dd = option.GetComponentInChildren<TMP_Dropdown>(true);
            if (dd == null || IngamePlayerSettings.Instance == null) return;

            bool same = dd.options.Count == Presets.Count;
            for (int i = 0; same && i < Presets.Count; i++)
                same = dd.options[i].text == Presets[i].Label;
            if (!same)
            {
                FitText(dd.itemText);
                FitText(dd.captionText);
                List<string> labels = new List<string>(Presets.Count);
                foreach (Preset p in Presets) labels.Add(p.Label);
                dd.ClearOptions();
                dd.AddOptions(labels);
            }
            dd.SetValueWithoutNotify(Clamp(IngamePlayerSettings.Instance.settings.pixelRes));
            dd.RefreshShownValue();
        }
    }
}
