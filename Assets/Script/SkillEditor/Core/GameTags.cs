using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;
using Netcode;
using Unity.Netcode;
using SOC.GamePlay;
using SOC.GamePlay.Attribute;

namespace GAS
{

    public class GameTags : NetworkBehaviour
    {
        // 动态的
        private NetworkVariable<Int64AttributeGroup> m_TagValues = new NetworkVariable<Int64AttributeGroup>();

        private void Awake()
        {
            m_TagValues.bRepNotify = true;
            if (!GameStart.IsDS)
            {
                m_TagValues.OnValueChanged = OnRep_TagValues;
            }
        }

        void OnRep_TagValues(Int64AttributeGroup oldValue, Int64AttributeGroup newValue)
        { }

        public bool GetTagValue(ushort enumId, out long tagValue)
        {
            return m_TagValues.Value.AttributeMap.TryGetValue(enumId, out tagValue);
        }

        public bool AddTag(ushort enumId, string tagPath)
        {
            if (string.IsNullOrEmpty(tagPath))
                return false;
            var instance = TagManager.GetInstance();
            if (instance == null)
                return false;
            var tagNode = instance.GetTagNode(tagPath);
            if (tagNode == null)
                return false;
            long tagValue;
            if (m_TagValues.Value.AttributeMap.TryGetValue(enumId, out tagValue))
            {
                tagValue = tagValue | (long)tagNode.mask;
            } else
                tagValue = (long)tagNode.mask;
            m_TagValues.Value.AttributeMap[enumId] = tagValue;
            m_TagValues.SetDirty(true);
            return true;
        }

        // 是否有这个TAG
        public bool HasTag(ushort enumId, string tagPath)
        {
            if (string.IsNullOrEmpty(tagPath))
                return false;
            long tagMask;
            if (!GetTagValue(enumId, out tagMask))
                return false;
            var instance = TagManager.GetInstance();
            if (instance == null)
                return false;
            var tagNode = instance.GetTagNode(tagPath);
            if (tagNode == null)
                return false;
            bool ret = ((ulong)tagMask & tagNode.mask) != 0;
            return ret;
        }
    }
}
