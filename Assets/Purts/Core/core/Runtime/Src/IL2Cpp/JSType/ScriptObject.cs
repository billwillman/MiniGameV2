/*
* Tencent is pleased to support the open source community by making Puerts available.
* Copyright (C) 2020 Tencent.  All rights reserved.
* Puerts is licensed under the BSD 3-Clause License, except for the third-party components listed in the file 'LICENSE' which may be subject to their corresponding license terms. 
* This file is subject to the terms and conditions defined in file 'LICENSE', which is part of this source code package.
*/

#if UNITY_2020_1_OR_NEWER
#if !PUERTS_DISABLE_IL2CPP_OPTIMIZATION && ENABLE_IL2CPP

using System;
using System.Runtime.CompilerServices;

namespace Puerts
{
    [UnityEngine.Scripting.Preserve]
    public class ScriptObject
    {
        IntPtr apis; // PObjectRefInfo first ptr
        IntPtr valueRef;
        IntPtr nativeJsEnv;

        // IL2CPP InternalCall stubs: bodies never run in Player. Native impl is
        // ScriptObjectGetValue / ScriptObjectSetValue / ScriptObjectSetValues in
        // Puerts_il2cpp.cpp, registered by InitialPuerts.
        [MethodImpl(MethodImplOptions.InternalCall)]
        object GetValue(IntPtr apis, string key, Type resultType)
        {
            throw new NotImplementedException();
        }

        [UnityEngine.Scripting.Preserve]
        [MethodImpl(MethodImplOptions.InternalCall)]
        void SetValue(IntPtr apis, string key, object value)
        {
            throw new NotImplementedException();
        }

        [UnityEngine.Scripting.Preserve]
        [MethodImpl(MethodImplOptions.InternalCall)]
        void SetValues(IntPtr apis, string[] keys, object[] values)
        {
            throw new NotImplementedException();
        }

        public T Get<T>(string key) 
        {
            return (T)GetValue(apis, key, typeof(T));
        }

        [UnityEngine.Scripting.Preserve]
        public void Set(string key, object value)
        {
            SetValue(apis, key, value);
        }

        [UnityEngine.Scripting.Preserve]
        public void Set(string[] keys, object[] values)
        {
            if (keys == null)
                throw new ArgumentNullException("keys");
            if (values == null)
                throw new ArgumentNullException("values");
            if (keys.Length != values.Length)
                throw new ArgumentException("keys and values length mismatch");
            if (keys.Length == 0)
                return;
            SetValues(apis, keys, values);
        }

        ~ScriptObject()
        {
            Puerts.NativeAPI.AddPendingKillScriptObjects(apis, nativeJsEnv, valueRef);
        }
    }
}

#endif
#endif