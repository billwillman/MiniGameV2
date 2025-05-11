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
        public StringAttributeGroup[] StringGroupVars;
        public IntAttributeGroup[] IntGroupVars;
        public Int64AttributeGroup[] Int64GroupVars;
    }
}
