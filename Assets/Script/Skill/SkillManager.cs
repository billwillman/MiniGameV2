using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;
using Animancer;
using XLua;

namespace SOC.GamePlay
{
    [LuaCallCSharp]
    [RequireComponent(typeof(BaseResLoaderAsyncMono))]
    public class SkillManager : ILuaBinder
    {
        private AnimancerComponent m_AnimancerComponent = null;

        /// <summary>
        /// ע��ļ����ࣨ��ӦLua����һ������֧�ֶ��Layer,һ��Layer֧�ֶ��State(ÿ��Layer����State����)
        /// </summary>
        public string[] RegisterSkillLuaClassNames = null;

        public AnimancerComponent Animancer {
            get {
                if (m_AnimancerComponent == null)
                    m_AnimancerComponent = GetComponentInChildren<AnimancerComponent>();
                return m_AnimancerComponent;
            }
        }

        [DoNotGen]

        protected new void Awake() {
            SelfTarget = this; // ǿ������Ϊ�Լ�
            base.Awake();
        }
    }
}
