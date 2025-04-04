using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XLua;

namespace UnityEngine.InputSystem
{

    [XLua.LuaCallCSharp]
    public static class PlayerInputUtils
    {
        public static Vector2 GetVector2DCallbackContext(InputAction.CallbackContext context)
        {
            return context.ReadValue<Vector2>();
        }

        public static bool GetButtonCallbackContext(InputAction.CallbackContext context)
        {
            return context.ReadValueAsButton();
        }
    }
}
