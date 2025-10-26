using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using TMPro;
using TootTallyCore.APIServices;
using TootTallyCore.Graphics;
using TootTallyCore.Graphics.Animations;
using TootTallyCore.Utils.Assets;
using TootTallyCore.Utils.Helpers;
using TootTallyCore.Utils.TootTallyGlobals;
using TootTallyCustomCursor;
using TootTallyMultiplayer;
using TootTallySpectator;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Video;
using static TootTallySpectator.SpectatingManager;

namespace TootTallyTournamentHost
{
    public class TournamentGameplayController : MonoBehaviour
    {

        //Base Vars
        private GameController _gcInstance;
        private GameObject _container, _UIHolder, _gameplayContainer, _notesHolder;
        private RectTransform _notesHolderRect, _gameplayContainerRect;
        private Canvas _canvas, _containerCanvas;
        private Camera _gameCam, _bgCam;
        private VideoPlayer _videoPlayer, _savedVideoPlayer;
        private Rect _bounds;

        //Notes Vars
        private int _noteCount;
        private TournamentHostNoteStructure[] _notesArray;

        //Game Modifiers Vars
        private GameObject _gameModifierContainer;
        private string _gameModifiers;
        private Vector2 _flPos;
        private GameObject _flObject;

        //Pointer Vars
        private GameObject _pointer;
        private Vector2 _pointerPos;
        private RectTransform _pointerRect;
        private CanvasGroup _pointerGlowCanvasGroup;

        //Trombone Vars
        private float _currentVolume;
        private float _noteStartPosition;
        private AudioSource _currentNoteSound;
        private AudioClip[] _tClips;

        //Note Particles Vars
        private GameObject _noteParticles;
        private GameController.noteendeffect[] _allNoteEndEffects;
        private TournamentHostNoteEndAnimation[] _allNoteEndAnimations;
        private int _noteParticlesIndex;

        //Champ Vars
        private GameObject _champObject;
        private float _currentHealth;
        private ChampGUIController _champGUIController; //Probably have to make my own TournamentChampGUIController but for now that'll do it

        //Highest Combo vars
        private TournamentHostHighestCombo _highestComboController;

        //TimeElapsed Vars
        private TournamentHostTimeElapse _timeElapsedController;

        //Username Vars
        private TMP_Text _userName;

        //Replay Vars
        private SpectatingSystem _spectatingSystem;
        private List<SocketFrameData> _frameData;
        private List<SocketTootData> _tootData;
        private List<SocketNoteData> _noteData;
        private int _frameIndex, _tootIndex;
        private SocketFrameData _lastFrame, _currentFrame;
        private SocketTootData _currentTootData;
        private SocketNoteData _currentNoteData;
        private bool _hasSentSecondFlag, _hasSentFirstFlag;
        private int _id;
        private int _comboCounter;

        //Scoring
        private int _currentScore; //Current means the one displayed. There's a smoothing from Current to Total.
        private int _totalScore;
        private bool _champMode;
        private bool _releaseBetweenNotes;
        private int _multiplier, _highestCombo;
        private float _noteScoreAverage;

        //Others
        public bool IsReady => _isFiller || _id == 0 || (_frameData != null && _frameData.Count > 0 && _frameData.Last().time > 1f);
        private bool _isTooting, _isFiller, _initCompleted = false;

        //CONST
        private readonly string[] _PERF_TO_STRING = { "X", "MEH", "OK", "x", "x" };
        private readonly Vector2 _COMBO_TEXT_ROT = new Vector3(0, 0, -40f);
        private readonly Vector2 _COMBO_TEXT_POS = new Vector3(15f, 15f, 0);
        private readonly Vector2 _COMBO_TEXT_POS_OFFSET = new Vector3(0, -35f, 0);

