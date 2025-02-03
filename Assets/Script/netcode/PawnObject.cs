using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System;

namespace SOC.GamePlay
{
    [RequireComponent(typeof(NetworkObject))]
    public class PawnNetworkObject : BaseNetworkMono
    {
        public static Action<NetworkObject> OnStaticNetworkObjectDespawn
        {
            get;
            set;
        }

        public static Action<NetworkObject> OnStaticNetworkObjectSpawn
        {
            get;
            set;
        }

        [XLua.BlackList]
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (OnStaticNetworkObjectSpawn != null)
            {
                OnStaticNetworkObjectSpawn(this.NetworkObject);
            }
        }

        [XLua.BlackList]
        public override void OnNetworkDespawn()
        {
            if (OnStaticNetworkObjectDespawn != null)
                OnStaticNetworkObjectDespawn(this.NetworkObject);
            base.OnNetworkDespawn();
        }
    }
}
