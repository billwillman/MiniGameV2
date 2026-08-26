using System;
using Puerts;
using UnityEngine;

namespace SOC.GamePlay
{
    public class ITSBinder : BaseMonoBehaviour
    {
        public string TsPath = string.Empty;

        [HideInInspector] public Action JsAwake;
        [HideInInspector] public Action JsStart;
        [HideInInspector] public Action JsUpdate;
        [HideInInspector] public Action JsFixedUpdate;
        [HideInInspector] public Action JsOnDestroy;

        void Awake()
        {
            LoadTs();
            InvokeJs(JsAwake);
        }

        void Start()
        {
            InvokeJs(JsStart);
        }

        void Update()
        {
            InvokeJs(JsUpdate);
        }

        void FixedUpdate()
        {
            InvokeJs(JsFixedUpdate);
        }

        protected override void OnInternalDestroyed()
        {
            InvokeJs(JsOnDestroy);
            ClearActions();
        }

        void LoadTs()
        {
            if (string.IsNullOrEmpty(TsPath))
            {
                Debug.LogError("[ITSBinder] TsPath is empty on " + name);
                return;
            }

            var env = JsGameStart.JsEnv;
            if (env == null)
            {
                Debug.LogError("[ITSBinder] JsGameStart.JsEnv is null on " + name);
                return;
            }

            try
            {
                ScriptObject mod = env.ExecuteModule(TsPath);
                if (mod == null)
                {
                    Debug.LogError("[ITSBinder] ExecuteModule returned null: " + TsPath);
                    return;
                }

                Action<ITSBinder> init = null;
                try
                {
                    init = mod.Get<Action<ITSBinder>>("init");
                }
                catch (Exception e)
                {
                    Debug.LogError("[ITSBinder] Failed to get export 'init' from " + TsPath + ": " + e);
                    return;
                }

                if (init == null)
                {
                    Debug.LogError("[ITSBinder] Module has no export 'init': " + TsPath);
                    return;
                }

                init(this);
            }
            catch (Exception e)
            {
                Debug.LogError("[ITSBinder] ExecuteModule failed: " + TsPath + " : " + e);
            }
        }

        void InvokeJs(Action fn)
        {
            if (JsGameStart.JsEnv == null)
            {
                ClearActions();
                return;
            }
            if (fn == null)
                return;
            try
            {
                fn();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void ClearActions()
        {
            JsAwake = null;
            JsStart = null;
            JsUpdate = null;
            JsFixedUpdate = null;
            JsOnDestroy = null;
        }
    }
}