        #region inits
        public void Initialize(GameController gcInstance, Camera gameCam, Camera bgCam, Rect bounds, Transform canvasTransform, SpectatingSystem spectatingSystem, int id)
        {
            _hasSentSecondFlag = _hasSentFirstFlag = false;
            _gcInstance = gcInstance;
            _gameCam = gameCam;
            _gameCam.name = $"GameplayCam{id}";
            _bgCam = bgCam;
            _bounds = bounds;
            gameCam.pixelRect = bounds;
            bgCam.pixelRect = bounds;
            _id = id;
            _isFiller = id < 0;
            _spectatingSystem = spectatingSystem;
            if (_spectatingSystem != null)
                _spectatingSystem.OnWebSocketOpenCallback = OnSpectatingConnect;

            InitContainer(canvasTransform);

            InitUIHolder();

            InitNoteHolder();

            InitNotes();

            if (Plugin.Instance.EnableNoteParticles.Value)
                InitNoteParticles();

            if (Plugin.Instance.EnableChampMeter.Value)
                InitChamp();

            InitPointer();

            InitReplayVariables();

            if (Plugin.Instance.EnableTimeElapsed.Value)
                InitTimeElapsed();

            if (Plugin.Instance.EnableHighestCombo.Value)
                InitHighestCombo();

            if (_gcInstance.bgcontroller.fullbgobject.transform.GetChild(0).GetChild(0).TryGetComponent(out VideoPlayer vp))
                InitVideoPlayer(vp);

            if (Plugin.Instance.EnableUsername.Value && _id > 0)
                InitUsername();

            if (_isFiller)
                HideEverything();

            _initCompleted = true;
        }

        private void HideEverything()
        {
            _UIHolder?.SetActive(false);
            _gameplayContainer.SetActive(false);
            //_noteParticles?.SetActive(false);
            //_notesHolder?.SetActive(false);
            //_pointer?.SetActive(false);
            _champObject?.SetActive(false);
            _timeElapsedController?.Hide();
            _highestComboController?.Hide();
            _userName?.gameObject.SetActive(false);
            _gameCam.enabled = false;
            _bgCam.enabled = false;
        }

        private void InitContainer(Transform canvasTransform)
        {
            _container = new GameObject($"Container{_id}");
            _container.transform.SetParent(canvasTransform);
            _container.AddComponent<RectTransform>();
            _gameplayContainer = GameObject.Instantiate(_container, _container.transform);
            _gameplayContainerRect = _gameplayContainer.GetComponent<RectTransform>();
            _gameplayContainerRect.anchorMin = _gameplayContainerRect.anchorMax = new Vector2(0, .5f);
            _gameplayContainerRect.sizeDelta = Vector2.zero;

            _containerCanvas = _container.AddComponent<Canvas>();
            _containerCanvas.worldCamera = _gameCam;
            _containerCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            /*CanvasScaler scaler = _containerCanvas.gameObject.AddComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.scaleFactor = 2f;
            _containerCanvas.scaleFactor = 1f;*/

            _gameCam.transform.SetParent(_container.transform);
            //var containerMask = _container.AddComponent<Mask>();
        }

        private void InitNoteParticles()
        {
            _noteParticles = GameObject.Instantiate(_gcInstance.noteparticles, _gameplayContainer.transform);
            _allNoteEndEffects = new GameController.noteendeffect[15];
            _allNoteEndAnimations = new TournamentHostNoteEndAnimation[15];
            SetAllNoteEndEffects();
            _noteParticlesIndex = 0;
        }

        private void InitUIHolder()
        {
            _UIHolder = GameObject.Instantiate(_gcInstance.ui_score_shadow.transform.parent.parent.gameObject, _container.transform);
            GameObjectFactory.DestroyFromParent(_UIHolder, "time_elapsed");
            GameObjectFactory.DestroyFromParent(_UIHolder, "maxcombo");
            GameObjectFactory.DestroyFromParent(_UIHolder, "flag-PracticeMode");
            GameObjectFactory.DestroyFromParent(_UIHolder, "flag-TurboMode");
            GameObjectFactory.DestroyFromParent(_UIHolder, "time_elapsed_bar");
            RemovePercentCounterIfFound();
        }

        private void InitNoteHolder()
        {
            _noteCount = TrombLoader.Plugin.Instance.beatsToShow.Value;
            _notesHolder = GameObject.Instantiate(_gcInstance.noteholder, _gameplayContainer.transform);
            _notesHolder.SetActive(true);
            DestroyImmediate(_notesHolder.transform.GetChild(0).gameObject);
            _notesHolderRect = _notesHolder.GetComponent<RectTransform>();
        }

        private void InitNotes()
        {
            _flipScheme = false;
            _notesArray = new TournamentHostNoteStructure[_noteCount];
            for (int i = 0; i < _noteCount; i++)
                _notesArray[i] = new TournamentHostNoteStructure(_notesHolder.transform.GetChild(i).gameObject);
        }
        private void RemovePercentCounterIfFound()
        {
            try
            {
                DestroyImmediate(_UIHolder.transform.Find("upper_right/ScoreShadow(Clone)").GetComponent("PercentCounter"));
            }
            catch (Exception e)
            {
                Plugin.LogInfo("PercentCounterNotFound");
            }
        }

