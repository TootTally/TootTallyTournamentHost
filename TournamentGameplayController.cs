using System;
using System.Collections.Generic;
using System.Linq;
using TootTallyCore.Graphics;
using TootTallyCore.Graphics.Animations;
using TootTallyCore.Utils.Helpers;
using TootTallyMultiplayer;
using TootTallySpectator;
using UnityEngine;
using UnityEngine.UI;
using static TootTallySpectator.SpectatingManager;

namespace TootTallyTournamentHost
{
    public class TournamentGameplayController : MonoBehaviour
    {
        private GameController _gcInstance;
        private GameObject _container;
        private Canvas _canvas;
        private Camera _camera;
        private Rect _bounds;
        private SpectatingSystem _spectatingSystem;
        private GameObject _pointer;
        private RectTransform _pointerRect;
        private CanvasGroup _pointerGlowCanvasGroup;
        private GameObject _noteParticles;
        private GameObject _UIHolder;
        private GameObject _champCanvas, _champPanel;
        private static bool _hasSentSecondFlag, _hasSentFirstFlag;

        public bool IsReady => _frameData != null && _frameData.Count > 0 && _frameData.Last().time > 1f;

        private bool _isTooting;

        public void Initialize(GameController gcInstance, Camera camera, Rect bounds, Transform canvasTransform, SpectatingSystem spectatingSystem)
        {
            _hasSentSecondFlag = _hasSentFirstFlag = false;
            _gcInstance = gcInstance;
            _camera = camera;
            _bounds = bounds;
            camera.pixelRect = bounds;
            _spectatingSystem = spectatingSystem;
            if (_spectatingSystem != null)
                _spectatingSystem.OnWebSocketOpenCallback = OnSpectatingConnect;

            _container = GameObject.Instantiate(new GameObject("Container"), canvasTransform.transform);
            _container.AddComponent<RectTransform>();

            _noteParticles = GameObject.Instantiate(_gcInstance.noteparticles, _container.transform);
            _allNoteEndEffects = new GameController.noteendeffect[15];
            SetAllNoteEndEffects();

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

            _noteParticlesIndex = 0;

            _pointer = GameObject.Instantiate(gcInstance.pointer, _container.transform);
            _pointerRect = _pointer.GetComponent<RectTransform>();
            _pointerGlowCanvasGroup = _pointer.transform.Find("note-dot-glow").GetComponent<CanvasGroup>();
            _frameIndex = 0;
            _tootIndex = 0;
            _lastFrame.time = -1;
            _currentNoteData.noteID = -1;
            _currentFrame = new SocketFrameData() { time = -1, noteHolder = 0, pointerPosition = 0 };
            _currentTootData = new SocketTootData() { time = -1, isTooting = false, noteHolder = 0 };
            _isTooting = false;
        }

        private Vector2 _pointerPos;

        public void InitFlashLight()
        {
            _pointerPos = new Vector2(.075f, (_pointer.transform.localPosition.y + 215) / 430);
            //TODO: Implement FL texture with mask to gamespace so it doesnt hide UI elements
        }

        public void UpdateFlashLight()
        {
            _pointerPos.y = (_pointer.transform.localPosition.y + 215) / 430;
            //TODO: Move texture position 
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
            PlaybackSpectatingData(_gcInstance);
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
                _currentNoteData.noteID = -1;
            }
            getScoreAverage();
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

        private int _currentScore;

        private void HandlePitchShift()
        {
            if (_tClips == null || _currentNoteSound == null || TournamentHostManager.isWaitingForSync) return;

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

        private GameController.noteendeffect[] _allNoteEndEffects;

        private int _noteParticlesIndex;

        private int _totalScore;
        private bool _champMode;
        private bool _releaseBetweenNotes;
        private int _multiplier, _highestcombo;
        private const int MAX_MULTIPLIER = 10;

        private float _noteScoreAverage;

        private void getScoreAverage()
        {
            if (!_releaseBetweenNotes)
                affectHealthBar(-15f);
            else
                affectHealthBar(Mathf.Clamp((_noteScoreAverage - 79f) * 0.2193f, -15f, 4.34f));
            var textID = _noteScoreAverage > 95f ? 4 :
                _noteScoreAverage > 88f ? 3 :
                _noteScoreAverage > 79f ? 2 :
                _noteScoreAverage > 70f ? 1 : 0;
            animateOutNote(_gcInstance.currentnoteindex, textID);
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

        private List<TournamentHostNoteEndAnimation> _allNoteEndAnimations;

        private void animateOutNote(int noteindex, int performance)
        {
            GameController.noteendeffect noteendeffect = _allNoteEndEffects[_noteParticlesIndex];
            TournamentHostNoteEndAnimation anim = _allNoteEndAnimations[_noteParticlesIndex];
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
            if (_multiplier > 0)
            {
                noteendeffect.burst_img.color = new Color(1f, 1f, 1f, 1f);
                noteendeffect.combotext_txt_shadow.text = _multiplier.ToString() + "x";
                noteendeffect.combotext_txt_front.color = new Color(1f, 1f, 1f, 1f);
                noteendeffect.combotext_txt_front.text = _multiplier.ToString() + "x";
                noteendeffect.combotext_rect.anchoredPosition3D = new Vector3(15f, 15f, 0f);
                noteendeffect.combotext_rect.localEulerAngles = new Vector3(0f, 0f, -40f);
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
            }
            anim.StartAnimateOutAnimation(noteendeffect.burst_obj, noteendeffect.burst_canvasg.gameObject, noteendeffect.drops_obj, noteendeffect.drops_canvasg.gameObject, noteendeffect.combotext_obj, _multiplier);
            _noteParticlesIndex++;
            if (_noteParticlesIndex > 14)
                _noteParticlesIndex = 0;
        }
    }
}
