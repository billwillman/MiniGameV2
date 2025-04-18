using System;
using UnityEngine;
using Unity.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using Utils;

namespace SOC.GamePlay
{
    [XLua.LuaCallCSharp]
    public class PlayerController : BaseNetworkMono
    {
#if UNITY_SERVER
        private void Awake() {
            var networkObject = GetComponent<NetworkObject>();
            networkObject.CheckObjectVisibility = OnIsOwnerClient;
            PawnId.bRepNotify = true;
            PawnId.OnValueChanged = PawnId_OnRepNotify;
            PawnNetworkObject.OnStaticNetworkObjectSpawn += OnPawnNetworkSpawn;
            PawnNetworkObject.OnStaticNetworkObjectDespawn += OnPawnNetworkDespawn;
        }

        private bool OnIsOwnerClient(ulong clientId) {
            return this.OwnerClientId == clientId;
        }
#else
        private void Awake()
        {
            PawnId.bRepNotify = true;
            PawnId.OnValueChanged = PawnId_OnRepNotify;
            PawnNetworkObject.OnStaticNetworkObjectSpawn = OnPawnNetworkSpawn;
            PawnNetworkObject.OnStaticNetworkObjectDespawn = OnPawnNetworkDespawn;
        }
#endif

        [XLua.BlackList]
        void OnPawnNetworkSpawn(NetworkObject obj)
        {
            if (PawnId.Value == obj.NetworkObjectId)
            {
                Pawn = obj;
            }
        }

        [XLua.BlackList]
        void OnPawnNetworkDespawn(NetworkObject obj)
        {
            if (PawnId.Value == obj.NetworkObjectId)
            {
                Pawn = null;
            }
        }

        [XLua.BlackList]
        public override void OnDestroy()
        {
            PawnId.OnValueChanged = null;
            PawnNetworkObject.OnStaticNetworkObjectSpawn -= OnPawnNetworkSpawn;
            PawnNetworkObject.OnStaticNetworkObjectDespawn -= OnPawnNetworkDespawn;
            base.OnDestroy();
        }

        [XLua.BlackList]
        void PawnId_OnRepNotify(ulong previousValue, ulong newValue)
        {
            if (newValue == default(ulong))
            {
                Pawn = null;
                return;
            }
            var networkMgr = this.NetworkManager;
            if (networkMgr != null)
            {
                var spawnMgr = networkMgr.SpawnManager;
                if (spawnMgr != null)
                {
                    NetworkObject networkObj;
                    if (spawnMgr.SpawnedObjects.TryGetValue(newValue, out networkObj))
                        Pawn = networkObj;
                }
            }
        }

        [XLua.BlackList]
        public override void OnNetworkDespawn() {
            ClearAllEvents();
            if (IsCanSpawnPawn)
            {
                ClearDelaySpawnPawnTimer();
                if (Pawn != null && Pawn.IsSpawned)
                {
                    Pawn.Despawn();
                    //ResourceMgr.Instance.DestroyInstGameObj(Pawn.gameObject);
                    Pawn = null;
                    PawnId.Value = default(ulong);
                    PawnId.SetDirty(true);
                }
            }
            base.OnNetworkDespawn();
        }

        void CreatePawn()
        {
            if (IsCanSpawnPawn)
            {
                // var pawnNetworkObject = PawnClassPrefab.GetComponent<NetworkObject>();
                //Pawn = this.NetworkManager.SpawnManager.InstantiateAndSpawn(pawnNetworkObject, OwnerClientId, false, true, true);  //OwnerClientId
                //string name = PawnClassPrefab.gameObject.name;
                var gameObj = GameObject.Instantiate(PawnClassPrefab.gameObject);
                //gameObj.name = name;
                Pawn = gameObj.GetComponent<NetworkObject>();
                if (Pawn != null)
                {
                    Pawn.SpawnWithOwnership(OwnerClientId);
                    //Pawn.SpawnAsPlayerObject(OwnerClientId);
                    // Pawn.Spawn();
                    //Pawn.DontDestroyWithOwner = true;
                    PawnId.Value = Pawn.NetworkObjectId;
                    PawnId.SetDirty(true);
                }
            }
        }

        protected bool IsCanSpawnPawn
        {
            get
            {
                bool ret = (IsServer || IsHost) && AutoPawnInstance && PawnClassPrefab != null;
                return ret;
            }
        }