        private void InitChamp()
        {
            _gcInstance.champcontroller.gameObject.SetActive(false);
            //Probably better to rewrite this in some way
            _champGUIController = GameObject.Instantiate(_gcInstance.champcontroller, _container.transform);
            _champObject = _champGUIController.gameObject;
            _champObject.SetActive(true);
            _champObject.transform.position = new Vector3(_champObject.transform.position.x, _champObject.transform.position.y, 0);
            //_champPanel.transform.localScale = Vector3.one * .75f;

            for (int i = 0; i < _champGUIController.letters.Length; i++)
                _champGUIController.letters[i] = _champObject.transform.GetChild(i + 1).gameObject;
            for (int i = 0; i < _champGUIController.champlvl.Length; i += 2)
            {
                _champGUIController.champlvl[i] = _champGUIController.letters[i / 2].transform.GetChild(0).GetChild(0).gameObject;
                _champGUIController.champlvl[i + 1] = _champGUIController.letters[i / 2].transform.GetChild(0).GetChild(1).gameObject;
            }
        }

        private void InitPointer()
        {
            var bar = GameObject.Instantiate(_gcInstance.leftbounds, _gameplayContainer.transform);
            _pointer = GameObject.Instantiate(_gcInstance.pointer, _gameplayContainer.transform);
            _pointerRect = _pointer.GetComponent<RectTransform>();
            _pointerPos = _pointerRect.anchoredPosition;
            _pointerGlowCanvasGroup = _pointer.transform.Find("note-dot-glow").GetComponent<CanvasGroup>();
            //_pointerRect.pivot = new Vector2(.91f, .5f);
        }

        private void InitReplayVariables()
        {
            _frameData = new List<SocketFrameData>();
            _tootData = new List<SocketTootData>();
            _noteData = new List<SocketNoteData>();
            _frameIndex = 0;
            _tootIndex = 0;
            _lastFrame.time = -1;
            _currentNoteData.noteID = -1;
            _comboCounter = 0;
            _currentFrame = new SocketFrameData() { time = -1, noteHolder = 0, pointerPosition = 0 };
            _currentTootData = new SocketTootData() { time = -1, isTooting = false, noteHolder = 0 };
            _isTooting = false;
        }

        private void SetAllNoteEndEffects()
        {
            for (int i = 0; i < 15; i++)
            {
                GameObject gameObject = _noteParticles.transform.GetChild(i).gameObject;
                GameController.noteendeffect noteendeffect = default;
                noteendeffect.noteeffect_obj = gameObject;
                noteendeffect.noteeffect_rect = gameObject.transform.GetComponent<RectTransform>();
                noteendeffect.burst_obj = gameObject.transform.GetChild(0).gameObject;
                noteendeffect.burst_img = gameObject.transform.GetChild(0).GetComponent<Image>();
                noteendeffect.burst_canvasg = gameObject.transform.GetChild(0).GetComponent<CanvasGroup>();
                noteendeffect.drops_obj = gameObject.transform.GetChild(1).gameObject;
                noteendeffect.drops_canvasg = gameObject.transform.GetChild(1).GetComponent<CanvasGroup>();
                noteendeffect.combotext_obj = gameObject.transform.GetChild(2).gameObject;
                noteendeffect.combotext_rect = gameObject.transform.GetChild(2).GetComponent<RectTransform>();
                noteendeffect.combotext_txt_front = gameObject.transform.GetChild(2).GetChild(0).GetComponent<Text>();
                noteendeffect.combotext_txt_shadow = gameObject.transform.GetChild(2).GetComponent<Text>();
                _allNoteEndEffects[i] = noteendeffect;
                _allNoteEndAnimations[i] = new TournamentHostNoteEndAnimation();
            }
        }

