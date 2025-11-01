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

    // ������
    //[Attachable(typeof(CommonGroup))]
    //[Name("BaseTask")]
    public abstract class BaseTask: CutsceneTrack
    {
        public bool CanTimeSkip = false; // �ܷ�ʱ�����������Ĭ�ϲ�����
        public bool IsRunOnce; // �Ƿ�ִֻ��һ��
        public uint RunedTimes = 0; // ִ���˼���

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

        // ������
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

        protected override void OnEnter()
        {
            base.OnEnter();
            float currenTime = this.CurrentTime;
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
