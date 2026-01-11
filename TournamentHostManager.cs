using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TootTallyCore.Utils.TootTallyNotifs;
using TootTallyGameModifiers;
using TootTallyLeaderboard.Replays;
using TootTallyMultiplayer;
using TootTallyMultiplayer.MultiplayerPanels;
using TootTallySpectator;
using UnityEngine;
using UnityEngine.Experimental.AI;
using UnityEngine.UI;
using UnityEngine.Video;
using static TootTallyMultiplayer.APIService.MultSerializableClasses;

namespace TootTallyTournamentHost
{
    public static class TournamentHostManager
    {
        private static Vector2 _screenSize;
        private static List<TournamentGameplayController> _tournamentControllerList = new List<TournamentGameplayController>();

        private static int _oldChosenSoundset;

        [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
        [HarmonyPrefix]
        public static void OnGameControllerStartPrefix(GameController __instance)
        {
            _oldChosenSoundset = GlobalVariables.chosen_soundset;
            GlobalVariables.chosen_soundset = 0;
        }

        [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
        [HarmonyPostfix]
        public static void OnGameControllerStart(GameController __instance)
        {
            _currentNoteIndex = __instance.beatstoshow;
            GlobalVariables.chosen_soundset = _oldChosenSoundset;
            __instance.latency_offset = 0;
            SpectatingManager.StopAllSpectator();
            _tournamentControllerList?.Clear();
            _screenSize = new Vector2(Screen.width, Screen.height);
            float horizontalScreenCount = (int)Plugin.Instance.HorizontalScreenCount.Value;
            float horizontalRatio = _screenSize.x / horizontalScreenCount;
            float verticalScreenCount = (int)Plugin.Instance.VerticalScreenCount.Value;
            float verticalRatio = _screenSize.y / verticalScreenCount;
            var gameplayCanvas = GameObject.Find("GameplayCanvas");
            Plugin.LogInfo($"Screen size: {_screenSize.x} x {_screenSize.y}");

            gameplayCanvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var sizeDelta = gameplayCanvas.GetComponent<RectTransform>().sizeDelta;
            gameplayCanvas.GetComponent<RectTransform>().sizeDelta = new Vector2(sizeDelta.x / horizontalScreenCount, sizeDelta.y);
            gameplayCanvas.GetComponent<RectTransform>().pivot = new Vector2(.5f * (horizontalScreenCount - (horizontalScreenCount - verticalScreenCount)), .5f);
            var botLeftCam = GameObject.Find("GameplayCam").GetComponent<Camera>();
            var bgController = __instance.bgcontroller.fullbgobject.GetComponent<Camera>();
            /*if (bgController.transform.GetChild(0).GetChild(0).TryGetComponent(out VideoPlayer vp))
            {
                var image = vp.gameObject.AddComponent<RawImage>();
                //RenderTexture renderImage = new RenderTexture(Screen.width, Screen.height, -10);
                //vp.targetTexture = renderImage;
                vp.renderMode = VideoRenderMode.RenderTexture;
                vp.targetTexture = vp.texture as RenderTexture;
                image.texture = vp.targetTexture;
            }*/
            var bgCamera = new GameObject("bgCamera", typeof(Camera)).GetComponent<Camera>();
            bgCamera.CopyFrom(bgController);
            bgCamera.depth = -9f;
            bgCamera.transform.localPosition = new Vector2(-4000, 4000);

            var canvasObject = new GameObject($"TournamentGameplayCanvas");
            /*var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.scaleFactor = 2.4f / verticalScreenCount;
            canvas.scaleFactor = 2.4f / verticalScreenCount;*/
            var scaleFactor = 2.4f / verticalScreenCount;

            var gridLayout = canvasObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(horizontalRatio / scaleFactor, verticalRatio / scaleFactor);
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
                        GameObject.Instantiate(bgCamera),
                        new Rect(x * horizontalRatio, y * verticalRatio, horizontalRatio, verticalRatio),
                        canvasObject.transform,
                        scaleFactor,
                        id > 0 ? new SpectatingSystem(id, id.ToString()) : null,
                        id);
                    _tournamentControllerList.Add(tc);
                }
            }
            GameObject.DestroyImmediate(bgCamera);
            botLeftCam.enabled = false;
            __instance.pointer.transform.localScale = Vector2.zero;
            __instance.ui_score_shadow.transform.parent.parent.transform.localScale = Vector3.zero;
            __instance.noteholder.SetActive(false);
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