        public void InitModifiers()
        {
            if (_gameModifierContainer != null) return;
            _gameModifierContainer = new GameObject("GameModifierContainer", typeof(RectTransform));
            _gameModifierContainer.transform.SetParent(_UIHolder.transform);
            var rect = _gameModifierContainer.GetComponent<RectTransform>();
            rect.pivot = rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition3D = Vector2.zero;
            rect.localScale = Vector3.one * .6f;
            var layoutGroup = _gameModifierContainer.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.MiddleLeft;
            layoutGroup.childControlHeight = layoutGroup.childControlWidth =
            layoutGroup.childForceExpandHeight = layoutGroup.childForceExpandWidth =
            layoutGroup.childScaleHeight = layoutGroup.childScaleWidth = false;
            layoutGroup.spacing = 16f;
            layoutGroup.padding = new RectOffset(82, 0, 0, 32);

            if (_gameModifiers == null || _gameModifiers == "") return;

            foreach (string modifier in _gameModifiers.Split(','))
                AddModifierIcon(modifier);

            //Doing it this way to make sure the modifiers are always in the same order -_-
            if (_gameModifiers.Contains("FL"))
                InitFlashLight();
            if (_gameModifiers.Contains("HD"))
                InitHidden();
            if (_gameModifiers.Contains("MR"))
                InitMirror();
            if (_gameModifiers.Contains("HC"))
                InitHiddenCursor();
        }

        public void AddModifierIcon(string modifier)
        {
            if (_gameModifierContainer == null) return;
            var iconHolder = new GameObject($"{modifier}IconHolder");
            iconHolder.transform.SetParent(_gameModifierContainer.transform);
            var iconHolderRect = iconHolder.AddComponent<RectTransform>();
            iconHolderRect.anchorMin = iconHolderRect.anchorMax = iconHolderRect.pivot = Vector2.zero;
            iconHolderRect.anchoredPosition3D = Vector2.zero;
            iconHolderRect.sizeDelta = Vector2.one * 16f;
            iconHolderRect.localScale = Vector3.one;
            var icon = GameObjectFactory.CreateImageHolder(iconHolder.transform, Vector2.zero, Vector2.one * 64f, AssetManager.GetSprite($"{modifier}.png"), $"{modifier}Icon");
            icon.GetComponent<Image>().color = new Color(1, 1, 1, .4f);
            icon.GetComponent<RectTransform>().anchoredPosition3D = Vector3.zero;

            //var rect = icon.GetComponent<RectTransform>();
            /*rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;*/
        }

        public void InitFlashLight()
        {
            _flObject = GameObjectFactory.CreateImageHolder(_pointer.transform, Vector2.zero, Vector2.one * 500f, AssetManager.GetSprite("THFL.png"), "FL");
            var rect = _flObject.GetComponent<RectTransform>();
            rect.anchoredPosition3D = Vector3.zero;
            var topBox = GameObject.Instantiate(_flObject, _flObject.transform);
            GameObject.DestroyImmediate(topBox.GetComponent<Image>());
            var img = topBox.AddComponent<Image>();
            img.color = Color.black;
            var tbRect = topBox.GetComponent<RectTransform>();
            tbRect.anchorMin = tbRect.anchorMax = new Vector2(.5f, 1);
            tbRect.pivot = new Vector2(.5f, 0);
            tbRect.sizeDelta = new Vector2(Screen.width * 1.5f, Screen.height * 1.5f);
            var botBox = GameObject.Instantiate(topBox, _flObject.transform);
            var bbRect = botBox.GetComponent<RectTransform>();
            bbRect.anchorMin = bbRect.anchorMax = new Vector2(.5f, 0);
            bbRect.pivot = new Vector2(.5f, 1);
            var rigBox = GameObject.Instantiate(topBox, _flObject.transform);
            var rbBox = rigBox.GetComponent<RectTransform>();
            rbBox.anchorMin = rbBox.anchorMax = new Vector2(1, .5f);
            rbBox.pivot = new Vector2(0, .5f);
        }

        public void InitHidden()
        {

        }

        public void InitMirror()
        {
            _gameplayContainerRect.localScale = new Vector3(1, -1, 1);
            foreach (var fx in _allNoteEndEffects)
                fx.noteeffect_rect.localScale = new Vector3(1, -1, 1);
        }

        public void InitHiddenCursor()
        {
            _pointer.SetActive(false);
        }

        public void InitTimeElapsed()
        {
            _timeElapsedController = new TournamentHostTimeElapse(_gcInstance, _UIHolder.transform);
        }

        public void InitHighestCombo()
        {
            _highestComboController = new TournamentHostHighestCombo(_gcInstance, _UIHolder.transform);
        }

        public void InitVideoPlayer(VideoPlayer vp)
        {
            if (_isFiller) return;

            _videoPlayer = _bgCam.gameObject.AddComponent<VideoPlayer>();
            _videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
            _videoPlayer.targetCamera = _bgCam;
            _videoPlayer.playOnAwake = false;
            _videoPlayer.source = VideoSource.Url;
            _videoPlayer.url = vp.url;
            _videoPlayer.Prepare();
            //_savedVideoPlayer = vp;
            //_videoPlayer.clip = vp.clip;
        }

