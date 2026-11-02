using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ResolutionAdjust
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "Miles.ResolutionAdjust";
        public const string Name = "ResolutionAdjust";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<int> Width;
        internal static ConfigEntry<bool> HideAboveMonitor;

        private void Awake()
        {
            Log = Logger;

            Width = Config.Bind("Resolution", "Width", 1920,
                "Width of the game's render resolution. Easiest to change in game: Settings > Pixel Resolution. " +
                "The height follows the game's 860:520 shape (1920 = 1920x1161). A width that isn't in the menu shows up there as Custom.");
            HideAboveMonitor = Config.Bind("Resolution", "HideAboveMonitor", true,
                "Hide resolutions wider than your monitor from the menu.");

            ResolutionMenu.Build();
            new Harmony(Guid).PatchAll(typeof(ResolutionMenu));

            if (Chainloader.PluginInfos.ContainsKey("HDLethalCompany"))
                Log.LogWarning("HDLethalCompany is installed too. Set its EnableRes to false so the two mods don't fight over the resolution.");

            Log.LogInfo($"{Name} {Version} loaded: {ResolutionMenu.Presets.Count} resolutions in the menu.");
        }
    }
}
