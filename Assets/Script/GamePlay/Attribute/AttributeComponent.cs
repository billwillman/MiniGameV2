using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using OD;
using SOC.GamePlay;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace SOC.GamePlay.Attribute
{
    [System.Serializable]
    public enum AttributeInitType
    {
        Awake = 0,
        Start = 1,
    }

    public class AttributeComponent : BaseNetworkMono
    {
        public NetworkAttributeGroupMeta[] AttributeGroupMeta = null;

        public AttributeInitType ServerInitType = AttributeInitType.Awake;
        public AttributeInitType ClientInitType = AttributeInitType.Start;

        [System.NonSerialized]
        public List<NetworkStringAttributeGroup> NetworkStringGroupVars;
        [System.NonSerialized]
        public List<NetworkIntAttributeGroup> NetworkIntGroupVars;
        [System.NonSerialized]
        public List<NetworkInt64AttributeGroup> NetworkInt64GroupVars;

        public void InitAttributeGroup()
        {
            if (AttributeGroupMeta != null)
            {
                foreach (var iter in AttributeGroupMeta)
                {
                    switch (iter.AttributeType)
                    {
                        case NetworkAttributeType.Int:
                            if (iter.Attributes != null && iter.Attributes.Length > 0)
                            {
                                if (NetworkIntGroupVars == null)
                                    NetworkIntGroupVars = new List<NetworkIntAttributeGroup>();
                                var Group1 = new NetworkIntAttributeGroup();
                                Group1.bRepNotify = iter.bRepNotify;
                                Group1.OnValueChanged = iter.OnIntGroupValueChanged;
                                ushort currentKey = 0;
                                foreach (var intIter in iter.Attributes)
                                {
                                    Group1.Value.AttributeMap[currentKey++] = intIter.IntDefaultValue;
                                }
                                Group1.SetDirty(false); // 默认值不认为是
                                NetworkIntGroupVars.Add(Group1);
                            }
                            break;
                        case NetworkAttributeType.Int64:
                            if (iter.Attributes != null && iter.Attributes.Length > 0)
                            {
                                if (NetworkInt64GroupVars == null)
                                    NetworkInt64GroupVars = new List<NetworkInt64AttributeGroup>();
                                var Group2 = new NetworkInt64AttributeGroup();
                                Group2.bRepNotify = iter.bRepNotify;
                                Group2.OnValueChanged = iter.OnInt64GroupValueChanged;
                                ushort currentKey = 0;
                                foreach (var int64Iter in iter.Attributes)
                                {
                                    Group2.Value.AttributeMap[currentKey++] = int64Iter.Int64DefaultValue;
                                }
                                Group2.SetDirty(false);
                                NetworkInt64GroupVars.Add(Group2);
                            }
                            break;
                        case NetworkAttributeType.String:
                            if (iter.Attributes != null && iter.Attributes.Length > 0)
                            {
                                if (NetworkStringGroupVars == null)
                                    NetworkStringGroupVars = new List<NetworkStringAttributeGroup>();
                                var Group3 = new NetworkStringAttributeGroup();
                                Group3.bRepNotify = iter.bRepNotify;
                                ushort currentKey = 0;
                                foreach (var stringIter in iter.Attributes)
                                {
                                    Group3.Value.AttributeMap[currentKey++] = stringIter.StringDefaultValue;
                                }
                                Group3.SetDirty(false);
                                NetworkStringGroupVars.Add(Group3);
                            }
                            break;
                    }
                }
            }
        }

        protected virtual void Awake()
        {
            if (GameStart.IsDS)
            {
                if (ServerInitType == AttributeInitType.Awake)
                    InitAttributeGroup();
            } else if (ClientInitType == AttributeInitType.Awake)
                InitAttributeGroup();
        }

        protected virtual void Start()
        {
            if (GameStart.IsDS)
            {
                if (ServerInitType == AttributeInitType.Start)
                    InitAttributeGroup();
            } else if (ClientInitType == AttributeInitType.Start)
                InitAttributeGroup();
        }
    }
}
