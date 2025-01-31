using System;
using UnityEngine;
using Unity.Netcode;

namespace SOC.GamePlay
{
    [XLua.LuaCallCSharp]
    public class BaseNetworkMono : NetworkBehaviour
    {
        // Client和Server都会执行
        [XLua.DoNotGen]
        public override void OnNetworkSpawn() {
            base.OnNetworkSpawn();
            if (onNetworkSpawn != null)
                onNetworkSpawn();
        }

        // Client和Server都会执行
        [XLua.DoNotGen]
        public override void OnNetworkDespawn() {
            if (onNetworkDespawn != null)
                onNetworkDespawn();
            base.OnNetworkDespawn();
        }

        public Action onNetworkSpawn {
            get;
            set;
        }

        public Action onNetworkDespawn {
            get;
            set;
        }
    }
}
