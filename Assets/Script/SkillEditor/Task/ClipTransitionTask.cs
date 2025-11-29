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

        private AnimancerState m_State;

        public override void OnTaskBegin()
        {
            m_State = null;
            var ctl = this.PlayerController;
            if (ctl)
            {
                m_State = ctl.Play(TransitionAsset);
            }
        }

        public override void OnTaskEnd()
        {
            if (m_State != null)
            {
                m_State.Stop();
                m_State = null;
            }
        }

        protected AnimancerComponent m_Animancer = null;
        protected AnimancerComponent PlayerController
        {
            get
            {
                if (m_Animancer == null)
                {
                    if (actor)
                        m_Animancer = actor.GetComponent<AnimancerComponent>();
                }
                return m_Animancer;
            }
        }
    }

}
