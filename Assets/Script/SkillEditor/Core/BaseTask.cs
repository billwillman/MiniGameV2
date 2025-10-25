using Slate;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    // 基本类
    //[Attachable(typeof(CommonGroup))]
    //[Name("BaseTask")]
    public abstract class BaseTask: CutsceneTrack
    {
        public bool CanTimeSkip = false; // 能否时间过了跳过，默认不可以

        public TagManager m_TagManager = null; // 标记

        public abstract void OnTaskBegin();
        public abstract void OnTaskEnd();

        protected override void OnEnter()
        {
            base.OnEnter();
            float currenTime = this.CurrentTime;
            if (currenTime >= 0 && (!this.CanTimeSkip || currenTime < this.endTime))
            {
                OnTaskBegin();
            }
        }

        protected override void OnExit()
        {
            base.OnExit();
            OnTaskEnd();
        }

        public Cutscene Scene
        {
            get
            {
                if (m_CutScene == null)
                    m_CutScene = GetComponent<Cutscene>();
                return m_CutScene;
            }
        }

        public float CurrentTime
        {
            get
            {
                Cutscene scene = this.Scene;
                if (scene != null)
                    return scene.currentTime;
                return -1;
            }
        }

        private Cutscene m_CutScene = null;
    }

}
