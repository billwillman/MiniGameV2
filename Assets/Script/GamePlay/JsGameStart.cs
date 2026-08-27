using System;
using Puerts;
using UnityEngine;

namespace SOC.GamePlay
{
    [DefaultExecutionOrder(-100)]
    public class JsGameStart : MonoBehaviour
    {
        public static JsGameStart Instance = null;
        public static ScriptEnv JsEnv = null;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[JsGameStart] Duplicate instance. Keeping the first and disabling this one.");
                enabled = false;
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            try
            {
                JsEnv = new ScriptEnv(new BackendV8(new JsLoader()), -1);
            }
            catch (Exception e)
            {
                JsEnv = null;
                Debug.LogError("[JsGameStart] Failed to create V8 ScriptEnv: " + e);
            }
        }

        void Update()
        {
            if (JsEnv != null)
                JsEnv.Tick();
        }

        void OnDestroy()
        {
            if (Instance != this)
                return;
            if (JsEnv != null)
            {
                JsEnv.Dispose();
                JsEnv = null;
            }
            Instance = null;
        }
    }
}
