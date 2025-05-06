using System.Collections;
using System.Collections.Generic;
using OD;
using Unity.Netcode;
using UnityEngine;

namespace SOC.GamePlay.Attribute
{

   public class AttributeGroup<T>: UserNetworkVariableSerialization<T>
    {
        public OrderedDictionary<ushort, T> AttributeMap = new OrderedDictionary<ushort, T>();
    }

    public class AttributeGroupMap<T>: UserNetworkVariableSerialization<T>
    {
        public OrderedDictionary<string, AttributeGroup<T>> IntValueBoard = new OrderedDictionary<string, AttributeGroup<T>>();
    }
}
