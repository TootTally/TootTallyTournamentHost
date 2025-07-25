﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using TootTallyCore.Graphics;
using TootTallyCore.Graphics.Animations;
using TootTallyCore.Utils.Helpers;
using TootTallyGameModifiers;
using TootTallyMultiplayer;
using TootTallySpectator;
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.UI;
using static TootTallySpectator.SpectatingManager;

namespace TootTallyTournamentHost
{
    public class TournamentGameplayController : MonoBehaviour
    {
        private GameController _gcInstance;
        private GameObject _container;
        private Camera _camera;
        private PostProcessingBehaviour _pppEffects;
        private Rect _bounds;
        private SpectatingSystem _spectatingSystem;
        private GameObject _pointer;
        private RectTransform _pointerRect;
        private CanvasGroup _pointerGlowCanvasGroup;
        private GameObject _noteParticles;
        private GameObject _UIHolder;
        private GameObject _champCanvas, _champPanel;
        private static bool _hasSentSecondFlag, _hasSentFirstFlag;
        //private PercentCounter _percentCounter;

        public bool IsReady => _frameData != null && _frameData.Count > 0 && _frameData.Last().time > 1f;

        private bool _isTooting;

        public void Initialize(GameController gcInstance, Camera camera, Rect bounds, Transform canvasTransform, SpectatingSystem spectatingSystem)
        {
            _hasSentSecondFlag = _hasSentFirstFlag = false;
            _gcInstance = gcInstance;
            _gcInstance.latency_offset = 0;
            _camera = camera;
            _pppEffects = _camera.GetComponent<PostProcessingBehaviour>();
            _bounds = bounds;
            camera.pixelRect = bounds;
            _spectatingSystem = spectatingSystem;
            _spectatingSystem.OnWebSocketOpenCallback = OnSpectatingConnect;

            _container = GameObject.Instantiate(new GameObject("Container"), canvasTransform.transform);
            _container.AddComponent<RectTransform>();

            _noteParticles = GameObject.Instantiate(_gcInstance.noteparticles, _container.transform);
            _allNoteEndEffects = new GameController.noteendeffect[15];
            _allNoteEndAnimations = new AnimateNoteEndEffectAnimations[15];
            SetAllNoteEndEffects();

            /*_maxStarObject = GameObject.Instantiate(_gcInstance.max_star_gameobject, _container.transform);
            _maxStarRect = _maxStarObject.GetComponent<RectTransform>();

            _noGapObject = GameObject.Instantiate(_gcInstance.no_gap_gameobject, _container.transform);
            _noGapRect = _noGapObject.GetComponent<RectTransform>();

            _popupTextShadow = GameObject.Instantiate(_gcInstance.popuptextshadow, _container.transform);
            _popupText = _popupTextShadow.transform.Find("txt_perfecto_popup-top").GetComponent<Text>();
            _popupTextObject = _popupTextShadow.gameObject;
            _popupTextRect = _popupTextObject.GetComponent<RectTransform>();
            _popupTextRect.anchorMin = _popupTextRect.anchorMax =
            _noGapRect.anchorMin = _noGapRect.anchorMax =
            _maxStarRect.anchorMin = _maxStarRect.anchorMax = new Vector2(.3f, .5f);

            _multiplierTextShadow = GameObject.Instantiate(_gcInstance.multtextshadow, _container.transform);
            _multiplierText = _multiplierTextShadow.transform.Find("Text top").GetComponent<Text>();
            _multiplierTextObject = _multiplierTextShadow.gameObject;
            _multiplierTextRect = _multiplierTextObject.GetComponent<RectTransform>();*/

            _UIHolder = GameObject.Instantiate(_gcInstance.ui_score_shadow.transform.parent.parent.gameObject, _container.transform);

            //Probably better to rewrite this in some way
            _champCanvas = GameObject.Instantiate(_gcInstance.champcontroller.transform.parent, _container.transform).gameObject;
            _champGUIController = _champCanvas.transform.GetChild(0).GetComponent<ChampGUIController>();
            _champPanel = _champCanvas.transform.GetChild(1).gameObject;
            _champPanel.transform.SetParent(_container.transform);
            _champPanel.transform.localScale = Vector3.one * .75f;
            var champRect = _champPanel.GetComponent<RectTransform>();
            champRect.anchorMin = champRect.anchorMax = new Vector2(.06f, .9f);
            champRect.anchoredPosition = Vector2.zero;
            var champParent = _champPanel.transform.GetChild(0);
            var champParentRect = champParent.GetComponent<RectTransform>();
            champParentRect.anchorMin = champParentRect.anchorMax = Vector2.one / 2f;
            champParentRect.anchoredPosition = Vector2.zero;

            for (int i = 0; i < _champGUIController.letters.Length; i++)
                _champGUIController.letters[i] = _champPanel.transform.GetChild(0).GetChild(i).gameObject;
            for (int i = 0; i < _champGUIController.champlvl.Length; i += 2)
            {
                _champGUIController.champlvl[i] = _champGUIController.letters[i / 2].transform.GetChild(0).GetChild(0).gameObject;
                _champGUIController.champlvl[i + 1] = _champGUIController.letters[i / 2].transform.GetChild(0).GetChild(1).gameObject;
            }

            GameObjectFactory.DestroyFromParent(_UIHolder, "time_elapsed");
            GameObjectFactory.DestroyFromParent(_UIHolder, "PracticeMode");
            GameObjectFactory.DestroyFromParent(_UIHolder, "time_elapsed_bar");

            try
            {
                DestroyImmediate(_UIHolder.transform.Find("upper_right/ScoreShadow(Clone)").GetComponent("PercentCounter"));
            }
            catch (Exception e)
            {
                Plugin.LogInfo("PercentCounterNotFound");
            }

            _UIScoreShadow = _UIHolder.transform.Find("upper_right/ScoreShadow").GetComponent<Text>();
            _UIScore = _UIScoreShadow.transform.Find("Score").GetComponent<Text>();

            _highestComboTextShadow = _UIHolder.transform.Find("maxcombo/maxcombo_shadow").GetComponent<Text>();
            _highestComboText = _highestComboTextShadow.transform.Find("maxcombo_text").GetComponent<Text>();

            _noteParticlesIndex = 0;
            _multiHideTimer = -1f;

            _pointer = GameObject.Instantiate(gcInstance.pointer, _container.transform);
            _pointerRect = _pointer.GetComponent<RectTransform>();
            _pointerRect.pivot = new Vector2(.58f, .5f);
            _pointerGlowCanvasGroup = _pointer.transform.Find("note-dot-glow").GetComponent<CanvasGroup>();
            _frameIndex = 0;
            _tootIndex = 0;
            _lastFrame.time = -1;
            _currentNoteData.noteID = -1;
            _currentFrame = new SocketFrameData() { time = -1, noteHolder = 0, pointerPosition = 0 };
            _currentTootData = new SocketTootData() { time = -1, isTooting = false, noteHolder = 0 };
            _isTooting = false;
        }

