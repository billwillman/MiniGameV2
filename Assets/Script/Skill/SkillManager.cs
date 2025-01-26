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
        /// 注册的技能类（对应Lua），一个技能支持多个Layer,一个Layer支持多个State(每个Layer可以State独立)
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
            SelfTarget = this; // 强制设置为自己
            base.Awake();
        }
    }
}
