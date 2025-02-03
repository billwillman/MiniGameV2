using System;
using UnityEngine;
using Unity.Collections;
using Unity.Netcode;
using Unity.VisualScripting;

namespace SOC.GamePlay
{
    [XLua.LuaCallCSharp]
    public class PlayerController : BaseNetworkMono
    {
#if UNITY_SERVER
        private void Awake() {
            var networkObject = GetComponent<NetworkObject>();
            networkObject.CheckObjectVisibility = OnIsOwnerClient;
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
            if (IsOwner && AutoPawnInstance && PawnClassPrefab != null)
            {
                if (Pawn != null)
                {
                    ResourceMgr.Instance.DestroyInstGameObj(Pawn.gameObject);
                    Pawn = null;
                    PawnId.Value = default(ulong);
                    PawnId.SetDirty(true);
                }
            }
            base.OnNetworkDespawn();
        }

        [XLua.BlackList]
        public override void OnNetworkSpawn()
        {
            if (IsOwner && AutoPawnInstance && PawnClassPrefab != null)
            {
                Pawn = this.NetworkManager.SpawnManager.InstantiateAndSpawn(PawnClassPrefab.NetworkObject);
                if (Pawn != null)
                {
                    PawnId.Value = Pawn.NetworkObjectId;
                    PawnId.SetDirty(true);
                }
            }
            base.OnNetworkSpawn();
        }

        [XLua.BlackList]
        public PawnNetworkObject PawnClassPrefab; // ʵ������ģ��Prefab
        [XLua.BlackList]
        public bool AutoPawnInstance = true; // �Զ�ʵ����

        public bool AttachPawn(PawnNetworkObject obj)
        {
            if (IsOwner)
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
            onClientInt3Event = null;

            onServerStrEvent = null;
            onServerIntEvent = null;
            onServerInt2Event = null;
            onServerInt3Event = null;
        }

        // ����������ͬ��
        [NonSerialized]
        [XLua.BlackList]
        public NetworkVariable<ulong> PawnId = new NetworkVariable<ulong>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // ����ʱ�Ľ�ɫ
        public NetworkObject Pawn
        {
            get;
            protected set;
        }
        
        // ------------------- ����Server ---------------------------------------
        // �ɿ�����
        public void DispatchServer_Reliable(string eventName, string paramStr) {
            if (!IsClient)
                return;
            DispatchEvent_Reliable_ServerRpc(eventName, paramStr);
        }

        public void DispatchServer_Reliable(string eventName, int intParam) {
            if (!IsClient)
                return;
            DispatchEvent_Reliable_ServerRpc(eventName, intParam);
        }

        public void DispatchServer_Reliable(string eventName, int intParam1, int intParam2) {
            if (!IsClient)
                return;
            DispatchEvent_Reliable_ServerRpc(eventName, intParam1, intParam2);
        }

        public void DispatchServer_Reliable(string eventName, int intParam1, int intParam2, int intParam3) {
            if (!IsClient)
                return;
            DispatchEvent_Reliable_ServerRpc(eventName, intParam1, intParam2, intParam3);
        }

        // �ǿɿ�����
        public void DispatchServer_UnReliable(string eventName, string paramStr) {
            if (!IsClient)
                return;
            DispatchEvent_Unreliable_ServerRpc(eventName, paramStr);
        }

        public void DispatchServer_UnReliable(string eventName, int intParam) {
            if (!IsClient)
                return;
            DispatchEvent_Unreliable_ServerRpc(eventName, intParam);
        }

        public void DispatchServer_UnReliable(string eventName, int intParam1, int intParam2) {
            if (!IsClient)
                return;
            DispatchEvent_Unreliable_ServerRpc(eventName, intParam1, intParam2);
        }

        public void DispatchServer_UnReliable(string eventName, int intParam1, int intParam2, int intParam3) {
            if (!IsClient)
                return;
            DispatchEvent_Unreliable_ServerRpc(eventName, intParam1, intParam2, intParam3);
        }

        // ------------------- �㲥����Client -----------------------------------
        // �ɿ�����
        public void DispatchAllClientEvent_Reliable(string eventName, string paramStr) {
            if (!IsServer)
                return;
            DispatchEvent_Reliable_ClientRpc(eventName, paramStr);
        }

        public void DispatchAllClientEvent_Reliable(string eventName, int intParam) {
            if (!IsServer)
                return;
            DispatchEvent_Reliable_ClientRpc(eventName, intParam);
        }

        public void DispatchAllClientEvent_Reliable(string eventName, int intParam1, int intParam2) {
            if (!IsServer)
                return;
            DispatchEvent_Reliable_ClientRpc(eventName, intParam1, intParam2);
        }

        public void DispatchAllClientEvent_Reliable(string eventName, int intParam1, int intParam2, int intParam3) {
            if (!IsServer)
                return;
            DispatchEvent_Reliable_ClientRpc(eventName, intParam1, intParam2, intParam3);
        }

        // ���ɿ�����
        public void DispatchAllClientEvent_UnReliable(string eventName, string paramStr) {
            if (!IsServer)
                return;
            DispatchEvent_Unreliable_ClientRpc(eventName, paramStr);
        }