        private VignetteModel.Settings _settings;
        private Vector2 _pointerPos;
        private Color _color;

        public void InitFlashLight()
        {
            _pppEffects.profile.vignette.enabled = true;
            _pointerPos = new Vector2(.075f, (_pointer.transform.localPosition.y + 215) / 430);
            _color = new Color(.5f, .5f, .5f, 1);
            _settings = new VignetteModel.Settings()
            {
                center = _pointerPos,
                color = _color,
                intensity = .8f,
                mode = VignetteModel.Mode.Classic,
                rounded = true,
                roundness = 1,
                smoothness = 1,
            };
        }

        public void UpdateFlashLight()
        {
            _pointerPos.y = (_pointer.transform.localPosition.y + 215) / 430;
            _settings.center = _pointerPos;
            _settings.color = _color;
            _pppEffects.profile.vignette.settings = _settings;
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
                _allNoteEndAnimations[i] = new AnimateNoteEndEffectAnimations();
            }
        }

        private void OnSpectatingConnect(SpectatingSystem sender)
        {
            _spectatingSystem.OnSocketUserStateReceived = OnUserStateReceived;
            _spectatingSystem.OnSocketFrameDataReceived = OnFrameDataReceived;
            _spectatingSystem.OnSocketTootDataReceived = OnTootDataReceived;
            _spectatingSystem.OnSocketNoteDataReceived = OnNoteDataReceived;
        }

