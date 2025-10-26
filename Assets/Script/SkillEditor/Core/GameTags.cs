using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;
using Netcode;
using Unity.Netcode;

namespace GAS
{

    public class GameTags : NetworkBehaviour
    {
        // ¶¯Ì¬µÄ
        private NetworkVariable<ulong> m_TagValue = new NetworkVariable<ulong>();

        private void Awake()
        {
            m_TagValue.bRepNotify = true;
            if (IsHost || IsClient)
                m_TagValue.OnValueChanged = OnRep_TagValue;
        }

        void OnRep_TagValue(ulong previousValue, ulong newValue)
        {}

        public ulong TagValue
        {
            get
            {
                return m_TagValue.Value;
            }
        }
    }
}
