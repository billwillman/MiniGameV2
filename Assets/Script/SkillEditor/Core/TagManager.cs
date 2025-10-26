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
    {}

    [Tooltip("GameTag×Ü¶¨Òå")]
    public class TagManager : SingetonMono<TagManager>
    {
        public List<TagRootNode> m_TagRootNodes = null;
        private Dictionary<string, TagRootNode> m_TagMap = new Dictionary<string, TagRootNode>();
    }
}
