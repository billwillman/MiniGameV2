using Slate;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{

    public class BaseCrossTask : CutsceneTrack
    {
        public bool CanTimeSkip = false; // 能否时间过了跳过，默认不可以
        public bool IsRunOnce; // 是否只执行一次
        protected uint RunedTimes = 0; // 执行了几次

        public TaskResultState ResultState
        {
            get;
            set;
        }

        private GameTags m_TagRef = null;
        public GameTags Tags
        {
            get
            {
                if (m_TagRef != null)
                    return m_TagRef;
                if (actor != null)
                {
                    m_TagRef = actor.GetComponent<GameTags>();
                }
                return m_TagRef;
            }
        }

        public List<BaseCondition> PreConditions
        {
            get;
            set;
        }

        public bool IsOKByPreConditions()
        {
            if (PreConditions == null || PreConditions.Count <= 0)
                return true;
            for (int i = 0; i < PreConditions.Count; ++i)
            {
                var condtion = PreConditions[i];
                if (condtion != null)
                {
                    if (!condtion.IsConditionOK())
                        return false;
                }
            }
            return true;
        }

        public bool GotoTime(float offsetTime = 0, bool isStartEndFrame = false)
        {
            if (offsetTime < 0)
                return false;
            if (isStartEndFrame)
                this.RootCurrentTime = this.RootTimeLength - offsetTime;
            else
                this.RootCurrentTime = offsetTime;
            return true;
        }

        public bool IsTaskTimesNoCanRun()
        {
            if (IsRunOnce)
            {
                if (RunedTimes > 0)
                    return false;
            }
            return true;
        }

        public virtual void OnTaskBegin() { }
        public virtual void OnTaskEnd() { }
        public virtual void OnCheckConditionFail() { }

        public virtual void OnTaskUpdate(float time, float previousTime) { }

        public float RootCurrentTime
        {
            get
            {
                return this.root.currentTime;
            }

            set
            {
                if (value < 0)
                    value = 0;
                this.root.currentTime = value;
            }
        }

        public float RootTimeLength
        {
            get
            {
                return this.root.length;
            }
        }

        protected override void OnUpdate(float time, float previousTime)
        {
            base.OnUpdate(time, previousTime);
            if (IsStartRuning)
            {
                OnTaskUpdate(time, previousTime);
            }
        }

        protected override void OnEnter()
        {
            base.OnEnter();
            float currenTime = this.RootCurrentTime;
            if (currenTime >= 0 && (!this.CanTimeSkip || currenTime < this.endTime))
            {
                if (IsTaskTimesNoCanRun())
                {
                    if (IsOKByPreConditions())
                    {
                        IsStartRuning = true;
                        ++RunedTimes;
                        OnTaskBegin();
                    } else
                        OnCheckConditionFail();
                }
            }
        }

        protected bool IsStartRuning = false;

        protected override void OnExit()
        {
            base.OnExit();
            if (IsStartRuning)
            {
                OnTaskEnd();
                IsStartRuning = false;
            }
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
