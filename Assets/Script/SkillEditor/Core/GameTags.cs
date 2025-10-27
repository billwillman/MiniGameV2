using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;
using Netcode;
using Unity.Netcode;
using SOC.GamePlay;

namespace GAS
{

    public class GameTags : NetworkBehaviour
    {
        // ¶¯Ì¬µÄ
        private NetworkList<ulong> m_TagValues = new NetworkList<ulong>();

        private void Awake()
        {
            if (!GameStart.IsDS)
                m_TagValues.OnListChanged += OnRep_TagValues;
        }

        void OnRep_TagValues(NetworkListEvent<ulong> evt)
        {}

        
    }
}
