using System;
using System.Collections;
using System.Collections.Generic;
using OD;
using SOC.GamePlay;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace SOC.GamePlay.Attribute
{
    public class AttributeComponent : BaseNetworkMono
    {
        public NetworkAttributeGroupMeta AttributeGroupMeta = null;

        [System.NonSerialized]
        public NetworkStringAttributeGroup[] NetworkStringGroupVars;
        [System.NonSerialized]
        public NetworkIntAttributeGroup[] NetworkIntGroupVars;
        [System.NonSerialized]
        public NetworkInt64AttributeGroup[] NetworkInt64GroupVars;
    }
}
