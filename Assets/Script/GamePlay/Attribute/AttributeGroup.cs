using System;
using System.Collections;
using System.Collections.Generic;
using OD;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

namespace SOC.GamePlay.Attribute
{

    public class AttributeGroup<T> : UserNetworkVariableSerialization<AttributeGroup<T>>
    {
        protected OrderedDictionary<ushort, T> m_AttributeMap = new OrderedDictionary<ushort, T>();
        public OrderedDictionary<ushort, T> AttributeMap
        {
            get
            {
                return m_AttributeMap;
            }
        }

        public static bool CompareAttributeGroup(AttributeGroup<T> a, AttributeGroup<T> b)
        {
            if (a == b)
                return true;
            if ((a == null && b != null) || (a != null && b == null))
                return false;
            var aAttr = a.AttributeMap;
            var bAttr = b.AttributeMap;
            if (aAttr.Count != bAttr.Count)
                return false;
            foreach (var iter in aAttr)
            {
                T bValue;
                if (!bAttr.TryGetValue(iter.Key, out bValue))
                    return false;
                if (!iter.Value.Equals(bValue))
                    return false;
            }
            return true;
        }

        public virtual void NetworkWrite(FastBufferWriter writer)
        {}

        public virtual void NetworkRead(FastBufferReader reader)
        {}

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
            UserNetworkVariableSerialization<Int64AttributeGroup>.DuplicateValue = OnDuplicateValue;
            UserNetworkVariableSerialization<Int64AttributeGroup>.ReadValue = OnReadValue;
            UserNetworkVariableSerialization<Int64AttributeGroup>.WriteValue = OnWriteValue;
            NetworkVariableSerialization<Int64AttributeGroup>.AreEqual = OnAreEqual;
        }

        public override void NetworkRead(FastBufferReader reader)
        {
            ushort Count;
            reader.ReadValue(out Count);
            if (Count <= 0)
                m_AttributeMap.Clear();
            else
            {
                m_AttributeMap.Clear();
                for (int idx = 0; idx < Count; ++idx)
                {
                    ushort key = 0;
                    reader.ReadValue(out key);
                    long value;
                    reader.ReadValue(out value);
                    m_AttributeMap[key] = value;
                }
            }
        }

        public override void NetworkWrite(FastBufferWriter writer)
        {
            ushort Count = (ushort)m_AttributeMap.Count;
            writer.WriteValue<ushort>(Count);
            foreach (var iter in AttributeMap)
            {
                writer.WriteValue<ushort>(iter.Key);
                var Value = iter.Value;
                writer.WriteValue(Value);
            }
        }

        private static bool OnAreEqual(ref Int64AttributeGroup a, ref Int64AttributeGroup b)
        {
            return CompareAttributeGroup(a, b);
        }

        private static void OnDuplicateValue(in Int64AttributeGroup value, ref Int64AttributeGroup duplicatedValue)
        {
            if (duplicatedValue == null)
                duplicatedValue = new Int64AttributeGroup();
            value.NetworkDuplicateTo(duplicatedValue);
        }

        private static void OnReadValue(FastBufferReader reader, ref Int64AttributeGroup value)
        {
            if (value == null)
                value = new Int64AttributeGroup();
            value.NetworkRead(reader);
        }

        private static void OnWriteValue(FastBufferWriter writer, in Int64AttributeGroup value)
        {
            value.NetworkWrite(writer);
        }

        private static void OnDuplicateValue(in AttributeGroup<long> value, ref AttributeGroup<long> duplicatedValue)
        {
            if (duplicatedValue == null)
                duplicatedValue = new AttributeGroup<long>();
            value.NetworkDuplicateTo(duplicatedValue);
        }

        private static void OnReadValue(FastBufferReader reader, ref AttributeGroup<long> value)
        {
            if (value == null)
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
            UserNetworkVariableSerialization<StringAttributeGroup>.DuplicateValue = OnDuplicateValue;
            UserNetworkVariableSerialization<StringAttributeGroup>.ReadValue = OnReadValue;
            UserNetworkVariableSerialization<StringAttributeGroup>.WriteValue = OnWriteValue;
            NetworkVariableSerialization<StringAttributeGroup>.AreEqual = OnAreEqual;
        }

        public override void NetworkRead(FastBufferReader reader)
        {
            ushort Count;
            reader.ReadValue(out Count);
            if (Count <= 0)
                m_AttributeMap.Clear();
            else
            {
                m_AttributeMap.Clear();
                for (int idx = 0; idx < Count; ++idx)
                {
                    ushort key = 0;
                    reader.ReadValue(out key);
                    string value;
                    reader.ReadValue(out value);
                    m_AttributeMap[key] = value;
                }
            }
        }

