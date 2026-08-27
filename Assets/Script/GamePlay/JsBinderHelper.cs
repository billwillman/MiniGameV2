using System;
using Puerts;
using UnityEngine;

namespace SOC.GamePlay
{
    public static class JsBinderHelper
    {
        public static void SetJsProperty(ScriptObject obj, string key, UnityEngine.Object value)
        {
            if (obj == null || string.IsNullOrEmpty(key))
                return;
            try
            {
                obj.Set(key, value);
            }
            catch (Exception e)
            {
                Debug.LogError("[JsBinderHelper] SetJsProperty failed: " + e);
            }
        }

        public static void DisposeScriptObject(ref ScriptObject obj)
        {
            if (obj == null)
                return;
            var d = obj as IDisposable;
            if (d != null)
                d.Dispose();
            obj = null;
        }
    }
}
