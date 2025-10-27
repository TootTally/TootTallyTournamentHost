using BaboonAPI.Hooks.Initializer;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TootTallyCore.Utils.Assets;
using TootTallyCore.Utils.TootTallyGlobals;
using TootTallyCore.Utils.TootTallyModules;
using TootTallyCore.Utils.TootTallyNotifs;
using TootTallyGameModifiers;
using TootTallyLeaderboard.Replays;
using TootTallyMultiplayer;
using TootTallySettings;
using TootTallySpectator;
using UnityEngine;
using UnityEngine.UI;
using static TootTallyTournamentHost.TournamentHostManager;

namespace TootTallyTournamentHost
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("TootTallySpectator", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("TootTallyLeaderboard", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("TootTallyMultiplayer", BepInDependency.DependencyFlags.HardDependency)]
    //[BepInDependency("com.hypersonicsharkz.highscoreaccuracy", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin, ITootTallyModule
    {
        public static Plugin Instance;

        private const string CONFIG_NAME = "TournamentHost.cfg";
        private Harmony _harmony;
        public ConfigEntry<bool> ModuleConfigEnabled { get; set; }
        public bool IsConfigInitialized { get; set; }

        //Change this name to whatever you want
        public string Name { get => "Tournament Host"; set => Name = value; }

        public static TootTallySettingPage settingPage;

        public static void LogInfo(string msg) => Instance.Logger.LogInfo(msg);
        public static void LogError(string msg) => Instance.Logger.LogError(msg);

        private void Awake()
        {
            if (Instance != null) return;
            Instance = this;
            _harmony = new Harmony(Info.Metadata.GUID);

            GameInitializationEvent.Register(Info, TryInitialize);
        }

        private void TryInitialize()
        {
            // Bind to the TTModules Config for TootTally
            ModuleConfigEnabled = TootTallyCore.Plugin.Instance.Config.Bind("Modules", "TournamentHost", true, "Tournament host client for TootTally");
            TootTallyModuleManager.AddModule(this);
            TootTallySettings.Plugin.Instance.AddModuleToSettingPage(this);
        }

        public void LoadModule()
        {
            AssetManager.LoadAssets(Path.Combine(Path.GetDirectoryName(Instance.Info.Location), "Assets"));
            string configPath = Path.Combine(Paths.BepInExRootPath, "config/");
            ConfigFile config = new ConfigFile(configPath + CONFIG_NAME, true) { SaveOnConfigSet = true };
            LayoutType = config.Bind("Global", nameof(LayoutType), TournamentHostManager.LayoutType.OneVsOne, "Type of Layout to display the users.");
            HorizontalScreenCount = config.Bind("Global", "HorizontalScreenCount", 2f, "Amount of screen displayed horizontally.");
            VerticalScreenCount = config.Bind("Global", "VerticalScreenCount", 2f, "Amount of screen displayed vertically.");
            EnableNoteParticles = config.Bind("Global", nameof(EnableNoteParticles), true, "Enables the note end effect.");
            EnableMissGlow = config.Bind("Global", nameof(EnableMissGlow), true, "Enables the miss effect when breaking combo.");
            EnableScoreText = config.Bind("Global", nameof(EnableScoreText), true, "Enables score display on the top right.");
            EnableScorePercentText = config.Bind("Global", nameof(EnableScorePercentText), true, "Enables score percent display on the top right.");
            EnableChampMeter = config.Bind("Global", nameof(EnableChampMeter), true, "Enables the Champ Meter.");
            EnableTimeElapsed = config.Bind("Global", nameof(EnableTimeElapsed), true, "Enable the time left for the song.");
            EnableHighestCombo = config.Bind("Global", nameof(EnableHighestCombo), true, "Enable the highest combo counter.");
            EnableUsername = config.Bind("Global", nameof(EnableUsername), true, "Enable the username display.");
            UserIDs = config.Bind("Global", "UserIDs", "0,0;0,0", "List of user IDs to spectate");
            settingPage = TootTallySettingsManager.AddNewPage(new TournamentHostSettingPage());
            _harmony.PatchAll(typeof(TournamentHostManager));
            TootTallyGlobalVariables.isTournamentHosting = true;
            LogInfo($"Module loaded!");
        }

        public void UnloadModule()
        {
            TootTallyGlobalVariables.isTournamentHosting = false;
            _harmony.UnpatchSelf();
            settingPage.Remove();
            TootTallyGlobalVariables.isTournamentHosting = false;
            LogInfo($"Module unloaded!");
        }

        public ConfigEntry<LayoutType> LayoutType { get; set; }
        public ConfigEntry<float> HorizontalScreenCount { get; set; }
        public ConfigEntry<float> VerticalScreenCount { get; set; }
        public ConfigEntry<bool> EnableNoteParticles { get; set; }
        public ConfigEntry<bool> EnableMissGlow { get; set; }
        public ConfigEntry<bool> EnableScoreText { get; set; }
        public ConfigEntry<bool> EnableScorePercentText { get; set; }
        public ConfigEntry<bool> EnableChampMeter { get; set; }
        public ConfigEntry<bool> EnableTimeElapsed { get; set; }
        public ConfigEntry<bool> EnableHighestCombo { get; set; }
        public ConfigEntry<bool> EnableUsername { get; set; }
        public ConfigEntry<string> UserIDs { get; set; }
        public static ConfigEntry<float> StartFade, EndFade;
        public static ConfigEntry<float> FLIntensity;
    }
}
