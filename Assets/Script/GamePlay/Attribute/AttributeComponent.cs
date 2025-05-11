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
        public List<NetworkStringAttributeGroup> NetworkStringGroupVars;
        [System.NonSerialized]
        public List<NetworkIntAttributeGroup> NetworkIntGroupVars;
        [System.NonSerialized]
        public List<NetworkInt64AttributeGroup> NetworkInt64GroupVars;

        private void Awake()
        {
            if (AttributeGroupMeta != null)
            {

            }
        }
    }
}