        public void InitUsername()
        {
            _userName = GameObjectFactory.CreateSingleText(_UIHolder.transform, "Username", "-");
            _userName.rectTransform.pivot = new Vector2(1, 0);
            _userName.rectTransform.anchorMax = _userName.rectTransform.anchorMin = new Vector2(.95f, 0);
            _userName.alignment = TextAlignmentOptions.BottomRight;
            _userName.enableWordWrapping = false;
            _userName.fontSize = 28f;
            Plugin.Instance.StartCoroutine(TootTallyAPIService.GetUserFromID(_id, user => _userName.text = user.username));
        }

        #endregion
        private void OnSpectatingConnect(SpectatingSystem sender)
        {
            _spectatingSystem.OnSocketSongInfoReceived = OnSongInfoReceived;
            _spectatingSystem.OnSocketUserStateReceived = OnUserStateReceived;
            _spectatingSystem.OnSocketFrameDataReceived = OnFrameDataReceived;
            _spectatingSystem.OnSocketTootDataReceived = OnTootDataReceived;
            _spectatingSystem.OnSocketNoteDataReceived = OnNoteDataReceived;
        }

        public void StartVideoPlayer()
        {
            _videoPlayer?.Play();
        }

        public void PauseVideoPlayer()
        {
            _videoPlayer?.Pause();
        }

        private void Update()
        {
            if (!_initCompleted || _isFiller) return;

            /*if (_savedVideoPlayer != null && _savedVideoPlayer.clip != null)
            {
                _videoPlayer.url 
                _videoPlayer.clip = _savedVideoPlayer.clip;
                _savedVideoPlayer = null;
            }*/

            UpdateTimeCounter();
            UpdateNotesPosition();
            if (_spectatingSystem == null || !_spectatingSystem.IsConnected)
            {
                if (_isTooting)
                {
                    _isTooting = false;
                    HandlePitchShift();
                }
                return;
            }
            _spectatingSystem?.UpdateStacks();
            HandlePitchShift();
            PlaybackSpectatingData();
        }

        public void Disconnect() => _spectatingSystem?.Disconnect();

        public void OnGetScoreAverage()
        {
            if (!_initCompleted) return;

            if (!_hasSentSecondFlag && MultiplayerManager.IsPlayingMultiplayer)
            {
                MultiplayerManager.GetMultiplayerController.SendQuickChat(42069);
                _hasSentSecondFlag = true;
            }

            if (_noteData != null && _noteData.Count > 0 && _noteData.Last().noteID > _gcInstance.currentnoteindex)
                _currentNoteData = _noteData.Find(x => x.noteID == _gcInstance.currentnoteindex);
            if (_currentNoteData.noteID != -1)
            {
                _champMode = _currentNoteData.champMode;
                _multiplier = _currentNoteData.multiplier;
                _noteScoreAverage = (float)_currentNoteData.noteScoreAverage;
                _releaseBetweenNotes = _currentNoteData.releasedButtonBetweenNotes;
                _totalScore = _currentNoteData.totalScore;
                _currentHealth = _currentNoteData.health;
                _highestCombo = _currentNoteData.highestCombo;
                _currentNoteData.noteID = -1;
            }
            GetScoreAverage();
        }

        private void OnSongInfoReceived(SocketSongInfo songInfo)
        {
            _gameModifiers = songInfo.gamemodifiers;
            Plugin.LogInfo($"Modifier for {_id}: {_gameModifiers}");
            InitModifiers();
        }

        private void OnUserStateReceived(SocketUserState stateData)
        {

        }

        private void OnFrameDataReceived(SocketFrameData frameData)
        {
            _frameData.Add(frameData);
        }

        private void OnTootDataReceived(SocketTootData tootData)
        {
            _tootData.Add(tootData);
        }

        private void OnNoteDataReceived(SocketNoteData noteData)
        {
            if (!_initCompleted) return;

            if (!_hasSentFirstFlag && MultiplayerManager.IsPlayingMultiplayer)
            {
                MultiplayerManager.GetMultiplayerController.SendQuickChat(6969);
                _hasSentFirstFlag = true;
            }

            _noteData.Add(noteData);
        }