        public void DispatchAllClientEvent_UnReliable(string eventName, int intParam) {
            if (!IsServer)
                return;
            DispatchEvent_Unreliable_ClientRpc(eventName, intParam);
        }

        public void DispatchAllClientEvent_UnReliable(string eventName, int intParam1, int intParam2) {
            if (!IsServer)
                return;
            DispatchEvent_Unreliable_ClientRpc(eventName, intParam1, intParam2);
        }

        public void DispatchAllClientEvent_UnReliable(string eventName, int intParam1, int intParam2, int intParam3) {
            if (!IsServer)
                return;
            DispatchEvent_Unreliable_ClientRpc(eventName, intParam1, intParam2, intParam3);
        }
        // ------------------- ���õ���Ӧ��Client�� ----------------------------------
        // �ɿ�����
        public void DispatchClientEvent_Reliable(string eventName, string paramStr) {
            if (!IsServer)
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
            if (!IsServer)
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
            if (!IsServer)
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

        public void DisptachClientEvent_Reliable(string eventName, int intParam1, int intParam2, int intParam3) {
            if (!IsServer)
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

        // �ǿɿ�����
        public void DispatchClientEvent_UnReliable(string eventName, string paramStr) {
            if (!IsServer)
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
            if (!IsServer)
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
            if (!IsServer)
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

        public void DispatchClientEvent_UnReliable(string eventName, int intParam1, int intParam2, int intParam3) {
            if (!IsServer)
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
        [ClientRpc(Delivery = RpcDelivery.Reliable)]
        protected void DispatchEvent_Reliable_ClientRpc(string eventName, int intParam1, int intParam2, int intParam3, ClientRpcParams clientRpcParams = default) {
            if (onClientInt3Event != null)
                onClientInt3Event(eventName, intParam1, intParam2, intParam3);
        }

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
        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        protected void DispatchEvent_Unreliable_ClientRpc(string eventName, int intParam1, int intParam2, int intParam3, ClientRpcParams clientRpcParams = default) {
            if (onClientInt3Event != null)
                onClientInt3Event(eventName, intParam1, intParam2, intParam3);
        }

        // -------------------------------------- ����ͻ��˶����Ե���
        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Reliable)]
        protected void DispatchEvent_Reliable_ServerRpc(string eventName, string paramStr, ServerRpcParams serverRpcParams = default) {
            if (onServerStrEvent != null)
                onServerStrEvent(eventName, paramStr);
        }

        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Reliable)]
        protected void DispatchEvent_Reliable_ServerRpc(string eventName, int intParam, ServerRpcParams serverRpcParams = default) {
            if (onServerIntEvent != null)
                onServerIntEvent(eventName, intParam);
        }

        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Reliable)]
        protected void DispatchEvent_Reliable_ServerRpc(string eventName, int intParam1, int intParam2, ServerRpcParams serverRpcParams = default) {
            if (onServerInt2Event != null)
                onServerInt2Event(eventName, intParam1, intParam2);
        }

        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Reliable)]
        protected void DispatchEvent_Reliable_ServerRpc(string eventName, int intParam1, int intParam2, int intParam3, ServerRpcParams serverRpcParams = default) {
            if (onServerInt3Event != null)
                onServerInt3Event(eventName, intParam1, intParam2, intParam3);
        }


        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
        protected void DispatchEvent_Unreliable_ServerRpc(string eventName, string paramStr, ServerRpcParams serverRpcParams = default) {
            if (onServerStrEvent != null)
                onServerStrEvent(eventName, paramStr);
        }

        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
        protected void DispatchEvent_Unreliable_ServerRpc(string eventName, int intParam, ServerRpcParams serverRpcParams = default) {
            if (onServerIntEvent != null)
                onServerIntEvent(eventName, intParam);
        }

        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
        protected void DispatchEvent_Unreliable_ServerRpc(string eventName, int intParam1, int intParam2, ServerRpcParams serverRpcParams = default) {
            if (onServerInt2Event != null)
                onServerInt2Event(eventName, intParam1, intParam2);
        }

        [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
        protected void DispatchEvent_Unreliable_ServerRpc(string eventName, int intParam1, int intParam2, int intParam3, ServerRpcParams serverRpcParams = default) {
            if (onServerInt3Event != null)
                onServerInt3Event(eventName, intParam1, intParam2, intParam3);
        }

        // ------------------------------ �ⲿ���� ------------------------
        public Action<string, string> onClientStrEvent {
            get; set;
        }

        public Action<string, int> onClientIntEvent {
            get; set;
        }
        public Action<string, int, int> onClientInt2Event {
            get; set;
        }
        public Action<string, int, int, int> onClientInt3Event {
            get; set;
        }

        public Action<string, string> onServerStrEvent {
            get; set;
        }

        public Action<string, int> onServerIntEvent {
            get; set;
        }
        public Action<string, int, int> onServerInt2Event {
            get; set;
        }
        public Action<string, int, int, int> onServerInt3Event {
            get; set;
        }
        // ----------------------------------------------------------------
    }
}
