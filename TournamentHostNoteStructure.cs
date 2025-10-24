using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TootTallyTournamentHost
{
    public struct TournamentHostNoteStructure
    {
        public GameObject root, noteStart, noteEnd;
        public NoteDesigner noteDesigner;
        public RectTransform noteRect, noteEndRect;
        public LineRenderer[] lineRenderers;


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
        }

        public void CancelLeanTweens() => LeanTween.cancel(root);

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
