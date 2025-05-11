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
        
        public void NetworkRead(FastBufferReader reader)
        {
            ushort Count = 0;
            NetworkVariableSerialization<ushort>.Read(reader, ref Count);
            if (Count <= 0)
                m_AttributeMap.Clear();
            else
            {
                m_AttributeMap.Clear();
                for (int idx = 0; idx < Count; ++idx)
                {
                    ushort key = 0;
                    NetworkVariableSerialization<ushort>.Read(reader, ref key);
                    T value = default(T);
                    NetworkVariableSerialization<T>.Read(reader, ref value);
                    m_AttributeMap[key] = value;
                }
            }
        }

        public void NetworkDuplicateTo(AttributeGroup<T> other)
        {
            other.AttributeMap.Clear();
            foreach (var iter in m_AttributeMap)
            {
                ushort key = 0;
                NetworkVariableSerialization<ushort>.Duplicate((ushort)iter.Key, ref key);
                T value = default(T);
                NetworkVariableSerialization<T>.Duplicate(iter.Value, ref value);
                other.AttributeMap[key] = value;
            }
        }
    }

    public class IntAttributeGroup: AttributeGroup<int>
    {

        static IntAttributeGroup()
        {
            WriteValue = OnWriteValue;
            ReadValue = OnReadValue;
            DuplicateValue = OnDuplicateValue;
        }

        private static void OnDuplicateValue(in AttributeGroup<int> value, ref AttributeGroup<int> duplicatedValue)
        {
            value.NetworkDuplicateTo(duplicatedValue);
        }

        private static void OnReadValue(FastBufferReader reader, out AttributeGroup<int> value)
        {
            value = new AttributeGroup<int>();
            value.NetworkRead(reader);
        }

        private static void OnWriteValue(FastBufferWriter writer, in AttributeGroup<int> value)
        {
            value.NetworkWrite(writer);
        }
    }
}
