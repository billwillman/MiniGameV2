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
        public TagRootNode rootNode = null;
        public List<TagNode> childNode = null;
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


        void AttachNode(TagNode rootNode, string preName = "")
        {
            if (rootNode == null)
                return;
            
            if (!string.IsNullOrEmpty(rootNode.name))
            {
                string name = preName + rootNode.name;
                m_TagMap[name] = rootNode;
                if (rootNode.childNode != null && rootNode.childNode.Count > 0)
                {
                    string newPreName = name + ".";
                    for (int i = 0; i < rootNode.childNode.Count; ++i)
                    {
                        var childNode = rootNode.childNode[i];
                        AttachNode(childNode, newPreName);
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
                        m_TagRootMap[rootNode.id] = rootNode;
                        AttachNode(rootNode);
                    }
                }
            }
        }
    }
}
