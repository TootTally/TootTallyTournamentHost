using Microsoft.FSharp.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TootTallyCore.Graphics.Animations;
using TootTallyDiffCalcLibs;
using UnityEngine;

using UnityEngine.UI;

namespace TootTallyTournamentHost
{
    public struct TournamentHostNoteStructure
    {
        public GameObject root, noteStart, noteEnd;
        public NoteDesigner noteDesigner;
        public RectTransform noteRect, noteEndRect;
        public LineRenderer[] lineRenderers;
        private TootTallyAnimation _animation;
        private Color _headOutColor, _headInColor, _tailOutColor, _tailInColor, _bodyOutStartColor, _bodyInStartColor, _bodyOutEndColor, _bodyInEndColor;
        private Image _headOut, _headIn, _tailOut, _tailIn;
        private Color _alphaStart, _alphaEnd;


        public TournamentHostNoteStructure(GameObject root)
        {
            root.SetActive(true);
            this.root = root;
            noteDesigner = root.GetComponent<NoteDesigner>();
            noteRect = root.GetComponent<RectTransform>();
            noteStart = root.transform.GetChild(0).gameObject;
            noteEnd = root.transform.GetChild(1).gameObject;
            noteEndRect = noteEnd.GetComponent<RectTransform>();
            lineRenderers = new LineRenderer[]
            {
                root.transform.GetChild(2).GetComponent<LineRenderer>(),
                root.transform.GetChild(3).GetComponent<LineRenderer>(),
            };
            lineRenderers[0].sortingOrder = -2;
            lineRenderers[1].sortingOrder = -1;
            InitHD();
            ResetHD();
        }

        public void CancelAnimation() => _animation?.Dispose();

        public void AnimateNoteOut() => _animation = TootTallyAnimationManager.AddNewScaleAnimation(root, new Vector3(0, 0, 1), .1f, new SecondDegreeDynamicsAnimation(1, 1, 1));

        public static float START_FADEOUT_POSX = 3.5f + 350f;
        public static float END_FADEOUT_POSX = -1.6f + 350f;

        public void InitHD()
        {
            _alphaStart = _alphaEnd = Color.black;
            _headOut = root.transform.Find("StartPoint").GetComponent<Image>();
            _headIn = root.transform.Find("StartPoint/StartPointColor").GetComponent<Image>();
            _tailOut = root.transform.Find("EndPoint").GetComponent<Image>();
            _tailIn = root.transform.Find("EndPoint/EndPointColor").GetComponent<Image>();

            _headOutColor = _headOut.color;
            _headInColor = _headIn.color;
            _tailOutColor = _tailOut.color;
            _tailInColor = _tailIn.color;
            _bodyOutStartColor = lineRenderers[0].startColor;
            _bodyOutEndColor = lineRenderers[0].endColor;
            _bodyInStartColor = lineRenderers[1].startColor;
            _bodyInEndColor = lineRenderers[1].endColor;
        }

        public void ResetHD()
        {
            if (root == null) return;
            lineRenderers[0].startColor = _bodyOutStartColor;
            lineRenderers[1].startColor = _bodyInStartColor;
            _headOut.color = _headOutColor;
            _headIn.color = _headInColor;

            lineRenderers[0].endColor = _bodyOutEndColor;
            lineRenderers[1].endColor = _bodyInEndColor;
            _tailOut.color = _tailOutColor;
            _tailIn.color = _tailInColor;
        }

        public void UpdateHD()
        {
            if (UnSanityChecks() || noteStart.transform.position.x > START_FADEOUT_POSX + 2 || noteEnd.transform.position.x < END_FADEOUT_POSX - 2) return;

            _alphaStart.a = 1f - Mathf.Clamp((noteStart.transform.position.x - END_FADEOUT_POSX) / (START_FADEOUT_POSX - END_FADEOUT_POSX), 0, 1);
            _alphaEnd.a = 1f - Mathf.Clamp((noteEnd.transform.position.x - END_FADEOUT_POSX) / (START_FADEOUT_POSX - END_FADEOUT_POSX), 0, 1);

            lineRenderers[0].startColor = _bodyOutStartColor - _alphaStart;
            lineRenderers[1].startColor = _bodyInStartColor - _alphaStart;
            _headOut.color = _headOutColor - _alphaStart;
            _headIn.color = _headInColor - _alphaStart;

            lineRenderers[0].endColor = _bodyOutEndColor - _alphaEnd;
            lineRenderers[1].endColor = _bodyInEndColor - _alphaEnd;
            _tailOut.color = _tailOutColor - _alphaEnd;
            _tailIn.color = _tailInColor - _alphaEnd;
        }

        private bool UnSanityChecks() => noteStart == null || _headOut == null || _headIn == null || _tailOut == null || _tailIn == null;

        public void SetColorScheme(float[] start, float[] end, bool flipScheme)
        {
            if (flipScheme)
                (end, start) = (start, end);

            noteDesigner.setColorScheme(
            start[0],
            start[1],
            start[2],
            end[0],
            end[1],
            end[2]);
        }
    }
}