        protected void OnDelaySpawnPawnTimerEvent(Timer obj, float timer)
        {
            ClearDelaySpawnPawnTimer();
            CreatePawn();
        }

        void ClearDelaySpawnPawnTimer()
        {
            if (m_DelayCreatePawnTimer != null)
            {
                m_DelayCreatePawnTimer.Dispose();
                m_DelayCreatePawnTimer = null;
            }
        }

        void DelayCreatePawn()
        {
            if (IsCanSpawnPawn)
            {
                ClearDelaySpawnPawnTimer();
                m_DelayCreatePawnTimer = TimerMgr.Instance.CreateTimer(0, false, false);
                m_DelayCreatePawnTimer.AddListener(OnDelaySpawnPawnTimerEvent);
                m_DelayCreatePawnTimer.Start();
            }

        }
        private ITimer m_DelayCreatePawnTimer = null;

        [XLua.BlackList]
        public override void OnNetworkSpawn()
        {
            // 需要延迟Pawn，否则Client端的NetworkManager.LocalClientId为0，导致IsOwner判断不对
            DelayCreatePawn();
            base.OnNetworkSpawn();
        }

        [XLua.BlackList]
        public PawnNetworkObject PawnClassPrefab; // 实例化的模板Prefab
        [XLua.BlackList]
        public bool AutoPawnInstance = true; // 自动实例化

        public bool AttachPawn(PawnNetworkObject obj)
        {
            if (IsServer || IsHost)
            {
                if (Pawn == obj)
                    return true;
                if (obj == null)
                {
                    PawnId.Value = default(ulong);
                } else
                {
                    PawnId.Value = obj.NetworkObjectId;
                }
                PawnId.SetDirty(true);
                Pawn = obj.NetworkObject;
                return true;
            }
            Debug.LogError("[PlayerController] AttachPawn not support client");
            return false;
        }

        public void ClearAllEvents() {
            onClientStrEvent = null;
            onClientIntEvent = null;
            onClientInt2Event = null;
           // onClientInt3Event = null;

            onServerStrEvent = null;
            onServerIntEvent = null;
            onServerInt2Event = null;
           // onServerInt3Event = null;
        }

        // 服务器属性同步
        [NonSerialized]
        [XLua.BlackList]
        public NetworkVariable<ulong> PawnId = new NetworkVariable<ulong>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Int属性数组
        //[NonSerialized]
        //public NetworkVariable<NativeArray<int>> NetworkIntAttribute = new NetworkVariable<NativeArray<int>>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server); 

        // 运行时的角色
        public NetworkObject Pawn
        {
            get;
            protected set;
        }
        
        // ------------------- 调用Server ---------------------------------------
        // 可靠传输
        public void DispatchServer_Reliable(string eventName, string paramStr) {
            if (!IsClient && !IsHost)
                return;
            DispatchEvent_Reliable_ServerRpc(eventName, paramStr);
        }

        public void DispatchServer_Reliable(string eventName, int intParam) {
            if (!IsClient && !IsHost)
                return;
            DispatchEvent_Reliable_ServerRpc(eventName, intParam);
        }

        public void DispatchServer_Reliable(string eventName, int intParam1, int intParam2) {
            if (!IsClient && !IsHost)
                return;
            DispatchEvent_Reliable_ServerRpc(eventName, intParam1, intParam2);
        }

        /*
        public void DispatchServer_Reliable(string eventName, int intParam1, int intParam2, int intParam3) {
            if (!IsClient && !IsHost)
                return;
            DispatchEvent_Reliable_ServerRpc(eventName, intParam1, intParam2, intParam3);
        }
        */

        // 非可靠传输
        public void DispatchServer_UnReliable(string eventName, string paramStr) {
            if (!IsClient && !IsHost)
                return;
            DispatchEvent_Unreliable_ServerRpc(eventName, paramStr);
        }

        public void DispatchServer_UnReliable(string eventName, int intParam) {
            if (!IsClient && !IsHost)
                return;
            DispatchEvent_Unreliable_ServerRpc(eventName, intParam);
        }

        public void DispatchServer_UnReliable(string eventName, int intParam1, int intParam2) {
            if (!IsClient && !IsHost)
                return;
            DispatchEvent_Unreliable_ServerRpc(eventName, intParam1, intParam2);
        }

