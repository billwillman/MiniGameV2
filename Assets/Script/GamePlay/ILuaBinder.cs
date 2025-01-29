#define UNITY_INPUT

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XLua;
using UnityEngine.Playables;


#if UNITY_INPUT
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
#endif

namespace SOC.GamePlay
{
    public enum LuaEvent_MonoEventType
    {
        None = 0,
        Awake = 1,
        Start = 2,
        Update = 3,
        Destroyed = 4,
        FixedUpdate = 5,
    }

    [LuaCallCSharp]
    public class ILuaBinder : BaseMonoBehaviour
    {
        public string LuaPath = string.Empty;
        public MonoBehaviour SelfTarget = null;

        // LuaEvent_MonoEvent 用int减少GC
        private Dictionary<int, LuaFunction> m_LuaEventMap = null;
        private LuaTable m_LuaSelf = null;
        private LuaTable m_LuaClass = null;
        private Dictionary<string, LuaFunction> m_LuaCustomFuncs = null;

        [BlackList]
        public void SignalReceiver_OnNotify_Lua(string evtName, Playable origin, INotification notification, object context)
        {
            CallCustomLuaFunc(evtName, m_LuaSelf, context, origin);
        }

        // 需要获取的Lua的方法
        [BlackList]
        public string[] CustomLuaFunctionName = null;

        [BlackList]
        public bool bInitCustomLuaFunctionInStart = false;

        public LuaTable LuaSelf {
            get {
                return m_LuaSelf;
            }
        }

        public LuaTable LuaClass {
            get {
                return m_LuaClass;
            }
        }

        public void RegisterLuaEvent(int evtType, LuaFunction func) {
            if (m_LuaEventMap == null)
                m_LuaEventMap = new Dictionary<int, LuaFunction>();
            m_LuaEventMap[evtType] = func;
        }

        public System.Object[] CallCustomLuaFunc(string evtName, params System.Object[] param) {
            if (m_LuaCustomFuncs != null)
            {
                LuaFunction func;
                if (m_LuaCustomFuncs.TryGetValue(evtName, out func))
                {
                    if (func != null)
                        return func.Call(param);
                    return null;
                }
            }
            if (!bInitCustomLuaFunctionInStart)
            {
                if (string.IsNullOrEmpty(evtName) || m_LuaSelf == null)
                    return null;
                LuaFunction func = m_LuaSelf.Get<LuaFunction>(evtName);
                if (func == null)
                    return null;
                if (m_LuaCustomFuncs == null)
                    m_LuaCustomFuncs = new Dictionary<string, LuaFunction>();
                m_LuaCustomFuncs[evtName] = func;
                return func.Call(param);
            }
            return null;
        }

#if UNITY_INPUT
        public static ReadOnlyArray<PlayerInput.ActionEvent> CreatePlayerInputActionEvents(PlayerInput.ActionEvent[] events, int index, int len) {
            ReadOnlyArray<PlayerInput.ActionEvent> ret = new ReadOnlyArray<PlayerInput.ActionEvent>(events, index, len);
            return ret;
        }

        public static ReadOnlyArray<PlayerInput.ActionEvent> CreatePlayerInputActionEvents(PlayerInput.ActionEvent[] events) {
            ReadOnlyArray<PlayerInput.ActionEvent> ret = new ReadOnlyArray<PlayerInput.ActionEvent>(events);
            return ret;
        }
#endif

        [BlackList]
        void DoDestroyLuaObject() {
            DisposeCustomLuaFuncs();
            if (m_LuaEventMap != null) {
                var iter = m_LuaEventMap.GetEnumerator();
                while (iter.MoveNext()) {
                    var func = iter.Current.Value;
                    if (func != null) {
                        func.Dispose();
                    }
                }
                iter.Dispose();
                m_LuaEventMap.Clear();
            }
            if (m_LuaSelf != null) {
                m_LuaSelf.Dispose();
                m_LuaSelf = null;
            }
            if (m_LuaClass != null) {
                m_LuaClass.Dispose();
                m_LuaClass = null;
            }
        }

