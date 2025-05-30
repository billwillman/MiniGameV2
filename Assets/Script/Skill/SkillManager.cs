using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.VisualScripting;
using Animancer;
using XLua;
using System.Diagnostics.Tracing;

namespace SOC.GamePlay
{
    [LuaCallCSharp]
    [RequireComponent(typeof(BaseResLoaderAsyncMono), typeof(TimelineEventLuaReceiver), typeof(AudioSource))]
    [RequireComponent(typeof(PawnNetworkObject))]
    public class SkillManager : ILuaBinder, ICustomLoaderEvent
    {
        private AnimancerComponent m_AnimancerComponent = null;
        private BaseResLoaderAsyncMono m_Loader = null;
        private AudioSource m_AudioSource = null;
        private TimelineEventLuaReceiver m_LuaEventReceiver;
        private PawnNetworkObject m_PawnObj = null;

        public string LuaStateRootPath = string.Empty;

        [BlackList]
        public string[] m_RegisterActions = null;
        [BlackList]
        public string m_ActionAssetRootPath = null;
        [BlackList]
        public bool m_CallLuaActionLoadedEvt = false;

        [BlackList]
        public MonoBehaviour CustomLoaderBehaviour {
            get {
                return this;
            }
        }

        public PawnNetworkObject PawnObj
        {
            get
            {
                if (m_PawnObj == null)
                    m_PawnObj = GetComponent<PawnNetworkObject>();
                return m_PawnObj;
            }
        }

        public bool IsLocalOwner
        {
            get
            {
                var pawn = this.PawnObj;
                if (pawn == null)
                    return false;
                bool ret = pawn.IsOwner;
                return ret;
            }

        }

        public void AddStateEvent(AnimancerState state, string evtName, Action evt)
        {
            if (state != null && !string.IsNullOrEmpty(evtName) && evt != null)
            {
                state.Events.Remove(evtName);
                state.Events.AddCallback(evtName, evt);
            }
        }


        // https://kybernetik.com.au/animancer/docs/manual/events/
        public void AnimationState_BindEvents(AnimancerState state, AnimationClip clip, bool isClearClipEvents = false)
        {
            if (state != null && clip != null)
            {
                state.Events.AddAllEvents(clip);
                if (isClearClipEvents)
                    clip.events = null;
            }
        }

        public void AnimationState_BindAllEvents(ClipState state)
        {
            if (state != null && state.Clip != null)
            {
                state.Events.Clear();
                AnimationState_BindEvents(state, state.Clip, true);
            }
        }

        public void AnimationState_ClearAllEvents(AnimationClip clip)
        {
            if (clip != null)
                clip.events = null;
        }

        public void AnimationState_ClearAllEvents(ClipState state, bool isClearAnimancerEvent = false)
        {
            if (state != null)
            {
                if (isClearAnimancerEvent)
                    state.Events.Clear();
                if (state != null)
                {
                    AnimationState_ClearAllEvents(state.Clip);
                }
            }
        }

        public void AnimationState_ClearAllEvents(ControllerState state, bool isClearAnimancerEvent = false)
        {
            if (state != null)
            {
                if (isClearAnimancerEvent)
                    state.Events.Clear();
                var controller = state.Controller;
                if (controller != null)
                {
                    var clips = controller.animationClips;
                    if (clips != null)
                    {
                        for (int i = 0; i < clips.Length; ++i)
                        {
                            AnimationState_ClearAllEvents(clips[i]);
                        }
                    }
                }
            }
        }

