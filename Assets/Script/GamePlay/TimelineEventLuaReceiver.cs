using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SOC.GamePlay
{
    [System.Serializable]
    public class TimeLuaReceiveEvent
    {
        [SerializeField]
        List<SignalAsset> m_Signals = new List<SignalAsset>();

        [SerializeField]
        List<string> m_LuaEventNames = new List<string>();

        public bool TryGetValue(SignalAsset key, out string eventName)
        {
            eventName = string.Empty;
            if (key == null)
                return false;
            int index = m_Signals.IndexOf(key);
            if (index < 0)
                return false;
            if (index < m_LuaEventNames.Count)
            {
                eventName = m_LuaEventNames[index];
                return true;
            }
            return false;
        }
    }

    [RequireComponent(typeof(ILuaBinder))]

    public class TimelineEventLuaReceiver : SignalReceiver, INotificationReceiver
    {
        private ILuaBinder m_LuaBinder = null;

        [SerializeField]
        TimeLuaReceiveEvent m_LuaEvents = new TimeLuaReceiveEvent();

        protected ILuaBinder LuaBinder
        {
            get
            {
                if (m_LuaBinder == null)
                    m_LuaBinder = GetComponent<ILuaBinder>();
                return m_LuaBinder;
            }
        }

        public void TriggerLuaEvent(System.Object context)
        {
            var luaBinder = this.LuaBinder;
            if (luaBinder != null)
            {
                luaBinder.CallCustomLuaFunc("OnTriggerLuaEvent", luaBinder.LuaSelf, context);
            }
        }

        public new void OnNotify(Playable origin, INotification notification, object context)
        {
            base.OnNotify(origin, notification, context);
            var luaBinder = this.LuaBinder;
            if (luaBinder != null)
            {
                var signal = notification as SignalEmitter;
                if (signal != null && signal.asset != null)
                {
                    string evtName;
                    if (m_LuaEvents.TryGetValue(signal.asset, out evtName) && !string.IsNullOrEmpty(evtName))
                    {
                        luaBinder.SignalReceiver_OnNotify_Lua(evtName, origin, notification, context);
                    }
                }
            }
        }
    }

}
