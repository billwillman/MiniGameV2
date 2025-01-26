using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.VisualScripting;
using Animancer;
using XLua;

namespace SOC.GamePlay
{
    [LuaCallCSharp]
    [RequireComponent(typeof(BaseResLoaderAsyncMono))]
    public class SkillManager : ILuaBinder, ICustomLoaderEvent
    {
        private AnimancerComponent m_AnimancerComponent = null;
        private BaseResLoaderAsyncMono m_Loader = null;

        /// <summary>
        /// 注册的技能类（对应Lua），一个技能支持多个Layer,一个Layer支持多个State(每个Layer可以State独立)
        /// </summary>
        public string[] RegisterSkillLuaClassNames = null;
        public string SkillAssetRoot = null;

        public MonoBehaviour CustomLoaderBehaviour {
            get {
                return this;
            }
        }

        protected Dictionary<string, AnimancerTransitionAssetBase> m_SkillAssetMap = new Dictionary<string, AnimancerTransitionAssetBase>();

        public bool OnLoaded(UnityEngine.Object targetRes, BaseResLoaderAsyncType asyncType, string resName, string tag) {
            if (targetRes == null)
                return false;
            switch (asyncType) {
                case BaseResLoaderAsyncType.ScriptObject:
                    if (RegisterSkillLuaClassNames  == null || string.IsNullOrEmpty(resName))
                        return false;
                    int index = System.Array.IndexOf(RegisterSkillLuaClassNames, resName);
                    if (index < 0)
                        return false;
                    if (tag == _cAnimancerResTag) {
                        AnimancerTransitionAssetBase translition = targetRes as AnimancerTransitionAssetBase;
                        if (translition == null)
                            return false;
                        m_SkillAssetMap[resName] = translition;
                    } else
                        return false; // 暂时不支持其他类型
                    return true;
                default:
                    return false;
            }
            return false;
        }

        public AnimancerComponent Animancer {
            get {
                if (m_AnimancerComponent == null)
                    m_AnimancerComponent = GetComponentInChildren<AnimancerComponent>();
                return m_AnimancerComponent;
            }
        }

        static readonly string _cAnimancerResTag = "Animancer_Assets";

        public bool RegisterSkills(LuaTable skillNames, bool isClearAll = false, int loadPriority = 0) {
            if (isClearAll)
                UnRegisterAllSkills();
            if (RegisterSkillLuaClassNames == null) {
                if (skillNames != null && skillNames.Length > 0) {
                    RegisterSkillLuaClassNames = new string[skillNames.Length];
                    for (int i = 1; i <= skillNames.Length; ++i) {
                        string value;
                        skillNames.Get<int, string>(i, out value);
                        RegisterSkillLuaClassNames[i - 1] = value;
                    }
                } else
                    return false;
            } else {
                var newLuaClasses = new string[skillNames.Length];
                for (int i = 1; i <= skillNames.Length; ++i) {
                    string value;
                    skillNames.Get<int, string>(i, out value);
                    newLuaClasses[i - 1] = value;
                }
                RegisterSkillLuaClassNames.AddRange(newLuaClasses);
            }

            // 加载资源
            LoadAllRegisterSkills();
            // ------------
            return true;
        }

        // 清理掉技能
        public void UnRegisterSkills(LuaTable skillNames) {
            
        }

        public void UnRegisterAllSkills() {
            var loader = this.Loader;
            if (loader == null)
                return;
            var iter = m_SkillAssetMap.GetEnumerator();
            while (iter.MoveNext()) {
                string resName = iter.Current.Key;
                loader.ClearScriptObject(this, resName, _cAnimancerResTag);
            }
            iter.Dispose();
            m_SkillAssetMap.Clear();
            RegisterSkillLuaClassNames = null;
        }

        public bool LoadAllRegisterSkills(int loadPriority = 0) {
            if (RegisterSkillLuaClassNames == null)
                return false;
            // 加载资源
            var loader = this.Loader;
            if (loader == null)
                return false;
            string rootPath = this.SkillAssetRoot;
            if (string.IsNullOrEmpty(rootPath))
                return false;
            if (!rootPath.EndsWith('/'))
                rootPath += "/";
            foreach (var skillName in RegisterSkillLuaClassNames) {
                if (string.IsNullOrEmpty(skillName) || m_SkillAssetMap.ContainsKey(skillName))
                    continue;
                string fileName = string.Format("{0}_{1}/{1}.asset", rootPath, name);
                if (!loader.LoadScriptObjectAsync(fileName, this, name, _cAnimancerResTag, loadPriority)) {
                    Debug.LogErrorFormat("[RegisterSkills] Error: {0:D}", fileName);
                }
            }
            return true;
        }

        public BaseResLoaderAsyncMono Loader {
            get {
                if (m_Loader == null)
                    m_Loader = GetComponent<BaseResLoaderAsyncMono>();
                return m_Loader;
            }
        }

        [DoNotGen]
        protected new void Awake() {
            SelfTarget = this; // 强制设置为自己
            base.Awake();
        }

        [DoNotGen]
        protected new void Start() {
            LoadAllRegisterSkills();
            base.Start();
        }
    }
}
