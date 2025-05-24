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

    [System.Serializable]
    public class Int64AttributeGroup: AttributeGroup<long>
    {
        static Int64AttributeGroup()
        {
            WriteValue = OnWriteValue;
            ReadValue = OnReadValue;
            DuplicateValue = OnDuplicateValue;
        }

        private static void OnDuplicateValue(in AttributeGroup<long> value, ref AttributeGroup<long> duplicatedValue)
        {
            value.NetworkDuplicateTo(duplicatedValue);
        }

        private static void OnReadValue(FastBufferReader reader, out AttributeGroup<long> value)
        {
            value = new AttributeGroup<long>();
            value.NetworkRead(reader);
        }

        private static void OnWriteValue(FastBufferWriter writer, in AttributeGroup<long> value)
        {
            value.NetworkWrite(writer);
        }
    }

    [System.Serializable]
    public class StringAttributeGroup: AttributeGroup<string>
    {
        static StringAttributeGroup()
        {
            WriteValue = OnWriteValue;
            ReadValue = OnReadValue;
            DuplicateValue = OnDuplicateValue;
        }

        private static void OnDuplicateValue(in AttributeGroup<string> value, ref AttributeGroup<string> duplicatedValue)
        {
            value.NetworkDuplicateTo(duplicatedValue);
        }

        private static void OnReadValue(FastBufferReader reader, out AttributeGroup<string> value)
        {
            value = new AttributeGroup<string>();
            value.NetworkRead(reader);
        }

        private static void OnWriteValue(FastBufferWriter writer, in AttributeGroup<string> value)
        {
            value.NetworkWrite(writer);
        }
    }

    [System.Serializable]
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

    [System.Serializable]
    public class NetworkIntAttributeGroup: NetworkVariable<IntAttributeGroup>
    {
        // new IntAttributeGroup()
        public NetworkIntAttributeGroup(): base(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server)
        {
            this.Value = new IntAttributeGroup();
        }
    }

    [System.Serializable]
    public class NetworkInt64AttributeGroup: NetworkVariable<Int64AttributeGroup>
    {
        public NetworkInt64AttributeGroup(): base(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server)
        {
            this.Value = new Int64AttributeGroup();
        }
    }

    [System.Serializable]
    public class NetworkStringAttributeGroup: NetworkVariable<StringAttributeGroup>
    {
        public NetworkStringAttributeGroup(): base(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server)
        {
            this.Value = new StringAttributeGroup();
        }
    }

    [System.Serializable]
    public enum NetworkAttributeType
    {
        Int = 0,
        Int64 = 1,
        String = 2,
    }

    [System.Serializable]
    public class NetworkAttributeMeta
    {
        public string AttributeName = string.Empty;
        public int IntDefaultValue = 0;
        public long Int64DefaultValue = 0;
        public string StringDefaultValue = string.Empty;
    }

    [System.Serializable]
    public class NetworkAttributeGroupMeta
    {
        public string AttributeGroupName = string.Empty;
        public NetworkAttributeType AttributeType = NetworkAttributeType.Int;
        public NetworkAttributeMeta[] Attributes = null;
        public bool bRepNotify = false;
        public NetworkVariable<IntAttributeGroup>.OnValueChangedDelegate OnIntGroupValueChanged;
        public NetworkVariable<Int64AttributeGroup>.OnValueChangedDelegate OnInt64GroupValueChanged;
        public NetworkVariable<StringAttributeGroup>.OnValueChangedDelegate OnStringGroupValueChanged;
    }
}
