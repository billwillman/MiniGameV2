using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class CustomDropList : Dropdown
{

    protected InputSystemUIInputModule NewUIInputModule
    {
        get
        {
            if (EventSystem.current == null)
                return null;
            var ret = EventSystem.current.currentInputModule as InputSystemUIInputModule;
            return ret;
        }
    }

    protected override void DestroyItem(DropdownItem item)
    {
        // 处理选择
        var UIInputModule = this.NewUIInputModule;
        if (UIInputModule != null)
        {
            
        }
        // -----------
        base.DestroyItem(item);
    }

    protected override void DestroyDropdownList(GameObject dropdownList)
    {
        // 处理选择
        var UIInputModule = this.NewUIInputModule;
        if (UIInputModule != null)
        {

        }
        // -----------
        base.DestroyDropdownList(dropdownList);
    }
}