        [BlackList]
        void LoadLua() {
            DoDestroyLuaObject();
            if (string.IsNullOrEmpty(LuaPath) || SelfTarget == null)
                return;
            var env = GameStart.EnvLua;
            if (env == null)
                return;
            var rets = env.DoString(string.Format("local module = require(\"{0}\")\n return module", LuaPath), SelfTarget.name);
            if (rets != null && rets.Length > 0) {
                LuaTable luaClass = rets[0] as LuaTable;
                if (luaClass != null) {
                    LuaFunction constructorFunc = luaClass.Get<LuaFunction>("New");
                    if (constructorFunc != null) {
                        m_LuaSelf = constructorFunc.Func<MonoBehaviour, ILuaBinder, LuaTable>(SelfTarget, this);
                        m_LuaClass = luaClass;
                    } else {
                        m_LuaSelf = luaClass;
                    }
                    InitCustomLuaFuncs();
                }
            }
        }

        [BlackList]
        void InitCustomLuaFuncs()
        {
            if (!bInitCustomLuaFunctionInStart || CustomLuaFunctionName == null || m_LuaSelf == null)
                return;
            for (int i = 0; i < CustomLuaFunctionName.Length; ++i)
            {
                string funcName = CustomLuaFunctionName[i];
                if (!string.IsNullOrEmpty(funcName))
                {
                    LuaFunction func = m_LuaSelf.Get<LuaFunction>(funcName);
                    if (func != null)
                    {
                        if (m_LuaCustomFuncs == null)
                            m_LuaCustomFuncs = new Dictionary<string, LuaFunction>();
                        m_LuaCustomFuncs[funcName] = func;
                    }
                }
            }
               
        }

        [BlackList]
        void DisposeCustomLuaFuncs()
        {
            if (m_LuaCustomFuncs == null)
                return;
            foreach(var iter in m_LuaCustomFuncs)
            {
                if (iter.Value != null)
                    iter.Value.Dispose();
            }
            m_LuaCustomFuncs.Clear();
            m_LuaCustomFuncs.TrimExcess();
        }

        [BlackList]
        private bool CallLuaFunc(LuaEvent_MonoEventType evtType) {
            if (m_LuaEventMap == null)
                return true;
            LuaFunction func;
            if (m_LuaEventMap.TryGetValue((int)evtType, out func) && func != null) {
                return func.Func<LuaTable, bool>(m_LuaSelf);
            }
            return true;
        }

        [BlackList]
        protected void Awake() {
            LoadLua();
            // 加载Lua
            if (CallLuaFunc(LuaEvent_MonoEventType.Awake)) {
                OnAwake();
            }
        }

        [BlackList]

        protected void Start() {
            if (CallLuaFunc(LuaEvent_MonoEventType.Start)) {
                OnStart();
            }
        }

        [BlackList]

        protected void Update() {
            if (CallLuaFunc(LuaEvent_MonoEventType.Update)) {
                OnUpdate();
            }
        }

        [BlackList]

        protected void FixedUpdate()
        {
            if (CallLuaFunc(LuaEvent_MonoEventType.FixedUpdate))
            {
                OnFixedUpdate();
            }
        }

        protected override void OnInternalDestroyed() {
            if (CallLuaFunc(LuaEvent_MonoEventType.Destroyed)) {
                OnDestroyed();
            }
            DoDestroyLuaObject();
        }

        [BlackList]
        public virtual void OnStart() { }
        [BlackList]
        public virtual void OnDestroyed() { }
        [BlackList]
        public virtual void OnUpdate() { }
		[BlackList]
        public virtual void OnAwake() { }
        [BlackList]
        public virtual void OnFixedUpdate() { }
    }
}