        public void PlaybackSpectatingData()
        {
            if (_frameData == null || _tootData == null || !_initCompleted) return;

            var currentMapPosition = _gcInstance.musictrack.time;

            if (_frameData.Count > 0)
                PlaybackFrameData(currentMapPosition);

            if (_tootData.Count > 0)
                PlaybackTootData(currentMapPosition);

            if (_frameData.Count > _frameIndex)
                InterpolateCursorPosition(currentMapPosition);
        }

        private void InterpolateCursorPosition(float currentMapPosition)
        {
            if (_currentFrame.time - _lastFrame.time > 0)
            {
                var newCursorPosition = EasingHelper.Lerp(_lastFrame.pointerPosition, _currentFrame.pointerPosition, (float)((currentMapPosition - _lastFrame.time) / (_currentFrame.time - _lastFrame.time)));
                SetCursorPosition(newCursorPosition);
            }
            else
                SetCursorPosition(_currentFrame.pointerPosition);
        }

        private void PlaybackFrameData(float currentMapPosition)
        {
            if (_lastFrame.time != _currentFrame.time && currentMapPosition >= _currentFrame.time)
                _lastFrame = _currentFrame;

            if (_frameData != null && _frameData.Count > _frameIndex && (_currentFrame.time == -1 || currentMapPosition >= _currentFrame.time))
            {
                _frameIndex = _frameData.FindIndex(_frameIndex > 1 ? _frameIndex - 1 : 0, x => currentMapPosition < x.time);
                if (_frameIndex != -1 && _frameData.Count > _frameIndex)
                    _currentFrame = _frameData[_frameIndex];
            }
        }

        public void PlaybackTootData(float currentMapPosition)
        {
            if (currentMapPosition >= _currentTootData.time && _isTooting != _currentTootData.isTooting)
            {
                _isTooting = _currentTootData.isTooting;
                if (_isTooting)
                {
                    _currentNoteSound.time = 0f;
                    _currentNoteSound.volume = _currentVolume = 1f;
                    PlayNote();
                    LeanTween.alphaCanvas(_pointerGlowCanvasGroup, 0.95f, 0.05f);
                }
                else
                {
                    LeanTween.alphaCanvas(_pointerGlowCanvasGroup, 0f, 0.05f);
                }
            }

            if (_tootData != null && _tootData.Count > _tootIndex && currentMapPosition >= _currentTootData.time)
                _currentTootData = _tootData[_tootIndex++];


        }

        private void SetCursorPosition(float newPosition)
        {
            if (!_initCompleted) return;
            _pointerPos.y = newPosition;
            _pointerRect.anchoredPosition = _pointerPos;
        }

        public void CopyAllAudioClips()
        {
            _currentNoteSound = GameObject.Instantiate(_gcInstance.currentnotesound);
            _tClips = _currentNoteSound.gameObject.transform.GetChild(0).gameObject.GetComponent<AudioClipsTromb>().tclips;
        }

        private void PlayNote()
        {
            if (!_initCompleted) return;

            float num = 9999f;
            int num2 = 0;
            for (int i = 0; i < 15; i++)
            {
                float num3 = Mathf.Abs(_gcInstance.notelinepos[i] - _pointer.GetComponent<RectTransform>().anchoredPosition.y);
                if (num3 < num)
                {
                    num = num3;
                    num2 = i;
                }
            }
            _noteStartPosition = _gcInstance.notelinepos[num2];
            _currentNoteSound.clip = _tClips[Mathf.Abs(num2 - 14)];
            _currentNoteSound.Play();
        }

        private void StopNote()
        {
            _currentNoteSound.Stop();
        }

        private void HandlePitchShift()
        {
            if (_tClips == null || _currentNoteSound == null || TournamentHostManager.isWaitingForSync || !_initCompleted) return;

            var pointerPos = _pointer.GetComponent<RectTransform>().anchoredPosition.y;

            if (!_isTooting)
            {
                if (_currentVolume < 0f)
                    _currentVolume = 0f;
                else if (_currentVolume > 0f)
                    _currentVolume -= Time.deltaTime * 18f;

                _currentNoteSound.volume = _currentVolume;
            }
            else
            {
                if (_currentNoteSound.time > _currentNoteSound.clip.length - 1.25f)
                    _currentNoteSound.time = 1f;

                float num11 = Mathf.Pow(_noteStartPosition - pointerPos, 2f) * 6.8E-06f;
                float num12 = (_noteStartPosition - pointerPos) * (1f + num11);
                if (num12 > 0f)
                {
                    num12 = (_noteStartPosition - pointerPos) * 1.392f;
                    num12 *= 0.5f;
                }
                float num13 = 1f - num12 * 0.00501f;
                if (num13 > 2f)
                    num13 = 2f;
                else if (num13 < 0.5f)
                    num13 = 0.5f;

                _currentNoteSound.pitch = num13;
            }
        }

