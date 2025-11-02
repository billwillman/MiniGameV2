using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Slate;

namespace GAS
{
    [Attachable(typeof(CommonActionTaskTrack))]
    [Description("跳转Task")]
    [Name("JumpTask")]
    public class JumpTask : BaseTask
    {
        // 条件失败跳转到时间
        public float ConditionFailJumpToFrom = -1f;
        public bool ConditionFailJumpEndTime = false;
        public float ConditionOKJumpToFrom = -1;
        public bool ConditionOKJumpEndTime = false;

        public override void OnTaskBegin()
        {
            if (this.root == null || ConditionOKJumpToFrom < 0)
                return;
            if (ConditionOKJumpEndTime)
                this.RootCurrentTime = this.RootTimeLength - ConditionOKJumpToFrom;
            else
                this.RootCurrentTime = ConditionOKJumpToFrom;
            this.ResultState = TaskResultState.Success;
        }

        public override void OnCheckConditionFail()
        {
            if (this.root == null || ConditionFailJumpToFrom < 0)
                return;
            if (ConditionOKJumpEndTime)
                this.RootCurrentTime = this.RootTimeLength - ConditionFailJumpToFrom;
            else
                this.RootCurrentTime = ConditionFailJumpToFrom;
        }

        [SerializeField, HideInInspector]
        private float m_TimeLen = 0.1f;

        public override float length
        {
            get
            {
                return m_TimeLen;
            }
            set
            {
                m_TimeLen = value;
                if (m_TimeLen < 0.1f)
                    m_TimeLen = 0.1f;
            }
        }
    }
}
