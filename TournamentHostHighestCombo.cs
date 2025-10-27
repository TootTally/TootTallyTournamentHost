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
    public class TournamentHostHighestCombo
    {
        private GameObject _highestComboObject;
        private RectTransform _highestComboRect;
        private Text _highestComboText, _highestComboTextShadow;

        private TootTallyAnimation _animation;
        private int _highestComboCount;
        private readonly Vector3 _highestComboStartScale;

        public TournamentHostHighestCombo(GameController __instance, Transform parent)
        {
            _highestComboStartScale = new Vector3(1.5f, 1.2f, 1f);
            _highestComboObject = GameObject.Instantiate(__instance.highestcomboobj, parent);
            _highestComboObject.SetActive(true); // In case GameplayUIReducer hides it
            _highestComboRect = _highestComboObject.GetComponent<RectTransform>();
            _highestComboTextShadow = _highestComboObject.transform.GetChild(0).GetComponent<Text>();
            _highestComboText = _highestComboTextShadow.transform.GetChild(0).GetComponent<Text>();
            _highestComboCount = 0;
        }

        public void UpdateHighestCombo(int currentCombo)
        {
            if (_highestComboCount >= currentCombo) return;

            _highestComboCount = currentCombo;
            _animation?.Dispose();
            _highestComboRect.localScale = _highestComboStartScale;
            _animation = TootTallyAnimationManager.AddNewScaleAnimation(_highestComboObject, Vector3.one, .1f, new SecondDegreeDynamicsAnimation(3.5f, 1.5f, 1f));
            _highestComboText.text = $"Longest Combo: <color=#ffff00>{_highestComboCount}</color>";
            _highestComboTextShadow.text = $"Longest Combo: {_highestComboCount}";
        }

        public void Hide()
        {
            _highestComboObject.SetActive(false);
        }
    }
}
