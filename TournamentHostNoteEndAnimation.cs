using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TootTallyCore.Graphics.Animations;
using UnityEngine;

namespace TootTallyTournamentHost
{
    public class TournamentHostNoteEndAnimation
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