        private static int _currentNoteIndex;

        [HarmonyPatch(typeof(GameController), nameof(GameController.grabNoteRefs))]
        [HarmonyPostfix]
        public static void OnGetScoreAveragePrefix(GameController __instance, int indexinc)
        {
            if (indexinc == 0 || _tournamentControllerList == null || __instance.leveldata == null || _currentNoteIndex > __instance.leveldata.Count - 1) return;
            var index = _currentNoteIndex++;
            _tournamentControllerList.ForEach(tc => Plugin.Instance.StartCoroutine(WaitForSecondsCallback(.1f,
                delegate
                {
                    tc.BuildSingleNote(index);
                }))
            );
        }

        [HarmonyPatch(typeof(GameController), nameof(GameController.pauseQuitLevel))]
        [HarmonyPostfix]
        public static void OnQuitDisconnect()
        {
            _tournamentControllerList?.ForEach(tc => tc.Disconnect());
            _tournamentControllerList?.Clear();
        }

        public static IEnumerator<WaitForSeconds> WaitForSecondsCallback(float seconds, Action callback)
        {
            yield return new WaitForSeconds(seconds);
            callback();
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
                _tournamentControllerList?.ForEach(tc => tc.StartVideoPlayer());
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

        [HarmonyPatch(typeof(MultiplayerLobbyPanel), nameof(MultiplayerLobbyPanel.UpdateLobbyInfo))]
        [HarmonyPostfix]
        public static void OnRefreshLobbyInfoPostfix(List<MultiplayerUserInfo> users)
        {
            Plugin.LogInfo("Entered lobby info overwrite.");
            if (Plugin.Instance.LayoutType.Value != LayoutType.Automatic || !MultiplayerManager.IsConnectedToMultiplayer)
                return;
            Plugin.LogInfo("Setting everything up.");
            var playerList = users.Where(x => x.state != "Spectating" && x.id != TootTallyAccounts.TootTallyUser.userInfo.id).OrderBy(x => x.id).ToList();
            var playerCount = playerList.Count;
            Plugin.LogInfo($"Player Count is {playerCount}");
            Vector2 dims = PlayerCountToDims(playerCount);
            Plugin.LogInfo($"Dimensions set to X:{dims.x} by Y:{dims.y}");
            Plugin.Instance.HorizontalScreenCount.Value = dims.x;
            Plugin.Instance.VerticalScreenCount.Value = dims.y;
            string userIDs = "";
            if (playerCount != 2)
            {
                for (int i = 0; i < dims.y; i++)
                {
                    for (int j = 0; j < dims.x; j++)
                    {
                        if (i * dims.x + j < playerCount)
                            userIDs += playerList[i * (int)dims.x + j].id;
                        else
                            userIDs += "0";
                        if (j < dims.x - 1)
                            userIDs += ",";
                    }
                    if (i < dims.y - 1)
                        userIDs += ";";
                }
            }
            else //Fuck that shit, specific layout for 1v1
                userIDs = $"{playerList[0].id},0;{playerList[1].id},0";



                Plugin.LogInfo($"UserIds set to {userIDs}");
            Plugin.Instance.UserIDs.Value = userIDs;
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
            Automatic,
            Custom,
        }

        public static Vector2 PlayerCountToDims(int count) =>
            count switch
            {
                1 or 2 or 3 or 4 => new Vector2(2, 2),
                5 or 6 => new Vector2(3, 2),
                7 or 8 => new Vector2(4, 2),
                9 or 10 => new Vector2(5, 2),
                11 or 12 => new Vector2(4, 3),
                13 or 14 or 15 => new Vector2(5, 3),
                16 => new Vector2(4, 4),
                17 or 18 or 19 or 20 => new Vector2(5, 4),
                21 or 22 or 23 or 24 or 25 => new Vector2(5, 5),
                26 or 27 or 28 or 29 or 30 => new Vector2(6, 5),
                31 or 32 or 33 or 34 or 35 or 36 => new Vector2(6, 6),
                37 or 38 or 39 or 40 or 41 or 42 => new Vector2(7, 6),
                43 or 44 or 45 or 46 or 47 or 48 or 49 => new Vector2(7, 7),
                _ => new Vector2(10, 10)
            };
    }
}
