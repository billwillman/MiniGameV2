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
    }

    public class AttributeGroupMap<T>: UserNetworkVariableSerialization<AttributeGroupMap<T>>
    {
        private OrderedDictionary<string, AttributeGroup<T>> m_IntValueBoard = new OrderedDictionary<string, AttributeGroup<T>>();
        public OrderedDictionary<string, AttributeGroup<T>> IntValueBoard
        {
            get
            {
                return m_IntValueBoard;
            }

        }
    }

    public class IntAttributeGroupMap: AttributeGroupMap<int>
    {
        static void OnWriteValueDelegate(FastBufferWriter writer, in AttributeGroupMap<int> value)
        {
            if (value == null)
                return;
            var board = value.IntValueBoard;
            if (board == null)
            {
                writer.WriteValue<ushort>(0); // 长度
                return;
            }
            writer.WriteValue<ushort>((ushort)board.Count); // 不要超过ushort
            foreach(var iter in board)
            {
                //writer.WriteValue<string>(iter.Key);
            }
        }

        static IntAttributeGroupMap()
        {
            WriteValue = OnWriteValueDelegate;
        }
    }
}
