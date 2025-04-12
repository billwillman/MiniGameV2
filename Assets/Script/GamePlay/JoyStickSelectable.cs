using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SOC.GamePlay
{
    public class JoyStickSelectable : Selectable
    {
        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (OnPointerDownEvent != null)
                OnPointerDownEvent(eventData);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            if (OnPointerUpEvent != null)
                OnPointerUpEvent(eventData);
        }

        public Action<PointerEventData> OnPointerDownEvent
        {
            get;
            set;
        }

        public Action<PointerEventData> OnPointerUpEvent
        {
            get;
            set;
        }
    }
}
