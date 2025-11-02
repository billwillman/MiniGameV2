using Slate;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    public enum TaskResultState
    {
        Default = 0,
        Success = 1,
        Fail = -1
    }

    // 基本类
    //[Attachable(typeof(CommonGroup))]
    //[Name("BaseTask")]
    public abstract class BaseTask: ActionClip
    {
        public bool CanTimeSkip = false; // 能否时间过了跳过，默认不可以
        public bool IsRunOnce; // 是否只执行一次
        public uint RunedTimes = 0; // 执行了几次

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

        // 依赖项
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
