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

        public ulong TagValue
        {
            get
            {
                return m_TagValue.Value;
            }
        }
    }
}