        /*
        public void DispatchServer_UnReliable(string eventName, int intParam1, int intParam2, int intParam3) {
            if (!IsClient && !IsHost)
                return;
            DispatchEvent_Unreliable_ServerRpc(eventName, intParam1, intParam2, intParam3);
        }
        */

        // ------------------- 广播所有Client -----------------------------------
        // 可靠传输
        public void DispatchAllClientEvent_Reliable(string eventName, string paramStr) {
            if (!IsServer && !IsHost)
                return;
            DispatchEvent_Reliable_ClientRpc(eventName, paramStr);
        }

        public void DispatchAllClientEvent_Reliable(string eventName, int intParam) {
            if (!IsServer && !IsHost)
                return;
            DispatchEvent_Reliable_ClientRpc(eventName, intParam);
        }

        public void DispatchAllClientEvent_Reliable(string eventName, int intParam1, int intParam2) {
            if (!IsServer && !IsHost)
                return;
            DispatchEvent_Reliable_ClientRpc(eventName, intParam1, intParam2);
        }
        /*
        public void DispatchAllClientEvent_Reliable(string eventName, int intParam1, int intParam2, int intParam3) {
            if (!IsServer && !IsHost)
                return;
            DispatchEvent_Reliable_ClientRpc(eventName, intParam1, intParam2, intParam3);
        }
        */
        // 不可靠传输
        public void DispatchAllClientEvent_UnReliable(string eventName, string paramStr) {
            if (!IsServer && !IsHost)
                return;
            DispatchEvent_Unreliable_ClientRpc(eventName, paramStr);
        }

        public void DispatchAllClientEvent_UnReliable(string eventName, int intParam) {
            if (!IsServer && !IsHost)
                return;
            DispatchEvent_Unreliable_ClientRpc(eventName, intParam);
        }

        public void DispatchAllClientEvent_UnReliable(string eventName, int intParam1, int intParam2) {
            if (!IsServer && !IsHost)
                return;
            DispatchEvent_Unreliable_ClientRpc(eventName, intParam1, intParam2);
        }
        /*
        public void DispatchAllClientEvent_UnReliable(string eventName, int intParam1, int intParam2, int intParam3) {
            if (!IsServer && !IsHost)
                return;
            DispatchEvent_Unreliable_ClientRpc(eventName, intParam1, intParam2, intParam3);
        }
        */
        // ------------------- 调用到对应的Client上 ----------------------------------
        // 可靠传输
        public void DispatchClientEvent_Reliable(string eventName, string paramStr) {
            if (!IsServer && !IsHost)
                return;
            NativeArray<ulong> send = new NativeArray<ulong>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try {
                send[0] = this.OwnerClientId;
                ClientRpcParams clientRpcParams = new ClientRpcParams() {
                    Send = new ClientRpcSendParams() {
                        TargetClientIdsNativeArray = send
                    }
                };
                DispatchEvent_Reliable_ClientRpc(eventName, paramStr, clientRpcParams);
            } finally {
                send.Dispose();
            }
        }

        public void DisptachClientEvent_Reliable(string eventName, int intParam) {
            if (!IsServer && !IsHost)
                return;
            NativeArray<ulong> send = new NativeArray<ulong>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try {
                send[0] = this.OwnerClientId;
                ClientRpcParams clientRpcParams = new ClientRpcParams() {
                    Send = new ClientRpcSendParams() {
                        TargetClientIdsNativeArray = send
                    }
                };
                DispatchEvent_Reliable_ClientRpc(eventName, intParam, clientRpcParams);
            } finally {
                send.Dispose();
            }
        }

