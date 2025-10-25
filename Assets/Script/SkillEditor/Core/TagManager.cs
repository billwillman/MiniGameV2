using SOC.GamePlay;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    public class TagNode
    {
        public string name;
        public TagRootNode rootNode = null;
        public List<TagNode> childNode = null;
    }

    public class TagRootNode : TagNode
    {
        public ulong tagValue;
    }

    public class TagManager : SingetonMono<TagManager>
    {
        public List<TagRootNode> m_TagRootNodes = null;
    }
}
