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
        public NetworkStringAttributeGroup[] NetworkStringGroupVars;
        public NetworkIntAttributeGroup[] NetworkIntGroupVars;
        public NetworkInt64AttributeGroup[] NetworkInt64GroupVars;
    }
}