        public void UpdateTimeCounter()
        {
            if (_timeElapsedController == null) return;
            _timeElapsedController.UpdateTimeBar(_gcInstance.musictrack.time);
            _timeElapsedController.UpdateTimeText(_gcInstance.timeelapsed_shad.text);
        }

        public void UpdateNotesPosition()
        {
            if (_notesHolder == null) return;
            _notesHolderRect.anchoredPosition = _gcInstance.noteholderr.anchoredPosition;
        }

        private void GetScoreAverage()
        {
            UpdateChampMeter();
            var textID = _noteScoreAverage > 95f ? 4 :
                _noteScoreAverage > 88f ? 3 :
                _noteScoreAverage > 79f ? 2 :
                _noteScoreAverage > 70f ? 1 : 0;
            _comboCounter = textID >= 3 ? _comboCounter + 1 : 0;
            AnimateNoteEndEffect(_gcInstance.currentnoteindex, textID);
            _highestComboController?.UpdateHighestCombo(_comboCounter);
        }

        private void UpdateChampMeter()
        {
            if (_champGUIController == null) return;

            int num = Mathf.FloorToInt(_currentHealth * 0.1f) - _champGUIController.healthcounter;
            for (int i = 0; i < Mathf.Abs(num); i++)
            {
                if (num > 0)
                    _champGUIController.advanceCounter(1);
                else if (num < 0)
                    _champGUIController.advanceCounter(-1);
            }
        }

        private const int SLIDER_SAMPLE_COUNT = 12;
        private bool _flipScheme;
        public void BuildSingleNote(int index)
        {
            if (_isFiller) return;
            float[] previousNoteData = new float[]
            {
                    9999f,
                    9999f,
                    9999f,
                    0f,
                    9999f
            };
            if (index > 0)
                previousNoteData = _gcInstance.leveldata[index - 1];

            float[] noteData = _gcInstance.leveldata[index];
            bool previousNoteIsSlider = Mathf.Abs(previousNoteData[0] + previousNoteData[1] - noteData[0]) <= 0.02f;
            bool isTapNote = noteData[1] <= 0.0625f && _gcInstance.tempo > 50f && noteData[3] == 0f && !previousNoteIsSlider;
            if (noteData[1] <= 0f)
            {
                noteData[1] = 0.015f;
                _gcInstance.leveldata[index][1] = 0.015f;
            }
            TournamentHostNoteStructure currentNote = _notesArray[index % _noteCount];
            TournamentHostNoteStructure previousNote = _notesArray[(index - 1) % _noteCount];
            currentNote.CancelAnimation();
            currentNote.root.transform.localScale = Vector3.one;
            currentNote.root.SetActive(true);
            _flipScheme = previousNoteIsSlider && !_flipScheme;

            currentNote.SetColorScheme(_gcInstance.note_c_start, _gcInstance.note_c_end, _flipScheme);
            //currentNote.noteDesigner.enabled = false;

            if (index > 0)
                previousNote.noteEnd.gameObject.SetActive(!previousNoteIsSlider || index - 1 >= _gcInstance.leveldata.Count); //End of previous note
            currentNote.noteEnd.SetActive(!isTapNote);
            currentNote.noteStart.SetActive(!previousNoteIsSlider);

            currentNote.noteRect.anchoredPosition3D = new Vector3(noteData[0] * _gcInstance.defaultnotelength, noteData[2], 0f);
            currentNote.noteEndRect.localScale = isTapNote ? Vector2.zero : Vector3.one;

            currentNote.noteEndRect.anchoredPosition3D = new Vector3(_gcInstance.defaultnotelength * noteData[1] - _gcInstance.levelnotesize + 11.5f, noteData[3], 0f);
            if (!isTapNote)
            {
                if (index >= TrombLoader.Plugin.Instance.beatsToShow.Value)
                {
                    currentNote.noteEndRect.anchorMin = currentNote.noteEndRect.anchorMax = new Vector2(1, .5f);
                    currentNote.noteEndRect.pivot = new Vector2(0.34f, 0.5f);
                }
            }
            float[] noteVal = new float[]
            {
                    noteData[0] * _gcInstance.defaultnotelength,
                    noteData[0] * _gcInstance.defaultnotelength + _gcInstance.defaultnotelength * noteData[1],
                    noteData[2],
                    noteData[3],
                    noteData[4]
            };
            //__instance.allnotevals.Add(noteVal);
            float noteLength = _gcInstance.defaultnotelength * noteData[1];
            float pitchDelta = noteData[3];
            foreach (LineRenderer lineRenderer in currentNote.lineRenderers)
            {
                lineRenderer.gameObject.SetActive(!isTapNote);
                if (isTapNote) continue;
                if (pitchDelta == 0f)
                {
                    lineRenderer.positionCount = 2;
                    lineRenderer.SetPosition(0, new Vector3(-3f, 0f, 0f));
                    lineRenderer.SetPosition(1, new Vector3(noteLength, 0f, 0f));
                }
                else
                {
                    lineRenderer.positionCount = SLIDER_SAMPLE_COUNT;
                    lineRenderer.SetPosition(0, new Vector3(-3f, 0f, 0f));
                    for (int k = 1; k < SLIDER_SAMPLE_COUNT; k++)
                    {
                        lineRenderer.SetPosition(k,
                        new Vector3(
                        noteLength / (SLIDER_SAMPLE_COUNT - 1) * k,
                        _gcInstance.easeInOutVal(k, 0f, pitchDelta, SLIDER_SAMPLE_COUNT - 1),
                        0f));
                    }
                }
            }
        }

