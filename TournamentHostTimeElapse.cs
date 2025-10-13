using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TootTallyCore.Graphics.Animations;
using UnityEngine;
using UnityEngine.UI;

namespace TootTallyTournamentHost
{
    public class TournamentHostTimeElapse
    {
        private GameObject _timeTextObj;
        private Text _timeTextShad, _timeText;

        private GameObject _barObj;
        private RectTransform _fullBarRect, _barRect, _barNubRect;
        private readonly float MAX_TIME;
        private int _barIndex;

        public TournamentHostTimeElapse(GameController __instance, Transform parent)
        {
            MAX_TIME = __instance.levelendtime;
            _barIndex = 0;
            _timeTextObj = GameObject.Instantiate(__instance.timeelapsedobj, parent);
            _timeTextShad = _timeTextObj.transform.GetChild(0).GetComponent<Text>();
            _timeText = _timeTextShad.transform.GetChild(0).GetComponent<Text>();

            _barObj = GameObject.Instantiate(__instance.time_bar_full_obj, parent);
            _fullBarRect = _barObj.GetComponent<RectTransform>();
            _barRect = _barObj.transform.GetChild(4).GetComponent<RectTransform>();
            _barNubRect = _barObj.transform.GetChild(5).GetComponent<RectTransform>();
        }

        public void UpdateTimeBar(float time)
        {
            var timePercent = (time / MAX_TIME);

            _barRect.localScale = new Vector3(timePercent, 1f, 1f);
            _barNubRect.anchoredPosition = new Vector2(timePercent * 120f - 60f, 50f);
            if (_barIndex < 3 && timePercent - (.25f * (_barIndex + 1)) >= 0)
            {
                AnimateNextTick();
                _barIndex++;
            }
        }

        public void AnimateNextTick()
        {
            _fullBarRect.localScale = new Vector3(1.1f, 1.1f, 1f);
            TootTallyAnimationManager.AddNewScaleAnimation(_barObj, Vector3.one, .15f, new SecondDegreeDynamicsAnimation(3.25f, 1f, 1f));
            var barTick = _barObj.transform.GetChild(_barIndex + 1).gameObject;
            barTick.GetComponent<RectTransform>().localScale = new Vector3(4f, 8f, 1f);
            TootTallyAnimationManager.AddNewScaleAnimation(barTick, Vector3.one, .15f, new SecondDegreeDynamicsAnimation(3.25f, 1f, 1f));
        }


        public void UpdateTimeText(string text)
        {
            _timeTextShad.text = _timeText.text = text;
        }

        public void Hide()
        {
            _timeTextObj.SetActive(false);
            _barObj.SetActive(false);
        }

        public void Show()
        {
            _timeTextObj.SetActive(true);
            _barObj.SetActive(true);
        }

    }
}
