using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SOC.GamePlay
{
    [RequireComponent(typeof(ILuaBinder))]

    public class TimelineEventLuaReceiver : SignalReceiver, INotificationReceiver
    {
        private ILuaBinder m_LuaBinder = null;

        protected ILuaBinder LuaBinder
        {
            get
            {
                if (m_LuaBinder == null)
                    m_LuaBinder = GetComponent<ILuaBinder>();
                return m_LuaBinder;
            }
        }

        public new void OnNotify(Playable origin, INotification notification, object context)
        {
            base.OnNotify(origin, notification, context);
            var luaBinder = this.LuaBinder;
            if (luaBinder != null)
            {
                luaBinder.SignalReceiver_OnNotify_Lua(origin, notification, context);
            }
        }
    }

}