        public void AnimationState_BindAllEvents(ControllerState state)
        {
            if (state != null)
            {
                state.Events.Clear();
                var controller = state.Controller;
                if (controller != null)
                {
                    var clips = controller.animationClips;
                    if (clips != null)
                    {
                        for (int i = 0; i < clips.Length; ++i)
                        {
                            var clip = clips[i];
                            if (clip != null)
                            {
                                var events = clip.events;
                                if (events != null && events.Length > 0)
                                {
                                    AnimationState_BindEvents(state, clip, true);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void RemoveStateEvent(AnimancerState state, string evtName)
        {
            if (state != null && !string.IsNullOrEmpty(evtName))
            {
                state.Events.Remove(evtName);
            }
        }

        public void RemoveStateAllEvents(AnimancerState state)
        {
            if (state != null)
            {
                state.Events.Clear();
            }
        }

        public AudioSource Audio
        {
            get
            {
                if (m_AudioSource == null)
                    m_AudioSource = GetComponent<AudioSource>();
                return m_AudioSource;
            }
        }

        public TimelineEventLuaReceiver LuaEventReceiver
        {
            get
            {
                if (m_LuaEventReceiver == null)
                    m_LuaEventReceiver = GetComponent<TimelineEventLuaReceiver>();
                return m_LuaEventReceiver;
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
                        // 调用Lua加载回调
                        if (m_CallLuaActionLoadedEvt)
                            CallCustomLuaFunc("OnScriptObjectLoaded", this.LuaSelf, resName, translition);
                        else
                            this.Animancer.Play(translition); // 默认行为
                        //----------
                    } else
                        return false; // 暂时不支持其他类型
                    return true;
                default:
                    return false;
            }
        }

        public AnimancerState PlayAction(string actionName, Action onEnd = null, int layerIndex = -1)
        {
            var playable = this.Animancer;
            if (playable == null || m_SkillAssetMap == null)
                return null;
            AnimancerTransitionAssetBase action;
            if (m_SkillAssetMap.TryGetValue(actionName, out action) && (action != null))
            {
                if (layerIndex < 0)
                {
                    var state = playable.Play(action);
                    if (onEnd != null)
                    {
                        state.Events.OnEnd = onEnd;
                        state.Events.NormalizedEndTime = 1.0f;
                    }
                    return state;
                } else
                {
                    if (playable.Layers == null || layerIndex >= playable.Layers.Count)
                        return null;
                    var layer = playable.Layers[layerIndex];
                    if (layer == null)
                        return null;
                    var state = layer.Play(action);
                    if (onEnd != null)
                    {
                        state.Events.OnEnd = onEnd;
                        state.Events.NormalizedEndTime = 1.0f;
                    }
                    return state;
                }
            }
            return null;
        }

        public AnimancerState PlayAction(string actionName, float fadeDuration, Action onEnd = null, FadeMode fadeMode = default, int layerIndex = -1)
        {
            var playable = this.Animancer;
            if (playable == null || m_SkillAssetMap == null)
                return null;
            AnimancerTransitionAssetBase action;
            if (m_SkillAssetMap.TryGetValue(actionName, out action) && (action != null))
            {
                if (layerIndex < 0)
                {
                    var state = playable.Play(action, fadeDuration, fadeMode);
                    if (onEnd != null)
                        state.Events.OnEnd = onEnd;
                    return state;
                } else
                {
                    if (playable.Layers == null || layerIndex >= playable.Layers.Count)
                        return null;
                    var layer = playable.Layers[layerIndex];
                    if (layer == null)
                        return null;
                    var state = layer.Play(action, fadeDuration, fadeMode);
                    if (onEnd != null)
                        state.Events.OnEnd = onEnd;
                    return state;
                }
            }
            return null;
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

        private bool m_InitLoadAllActions = false;
        [BlackList]
        private bool m_IsRunStarted = false;

        public bool LoadAllRegisterActions(int loadPriority = 0) {
            // m_IsRunStarted 是因为在LUA侧 Ctor里会调用加载动作，但会导致第二次加载立马到，这里必须 LoadAllRegisterActions 在Start之后执行
            if (m_RegisterActions == null || !m_IsRunStarted)
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
                string fileName = string.Format("{0}{1}.asset", rootPath, skillName);
                Debug.LogFormat("[RegisterSkills] Start LoadAsync: {0}", fileName);
                if (!loader.LoadScriptObjectAsync(fileName, this, skillName, _cAnimancerResTag, loadPriority)) {
                    Debug.LogErrorFormat("[RegisterSkills] Error: {0}", fileName);
                }
            }

            m_InitLoadAllActions = true;
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
            m_IsRunStarted = true;
            if (!m_InitLoadAllActions)
            {
                LoadAllRegisterActions();
            }
            base.Start();
        }
    }
}
