using System;
using System.Collections;
using System.Collections.Generic;
using OD;
using Unity.Netcode;
using UnityEngine;

namespace SOC.GamePlay.Attribute
{

   public class AttributeGroup<T>: UserNetworkVariableSerialization<AttributeGroup<T>>
    {
        private OrderedDictionary<ushort, T> m_AttributeMap = new OrderedDictionary<ushort, T>();
        public OrderedDictionary<ushort, T> AttributeMap
        {
            get
            {
                return m_AttributeMap;
            }
        }

        public void NetworkWrite(FastBufferWriter writer)
        {
            ushort Count = (ushort)m_AttributeMap.Count;
            NetworkVariableSerialization<ushort>.Write(writer, ref Count);
            foreach (var iter in AttributeMap)
            {
                writer.WriteValue<ushort>(iter.Key);
                var Value = iter.Value;
                NetworkVariableSerialization<T>.Write(writer, ref Value);
            }
        }
    }

    public class IntAttributeGroup: AttributeGroup<int>
    {

        static IntAttributeGroup()
        {
            WriteValue = OnWriteValue;
        }

        private static void OnWriteValue(FastBufferWriter writer, in AttributeGroup<int> value)
        {
            
        }
    }
}
