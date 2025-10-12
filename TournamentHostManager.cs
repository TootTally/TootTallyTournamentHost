using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TootTallyCore.Utils.TootTallyNotifs;
using TootTallyGameModifiers;
using TootTallyLeaderboard.Replays;
using TootTallyMultiplayer;
using TootTallySpectator;
using UnityEngine;
using UnityEngine.UI;

namespace TootTallyTournamentHost
{
    public static class TournamentHostManager
    {
        private static Vector2 _screenSize;
        private static List<TournamentGameplayController> _tournamentControllerList = new List<TournamentGameplayController>();

        [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
        [HarmonyPostfix]
        public static void OnGameControllerStart(GameController __instance)
        {
            SpectatingManager.StopAllSpectator();
            _tournamentControllerList?.Clear();
            _screenSize = new Vector2(Screen.width, Screen.height);
            float horizontalScreenCount = (int)Plugin.Instance.HorizontalScreenCount.Value;
            float horizontalRatio = _screenSize.x / horizontalScreenCount;
            float verticalScreenCount = (int)Plugin.Instance.VerticalScreenCount.Value;
            float verticalRatio = _screenSize.y / verticalScreenCount;
            var gameplayCanvas = GameObject.Find("GameplayCanvas");

            gameplayCanvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var sizeDelta = gameplayCanvas.GetComponent<RectTransform>().sizeDelta;
            gameplayCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(sizeDelta.x / horizontalScreenCount, sizeDelta.y);
            gameplayCanvas.GetComponent<RectTransform>().pivot = new Vector2(.5f * (horizontalScreenCount - (horizontalScreenCount - verticalScreenCount)), .5f);
            var botLeftCam = GameObject.Find("GameplayCam").GetComponent<Camera>();
            var bgCam = __instance.bgcontroller.fullbgobject.GetComponent<Camera>();

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
            //gridLayout.childAlignment = TextAnchor.LowerLeft;

            var IDs = ConvertStringToMatrix(Plugin.Instance.UserIDs.Value);
            for (int y = 0; y < verticalScreenCount; y++)
            {
                for (int x = 0; x < horizontalScreenCount; x++)
                {
                    var id = IDs[y][x];
                    var tc = gameplayCanvas.AddComponent<TournamentGameplayController>();
                    tc.Initialize(__instance,
                        GameObject.Instantiate(botLeftCam),
                        GameObject.Instantiate(bgCam),
                        new Rect(x * horizontalRatio, y * verticalRatio, horizontalRatio, verticalRatio),
                        canvasObject.transform,
                        id > 0 ? new SpectatingSystem(id, id.ToString()) : null,
                        id < 0);
                    _tournamentControllerList.Add(tc);
                }
            }
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
            //_tournamentControllerList.ForEach(tc => tc.OnTallyScore());
            return false;
        }

        [HarmonyPatch(typeof(GameController), nameof(GameController.fixAudioMixerStuff))]
        [HarmonyPrefix]
        public static void CopyAllAudioClips()
        {
            _tournamentControllerList?.ForEach(tc => tc.CopyAllAudioClips());
        }

        [HarmonyPatch(typeof(PlaytestAnims), nameof(PlaytestAnims.Start))]
        [HarmonyPatch(typeof(PointSceneController), nameof(PointSceneController.Start))]
        [HarmonyPostfix]
        public static void DisconnectAllClients()
        {
            _tournamentControllerList?.ForEach(tc => tc.Disconnect());
            _tournamentControllerList?.Clear();
        }


        [HarmonyPatch(typeof(GameModifiers.Flashlight), nameof(GameModifiers.Flashlight.Initialize))]
        [HarmonyPrefix]
        public static bool InitFlashlight()
        {
            _tournamentControllerList?.ForEach(tc => tc.InitFlashLight());
            return false;
        }

        [HarmonyPatch(typeof(GameModifiers.Flashlight), nameof(GameModifiers.Flashlight.Update))]
        [HarmonyPrefix]
        public static bool UpdateFlashlight()
        {
            _tournamentControllerList?.ForEach(tc => tc.UpdateFlashLight());
            return false;
        }

        public static bool isWaitingForSync;

        [HarmonyPatch(typeof(GameController), nameof(GameController.playsong))]
        [HarmonyPrefix]
        public static bool OverwriteStartSongIfSyncRequired(GameController __instance)
        {
            if (ShouldWaitForSync(out isWaitingForSync))
                TootTallyNotifManager.DisplayNotif("Waiting to sync with host...");

            return !isWaitingForSync;
        }

        [HarmonyPatch(typeof(GameController), nameof(GameController.Update))]
        [HarmonyPostfix]
        public static void OnUpdatePlaybackSpectatingData(GameController __instance)
        {
            if (isWaitingForSync && __instance.curtainc.doneanimating && !ShouldWaitForSync(out isWaitingForSync))
            {
                if (MultiplayerManager.IsPlayingMultiplayer)
                    MultiplayerManager.GetMultiplayerController.SendQuickChat(42069);
                __instance.startSong(false);
            }
        }

        [HarmonyPatch(typeof(MultiplayerSystem), nameof(MultiplayerSystem.SendUpdateScore))]
        [HarmonyPrefix]
        public static bool OnUpdatePlaybackSpectatingData() => false;

        [HarmonyPatch(typeof(ReplaySystemManager), nameof(ReplaySystemManager.ShouldSubmitReplay))]
        [HarmonyPrefix]
        public static void OverwriteShouldSubmitReplay(ref bool __result)
        {
            __result = false;
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

        public static int[][] ConvertStringToMatrix(string userIDs)
        {
            var userColumns = userIDs.Split(';');
            int[][] matrix = new int[userColumns.Length][];
            for (int i = 0; i < userColumns.Length; i++)
            {
                var userRows = userColumns[i].Split(',');
                matrix[i] = new int[userRows.Length];
                for (int j = 0; j < userRows.Length; j++)
                {
                    if (int.TryParse(userRows[j], out int row))
                        matrix[i][j] = row;
                    else
                        matrix[i][j] = 0;
                }
            }
            return matrix;
        }
        public static string ConvertMatrixToString(int[][] userIDMatrix)
        {
            var userIDString = "";
            for (int i = 0; i < userIDMatrix.Length; i++)
            {
                userIDString += userIDMatrix[i].Join(delimiter: ",");
                if (i != userIDMatrix.Length - 1)
                    userIDString += ";";
            }
            return userIDString;
        }

        public enum LayoutType
        {
            OneVsOne,
            TwoVsTwo,
            ThreeVsThree,
            FourVsFour,
            Custom,
        }
    }
}
