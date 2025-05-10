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
        // 数据面板(支持同步)
        public NetworkVariable<IntAttributeGroupMap> IntValueBoard = new NetworkVariable<IntAttributeGroupMap>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<AttributeGroupMap<float>> FloatValueBoard = new NetworkVariable<AttributeGroupMap<float>>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<AttributeGroupMap<string>> StringValueBoard = new NetworkVariable<AttributeGroupMap<string>>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<AttributeGroupMap<long>> LongValueBoard = new NetworkVariable<AttributeGroupMap<long>>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        //---------
    }
}
