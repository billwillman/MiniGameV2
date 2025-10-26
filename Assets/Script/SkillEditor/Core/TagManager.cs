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
        public ulong id; // ö��
    }

    [Tooltip("GameTag�ܶ���")]
    public class TagManager : SingetonMono<TagManager>
    {
        public List<TagRootNode> m_TagRootNodes = null;
        private Dictionary<string, TagNode> m_TagMap = new Dictionary<string, TagNode>();
        private Dictionary<ulong, TagRootNode> m_TagRootMap = new Dictionary<ulong, TagRootNode>(); // ö�ٶ�Ӧ��TagRootNode
    }
}