        public void DisptachClientEvent_Reliable(string eventName, int intParam1, int intParam2) {
            if (!IsServer && !IsHost)
                return;
            NativeArray<ulong> send = new NativeArray<ulong>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try {
                send[0] = this.OwnerClientId;
                ClientRpcParams clientRpcParams = new ClientRpcParams() {
                    Send = new ClientRpcSendParams() {
                        TargetClientIdsNativeArray = send
                    }
                };
                DispatchEvent_Reliable_ClientRpc(eventName, intParam1, intParam2, clientRpcParams);
            } finally {
                send.Dispose();
            }
        }
        /*
        public void DisptachClientEvent_Reliable(string eventName, int intParam1, int intParam2, int intParam3) {
            if (!IsServer && !IsHost)
                return;
            NativeArray<ulong> send = new NativeArray<ulong>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try {
                send[0] = this.OwnerClientId;
                ClientRpcParams clientRpcParams = new ClientRpcParams() {
                    Send = new ClientRpcSendParams() {
                        TargetClientIdsNativeArray = send
                    }
                };
                DispatchEvent_Reliable_ClientRpc(eventName, intParam1, intParam2, intParam3, clientRpcParams);
            } finally {
                send.Dispose();
            }
        }
        */
        // 非可靠传输
        public void DispatchClientEvent_UnReliable(string eventName, string paramStr) {
            if (!IsServer && !IsHost)
                return;
            NativeArray<ulong> send = new NativeArray<ulong>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try {
                send[0] = this.OwnerClientId;
                ClientRpcParams clientRpcParams = new ClientRpcParams() {
                    Send = new ClientRpcSendParams() {
                        TargetClientIdsNativeArray = send
                    }
                };
                DispatchEvent_Unreliable_ClientRpc(eventName, paramStr, clientRpcParams);
            } finally {
                send.Dispose();
            }
        }

        public void DispatchClientEvent_UnReliable(string eventName, int intParam) {
            if (!IsServer && !IsHost)
                return;
            NativeArray<ulong> send = new NativeArray<ulong>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try {
                send[0] = this.OwnerClientId;
                ClientRpcParams clientRpcParams = new ClientRpcParams() {
                    Send = new ClientRpcSendParams() {
                        TargetClientIdsNativeArray = send
                    }
                };
                DispatchEvent_Unreliable_ClientRpc(eventName, intParam, clientRpcParams);
            } finally {
                send.Dispose();
            }
        }

        public void DispatchClientEvent_UnReliable(string eventName, int intParam1, int intParam2) {
            if (!IsServer && !IsHost)
                return;
            NativeArray<ulong> send = new NativeArray<ulong>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try {
                send[0] = this.OwnerClientId;
                ClientRpcParams clientRpcParams = new ClientRpcParams() {
                    Send = new ClientRpcSendParams() {
                        TargetClientIdsNativeArray = send
                    }
                };
                DispatchEvent_Unreliable_ClientRpc(eventName, intParam1, intParam2, clientRpcParams);
            } finally {
                send.Dispose();
            }
        }

        /*
        public void DispatchClientEvent_UnReliable(string eventName, int intParam1, int intParam2, int intParam3) {
            if (!IsServer && !IsHost)
                return;
            NativeArray<ulong> send = new NativeArray<ulong>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try {
                send[0] = this.OwnerClientId;
                ClientRpcParams clientRpcParams = new ClientRpcParams() {
                    Send = new ClientRpcSendParams() {
                        TargetClientIdsNativeArray = send
                    }
                };
                DispatchEvent_Unreliable_ClientRpc(eventName, intParam1, intParam2, intParam3, clientRpcParams);
            } finally {
                send.Dispose();
            }
        }
        */

        // ---------------------------------------------------------------------------------------

        [ClientRpc(Delivery = RpcDelivery.Reliable)]
        // Server To Client
        protected void DispatchEvent_Reliable_ClientRpc(string eventName, string paramStr, ClientRpcParams clientRpcParams = default) {
            if (onClientStrEvent != null)
                onClientStrEvent(eventName, paramStr);
        }

        [ClientRpc(Delivery = RpcDelivery.Reliable)]
        protected void DispatchEvent_Reliable_ClientRpc(string eventName, int intParam, ClientRpcParams clientRpcParams = default) {
            if (onClientIntEvent != null)
                onClientIntEvent(eventName, intParam);
        }
        [ClientRpc(Delivery = RpcDelivery.Reliable)]
        protected void DispatchEvent_Reliable_ClientRpc(string eventName, int intParam1, int intParam2, ClientRpcParams clientRpcParams = default) {
            if (onClientInt2Event != null)
                onClientInt2Event(eventName, intParam1, intParam2);
        }
        /*
        [ClientRpc(Delivery = RpcDelivery.Reliable)]
        protected void DispatchEvent_Reliable_ClientRpc(string eventName, int intParam1, int intParam2, int intParam3, ClientRpcParams clientRpcParams = default) {
            if (onClientInt3Event != null)
                onClientInt3Event(this.OwnerClientId, eventName, intParam1, intParam2, intParam3);
        }
        */
        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        // Server To Client
        protected void DispatchEvent_Unreliable_ClientRpc(string eventName, string paramStr, ClientRpcParams clientRpcParams = default) {
            if (onClientStrEvent != null)
                onClientStrEvent(eventName, paramStr);
        }

        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        protected void DispatchEvent_Unreliable_ClientRpc(string eventName, int intParam, ClientRpcParams clientRpcParams = default) {
            if (onClientIntEvent != null)
                onClientIntEvent(eventName, intParam);
        }
        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        protected void DispatchEvent_Unreliable_ClientRpc(string eventName, int intParam1, int intParam2, ClientRpcParams clientRpcParams = default) {
            if (onClientInt2Event != null)
                onClientInt2Event(eventName, intParam1, intParam2);
        }