        private void Update()
        {
            _spectatingSystem?.UpdateStacks();
            if (!_gcInstance.freeplay && !_gcInstance.quitting && !_gcInstance.level_finished)
            {
                HandlePitchShift();
                PlaybackSpectatingData(_gcInstance);
            }
            UpdateMultiHideTimer();
        }

        public void Disconnect() => _spectatingSystem?.Disconnect();

        public void OnGetScoreAverage()
        {
            if (!_hasSentSecondFlag)
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
                if (_highestcombo < _currentNoteData.highestCombo)
                    updateHighestCombo(_currentNoteData.highestCombo);
                _currentNoteData.noteID = -1;
            }
            getScoreAverage();
        }

        public void OnTallyScore()
        {
            tallyScore();
        }

        private List<SocketFrameData> _frameData = new List<SocketFrameData>();
        private List<SocketTootData> _tootData = new List<SocketTootData>();
        private List<SocketNoteData> _noteData = new List<SocketNoteData>();
        private int _frameIndex;
        private int _tootIndex;
        private SocketFrameData _lastFrame, _currentFrame;
        private SocketTootData _currentTootData;
        private SocketNoteData _currentNoteData;

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
            if (!_hasSentFirstFlag)
            {
                MultiplayerManager.GetMultiplayerController.SendQuickChat(6969);
                _hasSentFirstFlag = true;
            }

            _noteData.Add(noteData);
        }

        public void PlaybackSpectatingData(GameController __instance)
        {
            if (_frameData == null || _tootData == null) return;

            var currentMapPosition = __instance.musictrack.time;

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

            if (_frameData.Count > _frameIndex && (_currentFrame.time == -1 || currentMapPosition >= _currentFrame.time))
            {
                _frameIndex = _frameData.FindIndex(_frameIndex > 1 ? _frameIndex - 1 : 0, x => currentMapPosition < x.time);
                if (_frameData.Count > _frameIndex && _frameIndex != -1)
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

            if (_tootData.Count > _tootIndex && currentMapPosition >= _currentTootData.time)
                _currentTootData = _tootData[_tootIndex++];


        }

        private void SetCursorPosition(float newPosition)
        {
            _pointerRect.anchoredPosition = new Vector2(_pointerRect.sizeDelta.x, newPosition);
        }

        private float _currentVolume;
        private float _noteStartPosition;
        private AudioSource _currentNoteSound;
        private AudioClip[] _tClips;

        public void CopyAllAudioClips()
        {
            _currentNoteSound = GameObject.Instantiate(_gcInstance.currentnotesound);
            _tClips = _currentNoteSound.gameObject.transform.GetChild(0).gameObject.GetComponent<AudioClipsTromb>().tclips;
        }

        private void PlayNote()
        {
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

        private Text _highestComboText, _highestComboTextShadow;

        private void updateHighestCombo(int combo)
        {
            _highestcombo = combo;
            LeanTween.cancel(_highestComboTextShadow.gameObject);
            _highestComboTextShadow.gameObject.transform.localScale = new Vector3(1.5f, 1.2f, 1f);
            LeanTween.scale(_highestComboTextShadow.gameObject, new Vector3(1f, 1f, 1f), 0.1f).setEaseOutQuart();
            _highestComboText.text = "longest combo: <color=#ffff00>" + combo.ToString() + "</color>";
            _highestComboTextShadow.text = "longest combo: " + combo.ToString();
        }

        private int _currentScore;
        private Text _UIScore, _UIScoreShadow;

        private void tallyScore()
        {
            if (_currentScore < _totalScore)
            {
                _currentScore += 777;
                _UIScore.text = _currentScore.ToString("n0");
                _UIScoreShadow.text = _currentScore.ToString("n0");
            }
            if (_currentScore > _totalScore)
            {
                _currentScore = _totalScore;
                _UIScore.text = _currentScore.ToString("n0");
                _UIScoreShadow.text = _currentScore.ToString("n0");
            }
        }

        private void HandlePitchShift()
        {
            if (_tClips == null || _currentNoteSound == null) return;

            var pointerPos = _pointer.GetComponent<RectTransform>().anchoredPosition.y;

            if (!_isTooting)
            {
                if (_currentVolume < 0f)
                    _currentVolume = 0f;
                else if (_currentVolume > 0f)
                    _currentVolume -= Time.deltaTime * 18f;

                _currentNoteSound.volume = _currentVolume;
            }
            if (_isTooting)
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

        private GameController.noteendeffect[] _allNoteEndEffects;
        private AnimateNoteEndEffectAnimations[] _allNoteEndAnimations;
        //private GameObject _maxStarObject;
        //private RectTransform _maxStarRect;

        //private RectTransform _noGapRect;
        //private GameObject _noGapObject;

        //private GameObject _popupTextObject;
        //private RectTransform _popupTextRect;
        //private Text _popupText, _popupTextShadow;

        //private GameObject _multiplierTextObject;
        //private RectTransform _multiplierTextRect;
        //private Text _multiplierText, _multiplierTextShadow;

        private int _noteParticlesIndex;

        private int _totalScore;
        private bool _champMode;
        private bool _releaseBetweenNotes;
        private int _multiplier, _highestcombo;
        private const int MAX_MULTIPLIER = 10;

        private float _multiHideTimer;

        private void UpdateMultiHideTimer()
        {
            if (_multiHideTimer > -1f)
            {
                _multiHideTimer += 1f * Time.deltaTime;
                if (_multiHideTimer > 1.5f)
                {
                    _multiHideTimer = -1f;
                    hideMultText();
                }
            }
        }

        private float _noteScoreAverage;

        private void getScoreAverage()
        {
            if (!_releaseBetweenNotes)
                affectHealthBar(-15f);
            else
                affectHealthBar(Mathf.Clamp((_noteScoreAverage - 79f) * 0.2193f, -15f, 4.34f));
            if (_noteScoreAverage > 95f)
            {
                doScoreText(4);
                return;
            }
            if (_noteScoreAverage > 88f)
            {
                doScoreText(3);
                return;
            }
            if (_noteScoreAverage > 79f)
            {
                doScoreText(2);
                return;
            }
            if (_noteScoreAverage > 70f)
            {
                doScoreText(1);
                return;
            }
            doScoreText(0);
        }

        private float _currentHealth;
        private ChampGUIController _champGUIController; //Probably have to make my own TournamentChampGUIController

        private void affectHealthBar(float healthchange)
        {
            if (_currentHealth < 100f && _currentHealth + healthchange >= 100f)
                _gcInstance.playGoodSound();
            else if (_currentHealth >= 100f && _currentHealth + healthchange < 100f)
            {
                _currentHealth = 0f;
                _gcInstance.sfxrefs.slidedown.Play();
            }
            _currentHealth += healthchange;
            if (_currentHealth > 100f)
                _currentHealth = 100f;
            else if (_currentHealth < 0f)
                _currentHealth = 0f;
            int num = Mathf.FloorToInt(_currentHealth * 0.1f) - _champGUIController.healthcounter;
            for (int i = 0; i < Mathf.Abs(num); i++)
            {
                if (num > 0)
                    _champGUIController.advanceCounter(1);
                else if (num < 0)
                    _champGUIController.advanceCounter(-1);
            }
        }

        private void hideMultText()
        {
            /*LeanTween.cancel(_popupTextShadow.gameObject);
            LeanTween.cancel(_multiplierTextShadow.gameObject);
            LeanTween.scale(_popupTextShadow.gameObject, new Vector3(0f, 1f, 1f), 0.09f).setEaseInQuart();
            LeanTween.scale(_multiplierTextShadow.gameObject, new Vector3(0f, 1f, 1f), 0.09f).setEaseInQuart();
            if (_maxStarObject.transform.localScale.x > 0.1f)
            {
                LeanTween.cancel(_maxStarObject);
                LeanTween.scale(_maxStarObject, new Vector3(0f, 0.25f, 1f), 0.09f).setEaseInQuart();
            }
            if (_noGapObject.transform.localScale.x > 0.1f)
            {
                LeanTween.cancel(_noGapObject);
                LeanTween.scale(_noGapObject, new Vector3(0f, 0.25f, 1f), 0.09f).setEaseInQuart();
            }*/
        }

        private void doScoreText(int whichtext)
        {
            string text = "";
            /*if (!_releaseBetweenNotes)
            {
                LeanTween.cancel(_noGapObject);
                _noGapObject.transform.localScale = new Vector3(0.001f, 0.001f, 1f);
                LeanTween.scale(_noGapObject, new Vector3(1f, 1f, 1f), 0.1f).setEaseOutQuart();
                if (_maxStarObject.transform.localScale.x > 0.1f)
                {
                    LeanTween.cancel(_maxStarObject);
                    _maxStarObject.transform.localScale = new Vector3(0f, 0f, 1f);
                }
            }
            else if (_releaseBetweenNotes && _noGapObject.transform.localScale.x > 0.1f)
            {
                LeanTween.cancel(_noGapObject);
                _noGapObject.transform.localScale = new Vector3(0f, 0f, 1f);
            }*/
            if (whichtext == 4)
                text = "<i>PERFECTO!</i>";
            else if (whichtext == 3)
                text = "<i>NICE</i>";
            else if (whichtext == 2)
                text = "<i>OK</i>";
            else if (whichtext == 1)
                text = "<i>MEH</i>";
            else if (whichtext == 0)
                text = "<i>NASTY</i>";

            animateOutNote(_gcInstance.currentnoteindex, whichtext);
            /*_popupText.text = text;
            _popupTextShadow.text = text;*/

            _multiHideTimer = 0f;
            /*if (_multiplier > 0)
            {
                _multiplierText.text = "<i>" + _multiplier.ToString() + "<size=28>x</size></i>";
                _multiplierTextShadow.text = "<i>" + _multiplier.ToString() + "<size=28>x</size></i>";
                LeanTween.cancel(_multiplierTextShadow.gameObject);
                _multiplierTextShadow.gameObject.transform.localScale = new Vector3(0.001f, 0.001f, 1f);
                LeanTween.scale(_multiplierTextShadow.gameObject, new Vector3(1f, 1f, 1f), 0.1f).setEaseOutQuart();
                _popupTextShadow.rectTransform.anchoredPosition3D = new Vector3(0f, -11f, 0f);
                _noGapRect.anchoredPosition3D = new Vector3(0f, -6f, 0f);
            }
            else
            {
                _popupTextShadow.rectTransform.anchoredPosition3D = new Vector3(0f, -31f, 0f);
                _noGapRect.anchoredPosition3D = new Vector3(0f, -26f, 0f);
                _multiplierText.text = "";
                _multiplierTextShadow.text = "";
            }
            if (_multiplier == MAX_MULTIPLIER)
            {
                LeanTween.cancel(_maxStarObject);
                _maxStarObject.transform.localScale = new Vector3(0.001f, 0.001f, 1f);
                LeanTween.scale(_maxStarObject, new Vector3(0.26f, 0.28f, 1f), 0.1f).setEaseOutQuart();
            }
            else
            {
                _maxStarObject.transform.localScale = new Vector3(0f, 0f, 1f);
            }
            LeanTween.cancel(_popupTextShadow.gameObject);
            _popupTextShadow.gameObject.transform.localScale = new Vector3(0.001f, 0.001f, 1f);
            LeanTween.scale(_popupTextShadow.gameObject, new Vector3(1f, 1f, 1f), 0.1f).setEaseOutQuart();
            if (whichtext == 4)
            {
                _popupText.color = new Color(1f, 1f, 0.95f, 1f);
                _popupTextShadow.color = new Color(0.28f, 0.27f, 0f, 1f);
                return;
            }
            if (whichtext == 0)
            {
                _popupText.color = new Color(1f, 1f, 1f, 1f);
                _popupTextShadow.color = new Color(0.23f, 0.11f, 0.05f, 1f);
                return;
            }
            _popupText.color = new Color(1f, 1f, 1f, 1f);
            _popupTextShadow.color = new Color(0f, 0f, 0f, 1f);*/
        }

        private void animateOutNote(int noteindex, int performance)
        {
            GameController.noteendeffect noteendeffect = _allNoteEndEffects[_noteParticlesIndex];
            AnimateNoteEndEffectAnimations anim = _allNoteEndAnimations[_noteParticlesIndex];
            anim.CancelAllAnimations();
            noteendeffect.noteeffect_obj.SetActive(true);
            noteendeffect.noteeffect_rect.anchoredPosition3D = new Vector3(0f, _gcInstance.allnotes[noteindex].transform.GetComponent<RectTransform>().anchoredPosition3D.y + _gcInstance.allnotes[noteindex].transform.GetChild(1).GetComponent<RectTransform>().anchoredPosition3D.y, 0f);
            noteendeffect.burst_obj.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            noteendeffect.combotext_obj.transform.localScale = new Vector3(1f, 1f, 1f);
            noteendeffect.drops_canvasg.alpha = 0.85f;
            noteendeffect.drops_obj.transform.localScale = new Vector3(0.1f, 0.1f, 1f);

            if (performance > 3)
            {
                noteendeffect.burst_img.sprite = _gcInstance.noteparticle_images[1];
                noteendeffect.burst_canvasg.alpha = 0.7f;
            }
            else
            {
                noteendeffect.burst_img.sprite = _gcInstance.noteparticle_images[0];
                noteendeffect.burst_canvasg.alpha = 0.4f;
            }
            //LeanTween.scale(noteendeffect.burst_obj, new Vector3(1f, 1f, 1f), 0.3f).setEaseOutQuart();
            //LeanTween.rotateZ(noteendeffect.burst_obj, -90f, 0.3f).setEaseLinear();
            //LeanTween.alphaCanvas(noteendeffect.burst_canvasg, 0f, 0.3f).setEaseInOutQuart();
            //LeanTween.scale(noteendeffect.drops_obj, new Vector3(0.9f, 0.9f, 1f), 0.25f).setEaseOutQuart();
            //LeanTween.alphaCanvas(noteendeffect.drops_canvasg, 0f, 0.3f).setEaseLinear();
            //LeanTween.scale(noteendeffect.combotext_obj, new Vector3(0.7f, 0.7f, 1f), 0.5f).setEaseOutQuart();
            //LeanTween.moveLocalX(noteendeffect.combotext_obj, 30f, 0.5f).setEaseOutQuart();
            //LeanTween.scale(noteendeffect.combotext_obj, new Vector3(1E-05f, 1E-05f, 0f), 0.2f).setEaseInOutQuart().setDelay(0.51f);
            if (_multiplier > 0)
            {
                noteendeffect.burst_img.color = new Color(1f, 1f, 1f, 1f);
                noteendeffect.combotext_txt_shadow.text = _multiplier.ToString() + "x";
                noteendeffect.combotext_txt_front.color = new Color(1f, 1f, 1f, 1f);
                noteendeffect.combotext_txt_front.text = _multiplier.ToString() + "x";
                noteendeffect.combotext_rect.anchoredPosition3D = new Vector3(15f, 15f, 0f);
                noteendeffect.combotext_rect.localEulerAngles = new Vector3(0f, 0f, -40f);
                //LeanTween.moveLocalY(noteendeffect.combotext_obj, 45f, 0.5f).setEaseOutQuad();
                //LeanTween.rotateZ(noteendeffect.combotext_obj, -10f, 0.5f).setEaseOutQuart();
            }
            else
            {
                if (performance == 0)
                {
                    noteendeffect.burst_img.color = new Color(1f, 0f, 0f, 1f);
                    noteendeffect.combotext_txt_shadow.text = "x";
                    noteendeffect.combotext_txt_front.color = new Color(1f, 0f, 0f, 1f);
                    noteendeffect.combotext_txt_front.text = "x";
                }
                else if (performance == 1)
                {
                    noteendeffect.burst_img.color = new Color(1f, 1f, 1f, 1f);
                    noteendeffect.combotext_txt_shadow.text = "MEH";
                    noteendeffect.combotext_txt_front.color = new Color(1f, 1f, 1f, 1f);
                    noteendeffect.combotext_txt_front.text = "MEH";
                }
                else if (performance == 2)
                {
                    noteendeffect.burst_img.color = new Color(1f, 1f, 1f, 1f);
                    noteendeffect.combotext_txt_shadow.text = "OK";
                    noteendeffect.combotext_txt_front.color = new Color(1f, 1f, 1f, 1f);
                    noteendeffect.combotext_txt_front.text = "OK";
                }
                noteendeffect.combotext_rect.anchoredPosition3D = new Vector3(15f, -20f, 0f);
                noteendeffect.combotext_rect.localEulerAngles = new Vector3(0f, 0f, 40f);
                //LeanTween.moveLocalY(noteendeffect.combotext_obj, -50f, 0.5f).setEaseOutQuad();
                //LeanTween.rotateZ(noteendeffect.combotext_obj, 10f, 0.5f).setEaseOutQuart();
            }
            anim.StartAnimateOutAnimation(noteendeffect.burst_obj, noteendeffect.burst_canvasg.gameObject, noteendeffect.drops_obj, noteendeffect.drops_canvasg.gameObject, noteendeffect.combotext_obj, _multiplier);
            _noteParticlesIndex++;
            if (_noteParticlesIndex > 14)
                _noteParticlesIndex = 0;
        }

        private class AnimateNoteEndEffectAnimations
        {
            private TootTallyAnimation burstObjScale, burstObjRotate, burstCanvasAlpha;
            private TootTallyAnimation dropsObjScale, dropObjAlpha;
            private TootTallyAnimation comboTextScale, comboTextPosY, comboTextRotateZ;


            public void StartAnimateOutAnimation(GameObject burst_obj, GameObject burst_canvasg, GameObject drops_obj, GameObject drops_canvasg, GameObject comboText_obj, int multiplier)
            {
                burstObjScale = TootTallyAnimationManager.AddNewScaleAnimation(burst_obj, Vector3.one, .3f, new SecondDegreeDynamicsAnimation(2f, 1, .5f));
                burstObjRotate = TootTallyAnimationManager.AddNewRotationAnimation(burst_obj, new Vector3(0, 0, -90f), .3f, new SecondDegreeDynamicsAnimation(2f, 1, 1));
                burstCanvasAlpha = TootTallyAnimationManager.AddNewAlphaAnimation(burst_canvasg, 0, .3f, new SecondDegreeDynamicsAnimation(.75f, 1, .15f));

                dropsObjScale = TootTallyAnimationManager.AddNewScaleAnimation(drops_obj, new Vector3(.9f, .9f, 1f), .25f, new SecondDegreeDynamicsAnimation(2.25f, 1, 1));
                dropObjAlpha = TootTallyAnimationManager.AddNewAlphaAnimation(drops_canvasg, 0, .3f, new SecondDegreeDynamicsAnimation(.75f, 1, .15f));

                comboTextScale = TootTallyAnimationManager.AddNewScaleAnimation(comboText_obj, new Vector3(.7f, .7f, 1f), .4f, new SecondDegreeDynamicsAnimation(2.15f, 1, 1.2f), delegate
                {
                    comboTextScale = TootTallyAnimationManager.AddNewScaleAnimation(comboText_obj, new Vector3(1E-05f, 1E-05f, 0f), .2f, new SecondDegreeDynamicsAnimation(2.5f, 1, 1.2f));
                });
                float targetPosY, targetRotZ;
                if (multiplier > 0)
                {
                    targetPosY = 45f;
                    targetRotZ = -10f;
                }
                else
                {
                    targetPosY = -50f;
                    targetRotZ = 10f;
                }

                comboTextPosY = TootTallyAnimationManager.AddNewTransformLocalPositionAnimation(comboText_obj, new Vector3(30f, targetPosY, 1f), .5f, new SecondDegreeDynamicsAnimation(2.15f, 1, 1f));
                comboTextRotateZ = TootTallyAnimationManager.AddNewRotationAnimation(comboText_obj, new Vector3(0f, 0f, targetRotZ), .5f, new SecondDegreeDynamicsAnimation(2.25f, 1, .5f));
            }


            public void CancelAllAnimations()
            {
                burstObjScale?.Dispose(true);
                burstObjRotate?.Dispose(true);
                dropsObjScale?.Dispose(true);
                dropObjAlpha?.Dispose(true);
                comboTextScale?.Dispose(true);
                comboTextPosY?.Dispose(true);
                comboTextRotateZ?.Dispose(true);
                burstCanvasAlpha?.Dispose(true);
            }
        }
    }
}
