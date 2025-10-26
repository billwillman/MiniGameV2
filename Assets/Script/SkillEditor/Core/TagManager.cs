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


        void AttachNode(TagNode parentNode, TagRootNode rootNode, string preName = "")
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
                        AttachNode(childNode, rootNode, newPreName);
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
                        AttachNode(rootNode, rootNode);
                    }
                }
            }
        }
    }
}
