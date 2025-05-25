using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using OD;
using SOC.GamePlay;
using Unity.Netcode;
using UnityEditor.UI;
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

    [XLua.LuaCallCSharp]
    public class AttributeComponent : BaseNetworkMono
    {
        public NetworkAttributeGroupMeta[] AttributeGroupMeta = null;

        public AttributeInitType ServerInitType = AttributeInitType.Awake;
        public AttributeInitType ClientInitType = AttributeInitType.Awake;

        [System.NonSerialized]
        public List<NetworkStringAttributeGroup> NetworkStringGroupVars;
        [System.NonSerialized]
        public List<NetworkIntAttributeGroup> NetworkIntGroupVars;
        [System.NonSerialized]
        public List<NetworkInt64AttributeGroup> NetworkInt64GroupVars;

        public int GetNetworkStringGroupVarsNum()
        {
            if (NetworkStringGroupVars == null)
                return 0;
            return NetworkStringGroupVars.Count;
        }

        public bool GetNetworkIntGroupVars(int index, out IntAttributeGroup ret)
        {
            ret = default(IntAttributeGroup);
            if (index < 0 || index >= GetNetworkIntGroupVarsNum())
                return false;
            var item = NetworkIntGroupVars[index];
            if (item == null)
                return false;
            ret = item.Value;
            return true;
        }

        public bool SetNetworkIntGroupVars(int index, ushort key, int value)
        {
            if (index < 0 || index >= GetNetworkIntGroupVarsNum())
                return false;
            var item = NetworkIntGroupVars[index];
            if (item == null || item.Value == null || item.Value.AttributeMap == null)
                return false;
            item.Value.AttributeMap[key] = value;
            bool isHostServer = this.IsHost && this.IsServer;
            if (isHostServer)
            {
                if (item.OnValueChanged != null)
                    item.OnValueChanged(item.Value, item.Value);
            } else
                item.SetDirty(true);
            return true;
        }

        public bool SetNetworkInt64GroupVars(int index, ushort key, long value)
        {
            if (index < 0 || index >= GetNetworkInt64GroupVarsNum())
                return false;
            var item = NetworkInt64GroupVars[index];
            if (item == null || item.Value == null || item.Value.AttributeMap == null)
                return false;
            bool isHostServer = this.IsHost && this.IsServer;
            item.Value.AttributeMap[key] = value;
            if (isHostServer)
            {
                if (item.OnValueChanged != null)
                    item.OnValueChanged(item.Value, item.Value);
            } else
                item.SetDirty(true);
            return true;
        }

        public bool SetNetworkStringGroupVars(int index, ushort key, string value)
        {
            if (index < 0 || index >= GetNetworkStringGroupVarsNum())
                return false;
            var item = NetworkStringGroupVars[index];
            if (item == null || item.Value == null || item.Value.AttributeMap == null)
                return false;
            item.Value.AttributeMap[key] = value;
            bool isHostServer = this.IsHost && this.IsServer;
            if (isHostServer)
            {
                if (item.OnValueChanged != null)
                    item.OnValueChanged(item.Value, item.Value);
            } else
                item.SetDirty(true);
            return true;
        }

        public bool GetNetworkStringGroupVarsToStr(int index, ushort key, out string ret)
        {
            ret = default(string);
            StringAttributeGroup group;
            if (!GetNetworkStringGroupVars(index, out group) || group == null || group.AttributeMap == null)
                return false;
            return group.AttributeMap.TryGetValue(key, out ret);
        }

        public bool GetNetworkIntGroupVarsToInt(int index, ushort key, out int ret)
        {
            ret = default(int);
            IntAttributeGroup group;
            if (!GetNetworkIntGroupVars(index, out group) || group == null || group.AttributeMap == null)
                return false;
            return group.AttributeMap.TryGetValue(key, out ret);
        }

        public bool GetNetworkInt64GroupVarsToInt64(int index, ushort key, out long ret)
        {
            ret = default(long);
            Int64AttributeGroup group;
            if (!GetNetworkInt64GroupVars(index, out group) || group == null || group.AttributeMap == null)
                return false;
            return group.AttributeMap.TryGetValue(key, out ret);
        }

        public bool GetNetworkStringGroupVars(int index, out StringAttributeGroup ret)
        {
            ret = default(StringAttributeGroup);
            if (index < 0 || index >= GetNetworkStringGroupVarsNum())
                return false;
            var item = NetworkStringGroupVars[index];
            if (item == null)
                return false;
            ret = item.Value;
            return true;
        }

        public bool GetNetworkInt64GroupVars(int index, out Int64AttributeGroup ret)
        {
            ret = default(Int64AttributeGroup);
            if (index < 0 || index >= GetNetworkInt64GroupVarsNum())
                return false;
            var item = NetworkInt64GroupVars[index];
            if (item == null)
                return false;
            ret = item.Value;
            return true;
        }

        public int GetNetworkIntGroupVarsNum()
        {
            if (NetworkIntGroupVars == null)
                return 0;
            return NetworkIntGroupVars.Count;
        }

        public int GetNetworkInt64GroupVarsNum()
        {
            if (NetworkInt64GroupVars == null)
                return 0;
            return NetworkInt64GroupVars.Count;
        }

        public Action OnPostInitAttributeEvent
        {
            get;
            set;
        }

        public Action OnPreInitAttributeEvent
        {
            get;
            set;
        }

        public void SetAttributeChangeEvent(int index, NetworkVariable<IntAttributeGroup>.OnValueChangedDelegate evt)
        {
            var Group = NetworkIntGroupVars[index];
            Group.OnValueChanged = evt;
        }

        public void SetAttributeChangeEvent(int index, NetworkVariable<Int64AttributeGroup>.OnValueChangedDelegate evt)
        {
            var Group = NetworkInt64GroupVars[index];
            Group.OnValueChanged = evt;
        }

        public void SetAttributeChangeEvent(int index, NetworkVariable<StringAttributeGroup>.OnValueChangedDelegate evt)
        {
            var Group = NetworkStringGroupVars[index];
            Group.OnValueChanged = evt;
        }

        [XLua.BlackList]
        public void InitAttributeGroup()
        {
            if (AttributeGroupMeta != null)
            {
                if (OnPreInitAttributeEvent != null)
                    OnPreInitAttributeEvent();

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
                                Group1.Initialize(this);
                                Group1.bRepNotify = iter.bRepNotify;
                                Group1.OnValueChanged = iter.OnIntGroupValueChanged;
                                ushort currentKey = 0;
                                foreach (var intIter in iter.Attributes)
                                {
                                    Group1.Value.AttributeMap.Add(currentKey++, intIter.IntDefaultValue);
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
                                Group2.Initialize(this);
                                Group2.bRepNotify = iter.bRepNotify;
                                Group2.OnValueChanged = iter.OnInt64GroupValueChanged;
                                ushort currentKey = 0;
                                foreach (var int64Iter in iter.Attributes)
                                {
                                    Group2.Value.AttributeMap.Add(currentKey++, int64Iter.Int64DefaultValue);
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
                                Group3.Initialize(this);
                                Group3.bRepNotify = iter.bRepNotify;
                                ushort currentKey = 0;
                                foreach (var stringIter in iter.Attributes)
                                {
                                    Group3.Value.AttributeMap.Add(currentKey++, stringIter.StringDefaultValue);
                                }
                                Group3.SetDirty(false);
                                NetworkStringGroupVars.Add(Group3);
                            }
                            break;
                    }
                }
                if (OnPostInitAttributeEvent != null)
                    OnPostInitAttributeEvent();
            }
        }

        [XLua.BlackList]
        protected virtual void Awake()
        {
            if (GameStart.IsDS)
            {
                if (ServerInitType == AttributeInitType.Awake)
                    InitAttributeGroup();
            } else if (ClientInitType == AttributeInitType.Awake)
                InitAttributeGroup();
        }

        [XLua.BlackList]
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
