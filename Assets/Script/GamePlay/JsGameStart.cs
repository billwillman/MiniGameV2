using System;
using Puerts;
using UnityEngine;

namespace SOC.GamePlay
{
    [DefaultExecutionOrder(-100)]
    public class JsGameStart : MonoBehaviour
    {
        public static JsGameStart Instance = null;
        public static JsEnv JsEnv = null;

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
                JsEnv = new JsEnv(new DefaultLoader(), -1, BackendType.V8, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception e)
            {
                JsEnv = null;
                Debug.LogError("[JsGameStart] Failed to create V8 JsEnv: " + e);
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
