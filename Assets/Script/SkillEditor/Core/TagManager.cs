using SOC.GamePlay;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    [System.Serializable]
    public class TagNode
    {
        public string name;
        [System.NonSerialized]
        public TagRootNode rootNode = null;
        public List<TagNode> childNode = null;
        [System.NonSerialized]
        public ulong mask = 0;
    }

    [System.Serializable]
    public class TagRootNode : TagNode
    {
        [System.NonSerialized]
        public ulong id; // 枚举
    }

    [Tooltip("GameTag总定义")]
    public class TagManager : SingetonMono<TagManager>
    {
        public List<TagRootNode> m_TagRootNodes = null;
        private Dictionary<string, TagNode> m_TagMap = new Dictionary<string, TagNode>();
        private Dictionary<ulong, TagRootNode> m_TagRootMap = new Dictionary<ulong, TagRootNode>(); // 枚举对应的TagRootNode

        public TagRootNode GetRootNode(ulong enumId)
        {
            TagRootNode ret;
            if (!m_TagRootMap.TryGetValue(enumId, out ret))
                ret = null;
            return ret;
        }

        public TagNode GetTagNode(string tagPathName)
        {
            if (string.IsNullOrEmpty(tagPathName))
                return null;
            TagNode ret;
            if (!m_TagMap.TryGetValue(tagPathName, out ret))
                ret = null;
            return ret;
        }

        // 最大4层TAG 4层, 每层16个子TAG
        private static readonly int _cMaxTag = 16;
        private static readonly int _cMaxLevel = 4;
        
        private ulong GetLevelMask(int level, out ulong parentMask, int subIndex = 0)
        {
            if (level >= _cMaxLevel || level < 0 || subIndex >= _cMaxTag || subIndex < 0)
            {
                throw new Exception();
            }
            if (level == 0)
            {
                parentMask = (ulong)1 << 63;
                return ulong.MaxValue;
            } else if (level == 1)
            {
                if (subIndex >= _cMaxTag - 1)
                    throw new Exception();
                // | 
                ulong ret = (ulong)1 << (62 - subIndex);
                parentMask = ret;
                ulong mask = (((ulong)1 << (_cMaxTag * (_cMaxLevel - level))) - 1);
                ret = ret | mask;
                return ret;
            } else if (level >= 2 && level < _cMaxLevel - 1)
            {
                int shiftAmount = (_cMaxTag * (_cMaxLevel - level) - 1 - subIndex);
                ulong ret = (ulong)1 << shiftAmount;
                parentMask = ret;
                ulong mask = (((ulong)1 << (_cMaxTag * (_cMaxLevel - level - 1))) - 1);
                ret = ret | mask;
                return ret;
            } else // _cMaxLevel - 1
            {
                ulong ret = (ulong)1 << ((_cMaxTag - 1) - subIndex);
                parentMask = 0;
                return ret;
            }
        }


        void AttachNode(TagNode parentNode, TagRootNode rootNode, int level = 0, ulong parentMask = 0, string preName = "")
        {
            if (parentNode == null)
                return;
            
            if (!string.IsNullOrEmpty(parentNode.name))
            {
                string name = preName + parentNode.name;
                m_TagMap[name] = parentNode;
                if (parentNode.childNode != null && parentNode.childNode.Count > 0)
                {
                    string newPreName = name + ".";
                    for (int i = 0; i < parentNode.childNode.Count; ++i)
                    {
                        var childNode = parentNode.childNode[i];
                        childNode.rootNode = rootNode;
                        ulong childParentMask;
                        childNode.mask = GetLevelMask(level, out childParentMask, i) | parentMask;
                        AttachNode(childNode, rootNode, level + 1, childParentMask | parentMask, newPreName);
                    }
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (m_TagRootNodes != null)
            {
                ulong rootId = 1;
                for (int i = 0; i < m_TagRootNodes.Count; ++i)
                {
                    var rootNode = m_TagRootNodes[i];
                    if (rootNode != null)
                    {
                        rootNode.id = rootId++;
                        rootNode.rootNode = null;
                        ulong parentMask;
                        rootNode.mask = GetLevelMask(0, out parentMask);
                        m_TagRootMap[rootNode.id] = rootNode;
                        AttachNode(rootNode, rootNode, 1, parentMask);
                    }
                }
            }
        }
    }
}
