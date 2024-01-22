using BaboonAPI.Hooks.Initializer;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TootTallyCore.Utils.TootTallyModules;
using TootTallyCore.Utils.TootTallyNotifs;
using TootTallySettings;
using TootTallySpectator;
using UnityEngine;
using UnityEngine.UI;

namespace TootTallyTournamentHost
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("TootTallySpectator", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("TootTallyMultiplayer", BepInDependency.DependencyFlags.HardDependency)]
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
            string configPath = Path.Combine(Paths.BepInExRootPath, "config/");
            ConfigFile config = new ConfigFile(configPath + CONFIG_NAME, true) { SaveOnConfigSet = true };
            HorizontalScreenCount = config.Bind("Global", "HorizontalScreenCount", 4f, "Amount of screen displayed horizontally");
            VerticalScreenCount = config.Bind("Global", "VerticalScreenCount", 2f, "Amount of screen displayed vertically");
            UserIDs = config.Bind("Global", "UserIDs", "0,0,0;0,0,0", "List of user IDs to spectate");

            settingPage = TootTallySettingsManager.AddNewPage("TournamentHost", "Tournament Host", 40f, new Color(0, 0, 0, 0));
            settingPage?.AddSlider("Hori Screens", 1, 10, HorizontalScreenCount, true);
            settingPage?.AddSlider("Vert Screens", 1, 10, VerticalScreenCount, true);
            settingPage?.AddLabel("UserIDs");
            settingPage?.AddTextField("UserIDs", UserIDs.Value, false, value => UserIDs.Value = value);
            _harmony.PatchAll(typeof(TournamentHostPatches));
            LogInfo($"Module loaded!");
        }

        public void UnloadModule()
        {
            _harmony.UnpatchSelf();
            settingPage.Remove();
            LogInfo($"Module unloaded!");
        }

        public static class TournamentHostPatches
        {
            private static Vector2 _screenSize;
            private static List<TournamentGameplayController> _tournamentControllerList = new List<TournamentGameplayController>();
            [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
            [HarmonyPostfix]
            public static void OnGameControllerStart(GameController __instance)
            {
                _tournamentControllerList?.Clear();
                _screenSize = new Vector2(Screen.width, Screen.height);
                float horizontalScreenCount = (int)Instance.HorizontalScreenCount.Value;
                float horizontalRatio = _screenSize.x / horizontalScreenCount;
                float verticalScreenCount = (int)Instance.VerticalScreenCount.Value;
                float verticalRatio = _screenSize.y / verticalScreenCount;
                var gameplayCanvas = GameObject.Find("GameplayCanvas");
                gameplayCanvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var sizeDelta = gameplayCanvas.GetComponent<RectTransform>().sizeDelta;
                gameplayCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(sizeDelta.x / horizontalScreenCount, sizeDelta.y);
                gameplayCanvas.GetComponent<RectTransform>().pivot = new Vector2(.5f * (horizontalScreenCount - (horizontalScreenCount - verticalScreenCount)), .5f);
                var botLeftCam = GameObject.Find("GameplayCam").GetComponent<Camera>();

                var canvasObject = new GameObject($"TournamentGameplayCanvas");
                var canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;

                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.scaleFactor = 2.4f / verticalScreenCount;
                canvas.scaleFactor = 2.4f / verticalScreenCount;

                var gridLayout = canvasObject.AddComponent<GridLayoutGroup>();
                gridLayout.cellSize = new Vector2(horizontalRatio / canvas.scaleFactor, verticalRatio / canvas.scaleFactor);
                gridLayout.startCorner = GridLayoutGroup.Corner.LowerLeft;

                var IDs = Instance.UserIDs.Value.Split(';');
                string[][] idList = new string[IDs.Length][];
                for (int i = 0; i < IDs.Length; i++)
                    idList[i] = IDs[i].Split(',');
                /*
                / grist:1
                / dom: 62
                / Samuran: 7
                / Danew: 106
                / Silver : 98
                / Beta : 11
                / Guardie : 114
                / Dew : 374
                / Static : 242
                / PX : 372
                */
                for (int y = 0; y < verticalScreenCount; y++)
                {
                    for (int x = 0; x < horizontalScreenCount; x++)
                    {
                        if (int.TryParse(idList[y][x], out int id) && id != 0)
                        {
                            var tc = gameplayCanvas.AddComponent<TournamentGameplayController>();
                            tc.Initialize(__instance, GameObject.Instantiate(botLeftCam), new Rect(x * horizontalRatio, y * verticalRatio, horizontalRatio, verticalRatio), canvasObject.transform, new SpectatingSystem(id, idList[y][x].ToString()));
                            _tournamentControllerList.Add(tc);
                        }
                    }
                }
                LeanTween.init(LeanTween.maxTweens * (int)verticalScreenCount * (int)horizontalScreenCount);
                botLeftCam.enabled = false;
                __instance.pointer.transform.localScale = Vector2.zero;
                __instance.ui_score_shadow.transform.parent.parent.transform.localScale = Vector3.zero;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.getScoreAverage))]
            [HarmonyPrefix]
            public static bool OnGetScoreAveragePrefix()
            {
                _tournamentControllerList.ForEach(tc => tc.OnGetScoreAverage());
                return false;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.tallyScore))]
            [HarmonyPrefix]
            public static bool OnTallyScorePrefix()
            {
                _tournamentControllerList.ForEach(tc => tc.OnTallyScore());
                return false;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.fixAudioMixerStuff))]
            [HarmonyPrefix]
            public static void CopyAllAudioClips()
            {
                _tournamentControllerList.ForEach(tc => tc.CopyAllAudioClips());
            }

            [HarmonyPatch(typeof(LevelSelectController), nameof(LevelSelectController.Start))]
            [HarmonyPostfix]
            public static void DisconnectAllClients()
            {
                _tournamentControllerList.ForEach(tc => tc.Disconnect());
                _tournamentControllerList.Clear();
            }

            private static bool _waitingToSync;

            [HarmonyPatch(typeof(GameController), nameof(GameController.playsong))]
            [HarmonyPrefix]
            public static bool OverwriteStartSongIfSyncRequired(GameController __instance)
            {
                if (ShouldWaitForSync(out _waitingToSync))
                    TootTallyNotifManager.DisplayNotif("Waiting to sync with host...");

                return !_waitingToSync;
            }

            [HarmonyPatch(typeof(GameController), nameof(GameController.Update))]
            [HarmonyPostfix]
            public static void OnUpdatePlaybackSpectatingData(GameController __instance)
            {
                if (_waitingToSync && __instance.curtainc.doneanimating && !ShouldWaitForSync(out _waitingToSync))
                    __instance.startSong(false);
            }

            private static bool ShouldWaitForSync(out bool waitForSync)
            {
                waitForSync = true;
                if (!_tournamentControllerList.Any(x => !x.IsReady))
                    waitForSync = false;
                if (Input.GetKey(KeyCode.Space))
                    waitForSync = false;
                return waitForSync;
            }
        }

        public ConfigEntry<float> HorizontalScreenCount { get; set; }
        public ConfigEntry<float> VerticalScreenCount { get; set; }
        public ConfigEntry<string> UserIDs { get; set; }
    }
}