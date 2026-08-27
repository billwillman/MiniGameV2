using System;
using Puerts;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SOC.GamePlay
{
    [DefaultExecutionOrder(-50)]
    public sealed class TSUIBinder : BaseMonoBehaviour
    {
        public UIBehaviour[] m_BindControls = null;

        ScriptObject m_JsSelf;
        ScriptObject m_Bp;

        public void InitRegisterControls(ScriptObject jsSelf)
        {
            ReleaseJsCache();

            if (jsSelf == null)
            {
                Debug.LogError("[TSUIBinder] InitRegisterControls jsSelf is null on " + name);
                return;
            }

            ScriptObject bp = null;
            try
            {
                bp = jsSelf.Get<ScriptObject>("bp");
            }
            catch (Exception e)
            {
                Debug.LogError("[TSUIBinder] Failed to get jsSelf.bp: " + e);
                return;
            }

            if (bp == null)
            {
                Debug.LogError("[TSUIBinder] TS class instance has no bp. Set this.bp = {} before InitRegisterControls.");
                return;
            }

            m_JsSelf = jsSelf;
            m_Bp = bp;
            WriteBindControls(bp, false);
        }

        protected override void OnInternalDestroyed()
        {
            ReleaseJsCache();
        }

        void WriteBindControls(ScriptObject bp, bool clear)
        {
            Canvas canvas = gameObject.GetComponent<Canvas>();
            if (canvas != null)
                JsBinderHelper.SetJsProperty(bp, "_Canvas", clear ? null : canvas);

            if (m_BindControls == null)
                return;
            for (int i = 0; i < m_BindControls.Length; ++i)
            {
                var control = m_BindControls[i];
                if (control == null)
                    continue;
                JsBinderHelper.SetJsProperty(bp, control.gameObject.name, clear ? null : control);
            }
        }

        void ReleaseJsCache()
        {
            if (JsGameStart.JsEnv != null)
            {
                try
                {
                    if (m_Bp != null)
                        WriteBindControls(m_Bp, true);
                    if (m_JsSelf != null)
                        JsBinderHelper.SetJsProperty(m_JsSelf, "bp", null);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            JsBinderHelper.DisposeScriptObject(ref m_Bp);
            JsBinderHelper.DisposeScriptObject(ref m_JsSelf);
        }
    }
}