        public override void NetworkWrite(FastBufferWriter writer)
        {
            ushort Count = (ushort)m_AttributeMap.Count;
            writer.WriteValue<ushort>(Count);
            foreach (var iter in AttributeMap)
            {
                writer.WriteValue<ushort>(iter.Key);
                var Value = iter.Value;
                writer.WriteValue(Value);
            }
        }

        private static bool OnAreEqual(ref StringAttributeGroup a, ref StringAttributeGroup b)
        {
            return CompareAttributeGroup(a, b);
        }

        private static void OnDuplicateValue(in StringAttributeGroup value, ref StringAttributeGroup duplicatedValue)
        {
             if (duplicatedValue == null)
                duplicatedValue = new StringAttributeGroup();
            value.NetworkDuplicateTo(duplicatedValue);
        }

        private static void OnReadValue(FastBufferReader reader, ref StringAttributeGroup value)
        {
            if (value == null)
                value = new StringAttributeGroup();
            value.NetworkRead(reader);
        }

        private static void OnWriteValue(FastBufferWriter writer, in StringAttributeGroup value)
        {
            value.NetworkWrite(writer);
        }

        private static void OnDuplicateValue(in AttributeGroup<string> value, ref AttributeGroup<string> duplicatedValue)
        {
            if (duplicatedValue == null)
                duplicatedValue = new AttributeGroup<string>();
            value.NetworkDuplicateTo(duplicatedValue);
        }

        private static void OnReadValue(FastBufferReader reader, ref AttributeGroup<string> value)
        {
            if (value == null)
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
            UserNetworkVariableSerialization<IntAttributeGroup>.DuplicateValue = OnDuplicateValue;
            UserNetworkVariableSerialization<IntAttributeGroup>.WriteValue = OnWriteValue;
            UserNetworkVariableSerialization<IntAttributeGroup>.ReadValue = OnReadValue;
            NetworkVariableSerialization<IntAttributeGroup>.AreEqual = OnAreEqual;
        }

        public override void NetworkRead(FastBufferReader reader)
        {
            ushort Count;
            reader.ReadValue(out Count);
            if (Count <= 0)
                m_AttributeMap.Clear();
            else
            {
                m_AttributeMap.Clear();
                for (int idx = 0; idx < Count; ++idx)
                {
                    ushort key = 0;
                    reader.ReadValue(out key);
                    int value;
                    reader.ReadValue(out value);
                    m_AttributeMap[key] = value;
                }
            }
        }

        public override void NetworkWrite(FastBufferWriter writer)
        {
            ushort Count = (ushort)m_AttributeMap.Count;
            writer.WriteValue<ushort>(Count);
            foreach (var iter in AttributeMap)
            {
                writer.WriteValue<ushort>(iter.Key);
                var Value = iter.Value;
                writer.WriteValue(Value);
            }
        }

        private static bool OnAreEqual(ref IntAttributeGroup a, ref IntAttributeGroup b)
        {
            return CompareAttributeGroup(a, b);
        }

        private static void OnDuplicateValue(in IntAttributeGroup value, ref IntAttributeGroup duplicatedValue)
        {
            if (duplicatedValue == null)
                duplicatedValue = new IntAttributeGroup();
            value.NetworkDuplicateTo(duplicatedValue);
        }

        private static void OnWriteValue(FastBufferWriter writer, in IntAttributeGroup value)
        {
            value.NetworkWrite(writer);
        }

        private static void OnReadValue(FastBufferReader reader, ref IntAttributeGroup value)
        {
            if (value == null)
                value = new IntAttributeGroup();
            value.NetworkRead(reader);
        }

        private static void OnDuplicateValue(in AttributeGroup<int> value, ref AttributeGroup<int> duplicatedValue)
        {
            if (duplicatedValue == null)
                duplicatedValue = new AttributeGroup<int>();
            value.NetworkDuplicateTo(duplicatedValue);
        }

        private static void OnReadValue(FastBufferReader reader, ref AttributeGroup<int> value)
        {
            if (value == null)
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
        public NetworkIntAttributeGroup(): base(new IntAttributeGroup(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server)
        {}
    }

    [System.Serializable]
    public class NetworkInt64AttributeGroup: NetworkVariable<Int64AttributeGroup>
    {
        public NetworkInt64AttributeGroup(): base(new Int64AttributeGroup(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server)
        {}
    }

    [System.Serializable]
    public class NetworkStringAttributeGroup: NetworkVariable<StringAttributeGroup>
    {
        public NetworkStringAttributeGroup(): base(new StringAttributeGroup(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server)
        {}
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
