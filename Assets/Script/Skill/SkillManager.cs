using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Animancer;

namespace SOC.GamePlay
{
    public class SkillManager : ILuaBinder
    {
        private AnimancerComponent m_AnimancerComponent = null;
        public AnimancerComponent Animancer {
            get {
                if (m_AnimancerComponent == null)
                    m_AnimancerComponent = GetComponentInChildren<AnimancerComponent>();
                return m_AnimancerComponent;
            }
        }
    }
}
