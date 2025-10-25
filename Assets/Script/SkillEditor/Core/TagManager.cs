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
    }

    [System.Serializable]
    public class TagRootNode : TagNode
    {
        [System.NonSerialized]
        public ulong tagValue;
    }

    public class TagManager : SingetonMono<TagManager>
    {
        public List<TagRootNode> m_TagRootNodes = null;
    }
}