        /*
        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        protected void DispatchEvent_Unreliable_ClientRpc(string eventName, int intParam1, int intParam2, int intParam3, ClientRpcParams clientRpcParams = default) {
            if (onClientInt3Event != null)
                onClientInt3Event(eventName, intParam1, intParam2, intParam3);
        }
        */

        // -------------------------------------- 任意客户端都可以调用
        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Reliable)]
        protected void DispatchEvent_Reliable_ServerRpc(string eventName, string paramStr, ServerRpcParams serverRpcParams = default) {
            if (onServerStrEvent != null)
                onServerStrEvent(this.OwnerClientId, eventName, paramStr);
        }

        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Reliable)]
        protected void DispatchEvent_Reliable_ServerRpc(string eventName, int intParam, ServerRpcParams serverRpcParams = default) {
            if (onServerIntEvent != null)
                onServerIntEvent(this.OwnerClientId, eventName, intParam);
        }

        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Reliable)]
        protected void DispatchEvent_Reliable_ServerRpc(string eventName, int intParam1, int intParam2, ServerRpcParams serverRpcParams = default) {
            if (onServerInt2Event != null)
                onServerInt2Event(this.OwnerClientId, eventName, intParam1, intParam2);
        }

        /*
        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Reliable)]
        protected void DispatchEvent_Reliable_ServerRpc(string eventName, int intParam1, int intParam2, int intParam3, ServerRpcParams serverRpcParams = default) {
            if (onServerInt3Event != null)
                onServerInt3Event(eventName, intParam1, intParam2, intParam3);
        }
        */


        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
        protected void DispatchEvent_Unreliable_ServerRpc(string eventName, string paramStr, ServerRpcParams serverRpcParams = default) {
            if (onServerStrEvent != null)
                onServerStrEvent(this.OwnerClientId, eventName, paramStr);
        }

        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
        protected void DispatchEvent_Unreliable_ServerRpc(string eventName, int intParam, ServerRpcParams serverRpcParams = default) {
            if (onServerIntEvent != null)
                onServerIntEvent(this.OwnerClientId, eventName, intParam);
        }

        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
        protected void DispatchEvent_Unreliable_ServerRpc(string eventName, int intParam1, int intParam2, ServerRpcParams serverRpcParams = default) {
            if (onServerInt2Event != null)
                onServerInt2Event(this.OwnerClientId, eventName, intParam1, intParam2);
        }

        /*
        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
        protected void DispatchEvent_Unreliable_ServerRpc(string eventName, int intParam1, int intParam2, int intParam3, ServerRpcParams serverRpcParams = default) {
            if (onServerInt3Event != null)
                onServerInt3Event(eventName, intParam1, intParam2, intParam3);
        }
        */

        // ------------------------------ 外部设置 ------------------------
        public Action<string, string> onClientStrEvent {
            get; set;
        }

        public Action<string, int> onClientIntEvent {
            get; set;
        }
        public Action<string, int, int> onClientInt2Event {
            get; set;
        }

        /*
        public Action<ulong, string, int, int, int> onClientInt3Event {
            get; set;
        }
        */

        public Action<ulong, string, string> onServerStrEvent {
            get; set;
        }

        public Action<ulong, string, int> onServerIntEvent {
            get; set;
        }
        public Action<ulong, string, int, int> onServerInt2Event {
            get; set;
        }
         /*
        public Action<ulong, string, int, int, int> onServerInt3Event {
            get; set;
        }
        */
        // ----------------------------------------------------------------
    }
}
