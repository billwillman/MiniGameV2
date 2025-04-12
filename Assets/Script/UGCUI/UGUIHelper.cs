using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SOC.GamePlay
{
    [XLua.LuaCallCSharp]
    public static class UGUIHelper
    {
        public static void ApplyUILoadAnchorAndOffsetMin(Transform target, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (target == null)
                return;
            RectTransform target2 = target as RectTransform;
            if (target2 == null)
                return;
            target2.anchorMin = anchorMin;
            target2.anchorMax = anchorMax;
            target2.offsetMin = offsetMin;
            target2.offsetMax = offsetMax;
        }

        public static void RemoveAllListeners(UnityEvent evt)
        {
            if (evt == null)
                return;
            evt.RemoveAllListeners();
        }
    }
}
