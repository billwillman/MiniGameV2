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

        [BlackList]
        public string[] m_RegisterActions = null;
        [BlackList]
        public string m_ActionAssetRootPath = null;

        [BlackList]
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
                    if (m_RegisterActions == null || string.IsNullOrEmpty(resName))
                        return false;
                    int index = System.Array.IndexOf(m_RegisterActions, resName);
                    if (index < 0)
                        return false;
                   // Debug.Log(targetRes);
                    if (tag == _cAnimancerResTag) {
                        AnimancerTransitionAssetBase translition = targetRes as AnimancerTransitionAssetBase;
                        if (translition == null) {
                            return false;
                        }
                        m_SkillAssetMap[resName] = translition;
                        Debug.Log("[SkillManager] SkillLoaded: " + resName);
                    } else
                        return false; // 暂时不支持其他类型
                    return true;
                default:
                    return false;
            }
        }

        public AnimancerComponent Animancer {
            get {
                if (m_AnimancerComponent == null)
                    m_AnimancerComponent = GetComponentInChildren<AnimancerComponent>();
                return m_AnimancerComponent;
            }
        }

        static readonly string _cAnimancerResTag = "Animancer_Assets";

        public bool RegisterActions(LuaTable actionNames, bool isClearAll = false, int loadPriority = 0) {
            if (isClearAll)
                UnRegisterAllActions();
            if (m_RegisterActions == null) {
                if (actionNames != null && actionNames.Length > 0) {
                    m_RegisterActions = new string[actionNames.Length];
                    for (int i = 1; i <= actionNames.Length; ++i) {
                        string value;
                        actionNames.Get<int, string>(i, out value);
                        m_RegisterActions[i - 1] = value;
                    }
                } else
                    return false;
            } else {
                var newLuaClasses = new List<string>();
                for (int i = 1; i <= actionNames.Length; ++i) {
                    string value;
                    actionNames.Get<int, string>(i, out value);
                    if (newLuaClasses.IndexOf(value) < 0 && System.Array.IndexOf(m_RegisterActions, value) < 0)
                    {
                        newLuaClasses.Add(value);
                    }
                }
                m_RegisterActions.AddRange(newLuaClasses);
            }

            // 加载资源
            LoadAllRegisterActions();
            // ------------
            return true;
        }

        // 清理掉技能
        public void UnRegisterActions(LuaTable actionNames) {
            if (actionNames == null)
                return;
            int newLen;
            if (m_RegisterActions != null)
                newLen = m_RegisterActions.Length;
            else
                newLen = 0;
            HashSet<string> removeMap = null;
            for (int i = 1; i <= actionNames.Length; ++i) {
                string skillName;
                actionNames.Get<int, string>(i, out skillName);
                if (!string.IsNullOrEmpty(skillName)) {
                    if (m_RegisterActions != null) {
                        int index = System.Array.IndexOf(m_RegisterActions, skillName);
                        if (index >= 0) {
                            if (removeMap == null)
                            {
                                removeMap = new HashSet<string>();
                            } else if (removeMap.Contains(skillName))
                                continue;
                            removeMap.Add(skillName);
                            m_RegisterActions[index] = m_RegisterActions[newLen - 1];
                            --newLen;
                        }
                    }
                    if (m_SkillAssetMap.Remove(skillName)) {
                        var loader = this.Loader;
                        if (loader != null) {
                            loader.ClearScriptObject(this, skillName, _cAnimancerResTag);
                        }
                    }
                }
            }

            if (m_RegisterActions != null)
                System.Array.Resize<string>(ref m_RegisterActions, newLen);

        }

        public void UnRegisterAllActions() {
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
            m_RegisterActions = null;
        }

        public bool LoadAllRegisterActions(int loadPriority = 0) {
            if (m_RegisterActions == null)
                return false;
            // 加载资源
            var loader = this.Loader;
            if (loader == null)
                return false;
            string rootPath = this.m_ActionAssetRootPath;
            if (string.IsNullOrEmpty(rootPath))
                return false;
            if (!rootPath.EndsWith('/'))
                rootPath += "/";
            foreach (var skillName in m_RegisterActions) {
                if (string.IsNullOrEmpty(skillName) || m_SkillAssetMap.ContainsKey(skillName))
                    continue;
                string fileName = string.Format("{0}_{1}/{1}_C.asset", rootPath, skillName);
                Debug.LogFormat("[RegisterSkills] Start LoadAsync: {0:D}", fileName);
                if (!loader.LoadScriptObjectAsync(fileName, this, skillName, _cAnimancerResTag, loadPriority)) {
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

        [BlackList]
        protected new void Awake() {
            SelfTarget = this; // 强制设置为自己
            base.Awake();
        }

        [BlackList]
        protected new void Start() {
            LoadAllRegisterActions();
            base.Start();
        }
    }
}