        private void AnimateNoteEndEffect(int noteindex, int performance)
        {
            if (_noteParticles == null || _allNoteEndEffects == null || _allNoteEndAnimations == null) return;
            if (_notesArray != null && _notesArray.Length < noteindex % _noteCount)
                _notesArray[noteindex % _noteCount].AnimateNoteOut();
            GameController.noteendeffect noteendeffect = _allNoteEndEffects[_noteParticlesIndex];
            TournamentHostNoteEndAnimation anim = _allNoteEndAnimations[_noteParticlesIndex];

            anim.CancelAllAnimations();
            noteendeffect.noteeffect_obj.SetActive(true);
            if (_gcInstance.leveldata != null && _gcInstance.leveldata.Count >= noteindex)
                noteendeffect.noteeffect_rect.anchoredPosition3D = new Vector3(0f, _gcInstance.leveldata[noteindex][4], 0f);
            noteendeffect.burst_obj.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            noteendeffect.combotext_obj.transform.localScale = Vector3.one;
            noteendeffect.drops_canvasg.alpha = 0.85f;
            noteendeffect.drops_obj.transform.localScale = new Vector3(0.1f, 0.1f, 1f);

            noteendeffect.burst_img.color = noteendeffect.combotext_txt_front.color = performance == 0 || !_releaseBetweenNotes ? Color.red : Color.white;

            if (performance >= 3)
            {
                noteendeffect.combotext_rect.localEulerAngles = _COMBO_TEXT_ROT;
                noteendeffect.combotext_rect.anchoredPosition3D = _COMBO_TEXT_POS;
                noteendeffect.combotext_txt_shadow.text = noteendeffect.combotext_txt_front.text = _comboCounter.ToString() + "x";
                noteendeffect.burst_img.sprite = _gcInstance.noteparticle_images[1];
                noteendeffect.burst_canvasg.alpha = 0.7f;
            }
            else
            {
                noteendeffect.combotext_rect.localEulerAngles = -_COMBO_TEXT_ROT;
                noteendeffect.combotext_rect.anchoredPosition3D = _COMBO_TEXT_POS + _COMBO_TEXT_POS_OFFSET;
                noteendeffect.combotext_txt_shadow.text = noteendeffect.combotext_txt_front.text = _PERF_TO_STRING[performance];
                noteendeffect.burst_img.sprite = _gcInstance.noteparticle_images[0];
                noteendeffect.burst_canvasg.alpha = 0.4f;
            }
            anim.StartAnimateOutAnimation(noteendeffect.burst_obj, noteendeffect.burst_canvasg.gameObject, noteendeffect.drops_obj, noteendeffect.drops_canvasg.gameObject, noteendeffect.combotext_obj, _multiplier);
            _noteParticlesIndex++;
            if (_noteParticlesIndex > 14)
                _noteParticlesIndex = 0;
        }
    }
}
