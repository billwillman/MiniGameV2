using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Animancer;
using Slate;

namespace GAS
{
    [Attachable(typeof(CommonActionTaskTrack))]
    [Description("Animacer Transition Task")]
    public class ClipTransitionTask : BaseTask
    {
        public ClipTransition TransitionAsset = null;

        public override float length
        {
            get
            {
                if (TransitionAsset == null)
                    return 0;
                return TransitionAsset.Length;
            }
        }
    }

}
