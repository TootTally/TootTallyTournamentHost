using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TootTallySettings;
using TootTallySettings.TootTallySettingsObjects;
using UnityEngine;
using UnityEngine.UI;
using static TootTallyTournamentHost.TournamentHostManager;

namespace TootTallyTournamentHost
{
    public class TournamentHostSettingPage : TootTallySettingPage
    {
        private static ColorBlock _pageBtnColors = new ColorBlock()
        {
            colorMultiplier = 1f,
            fadeDuration = .2f,
            disabledColor = Color.gray,
            normalColor = new Color(1, 0, .5f),
            pressedColor = new Color(1f, .2f, .7f),
            highlightedColor = new Color(.8f, 0, .3f),
            selectedColor = new Color(1, 0, .5f)
        };

        private TootTallySettingDropdown _layoutTypeDropdown;
        private TootTallySettingSlider _horizontalScreenCountSlider, _verticalScreenCountSlider;
        private TournamentHostLayoutPreview _layoutPreview;

        public TournamentHostSettingPage() : base("Tournament Host", "Tournament Host", 40f, new Color(0,0,0,.1f), _pageBtnColors)
        {
            
            _layoutTypeDropdown = AddDropdown("Layout Type", Plugin.Instance.LayoutType);
            _horizontalScreenCountSlider = AddSlider("Horizontal Screen", 1, 10, Plugin.Instance.HorizontalScreenCount, true);
            _verticalScreenCountSlider = AddSlider("Vertical Screen", 1, 10, Plugin.Instance.VerticalScreenCount, true);
            AddToggle("Enable Note Particles", Plugin.Instance.EnableNoteParticles);
            AddToggle("Enable Miss Glow", Plugin.Instance.EnableMissGlow);
            AddToggle("Enable Score Text", Plugin.Instance.EnableScoreText);
            AddToggle("Enable Score Percent Text", Plugin.Instance.EnableScorePercentText);
            AddToggle("Enable Champ Meter", Plugin.Instance.EnableChampMeter);
            AddToggle("Enable Time Elapsed", Plugin.Instance.EnableTimeElapsed);
            AddToggle("Enable Highest Combo", Plugin.Instance.EnableHighestCombo);
            AddToggle("Enable Username", Plugin.Instance.EnableUsername);

            if (TootTallySettingsManager.isInitialized)
                Initialize();
        }

        public override void Initialize()
        {
            base.Initialize();
            _layoutPreview = new TournamentHostLayoutPreview(gridPanel.transform);
            _horizontalScreenCountSlider.slider.onValueChanged.AddListener(_layoutPreview.UpdateLayout);
            _verticalScreenCountSlider.slider.onValueChanged.AddListener(_layoutPreview.UpdateLayout);
            _layoutTypeDropdown.dropdown.onValueChanged.AddListener(OnLayoutTypeChange);
            UpdateScreenCountSliderState();
            
        }

        private void OnLayoutTypeChange(int value)
        {
            Vector2 screenCount = (LayoutType)value switch
            {
                LayoutType.OneVsOne => new Vector2(1, 2),
                LayoutType.TwoVsTwo => new Vector2(2, 2),
                LayoutType.ThreeVsThree => new Vector2(3, 2),
                LayoutType.FourVsFour => new Vector2(4, 2),
                _ => new Vector2(4, 4),
            };
            UpdateParams(screenCount);
        }

        private void UpdateParams(Vector2 screenCount)
        {
            Plugin.Instance.HorizontalScreenCount.Value = screenCount.x;
            Plugin.Instance.VerticalScreenCount.Value = screenCount.y;

            UpdateScreenCountSliderState();
            _layoutPreview.UpdateLayout();
        }

        private void UpdateScreenCountSliderState()
        {
            _horizontalScreenCountSlider.slider.gameObject.SetActive(Plugin.Instance.LayoutType.Value == LayoutType.Custom);
            _verticalScreenCountSlider.slider.gameObject.SetActive(Plugin.Instance.LayoutType.Value == LayoutType.Custom);
        }
    }
}
