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
        public float ConditionFailJumpToFromStartTime = -1f;
        public float ConditionOKJumpToFromStartTime = -1;

        public override void OnTaskBegin()
        {
            if (this.root == null || ConditionOKJumpToFromStartTime < 0)
                return;
            this.RootCurrentTime = ConditionOKJumpToFromStartTime;
            this.ResultState = TaskResultState.Success;
        }

        public override void OnCheckConditionFail()
        {
            if (this.root == null || ConditionFailJumpToFromStartTime < 0)
                return;
            this.root.currentTime = ConditionFailJumpToFromStartTime;
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
