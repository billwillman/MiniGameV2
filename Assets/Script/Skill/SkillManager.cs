using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;
using Animancer;

namespace SOC.GamePlay
{
    public class SkillManager : ILuaBinder
    {
        private AnimancerComponent m_AnimancerComponent = null;

        /// <summary>
        /// ע��ļ����ࣨ��ӦLua����һ������֧�ֶ��Layer,һ��Layer֧�ֶ��State(ÿ��Layer����State����)
        /// </summary>
        public string[] RegisterSkillClasses = null;

        public AnimancerComponent Animancer {
            get {
                if (m_AnimancerComponent == null)
                    m_AnimancerComponent = GetComponentInChildren<AnimancerComponent>();
                return m_AnimancerComponent;
            }
        }
    }
}
