using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.Serialization;

namespace UnityEngine.InputSystem.UI
{

    public class CustomDropList : Dropdown
    {

        protected static InputSystemUIInputModule NewUIInputModule
        {
            get
            {
                if (EventSystem.current == null)
                    return null;
                var ret = EventSystem.current.currentInputModule as InputSystemUIInputModule;
                return ret;
            }
        }

        private static void CheckRemoveObj(UnityEngine.Object target)
        {
            if (target == null)
                return;
            var UIInputModule = NewUIInputModule;
            if (UIInputModule != null)
            {
                /*
                var t = typeof(InputSystemUIInputModule);
                var flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
                var field = t.GetField("m_PointerStates", flags);
                var items = field.GetValue(UIInputModule) as IEnumerable<PointerModel>;
                */
                UIInputModule.SendMessage("ResetPointers");
            }
        }

        protected override void DestroyItem(DropdownItem item)
        {
            // 处理选择
           // CheckRemoveObj(item);
            // -----------
            base.DestroyItem(item);
        }

        protected override void DestroyDropdownList(GameObject dropdownList)
        {
            // 处理选择
            CheckRemoveObj(dropdownList);
            // -----------
            base.DestroyDropdownList(dropdownList);
        }
    }
}