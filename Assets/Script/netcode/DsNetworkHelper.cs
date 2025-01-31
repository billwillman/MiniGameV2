using System;
using UnityEngine;
using Unity.Netcode;

namespace SOC.GamePlay
{

    [XLua.LuaCallCSharp]
    public static class DsNetworkHelper
    {
        private static Action<bool> m_OnClientStopped = null;
        private static Action m_OnClientStarted = null;
        private static Action m_OnTransportFailure = null;
        private static Action<NetworkManager, ConnectionEventData> m_OnConnectionEvent = null;
        private static NetworkSceneManager.OnEventCompletedDelegateHandler m_OnLoadEventCompleted = null;
        private static NetworkSceneManager.OnEventCompletedDelegateHandler m_OnUnloadEventCompleted = null;

        public static void NetworkManager_ClearOnUnloadEventCompleted() {
            if (m_OnUnloadEventCompleted != null && NetworkManager.Singleton != null) {
                NetworkManager.Singleton.SceneManager.OnUnloadEventCompleted -= m_OnUnloadEventCompleted;
            }
            m_OnUnloadEventCompleted = null;
        }

        public static bool NetworkManager_SetOnUnloadEventCompleted(NetworkSceneManager.OnEventCompletedDelegateHandler onServerEvt) {
            if (NetworkManager.Singleton != null) {
                if (m_OnUnloadEventCompleted != null)
                    NetworkManager.Singleton.SceneManager.OnUnloadEventCompleted -= m_OnUnloadEventCompleted;
                if (onServerEvt != null)
                    NetworkManager.Singleton.SceneManager.OnUnloadEventCompleted += onServerEvt;
                m_OnUnloadEventCompleted = onServerEvt;
                return true;
            }
            return false;
        }

        public static void NetworkManager_ClearOnLoadEventCompleted() {
            if (m_OnLoadEventCompleted != null && NetworkManager.Singleton != null) {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= m_OnLoadEventCompleted;
            }
            m_OnLoadEventCompleted = null;
        }

        public static bool NetworkManager_SetOnLoadEventCompleted(NetworkSceneManager.OnEventCompletedDelegateHandler onServerEvt) {
            if (NetworkManager.Singleton != null) {
                if (m_OnLoadEventCompleted != null)
                    NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= m_OnLoadEventCompleted;
                if (onServerEvt != null)
                    NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += onServerEvt;
                m_OnLoadEventCompleted = onServerEvt;
                return true;
            }
            return false;
        }

        public static bool NetworkManager_SetOnClientStopped(Action<bool> onClientStopped) {
            if (NetworkManager.Singleton != null) {
                if (m_OnClientStopped != null)
                    NetworkManager.Singleton.OnClientStopped -= m_OnClientStopped;
                if (onClientStopped != null)
                    NetworkManager.Singleton.OnClientStopped += onClientStopped;
                m_OnClientStopped = onClientStopped;
                return true;
            }
            return false;
        }

        public static bool NetworkManager_SetOnClientStarted(Action onClientStarted) {
            if (NetworkManager.Singleton != null) {
                if (m_OnClientStarted != null)
                    NetworkManager.Singleton.OnClientStarted -= m_OnClientStarted;
                if (onClientStarted != null)
                    NetworkManager.Singleton.OnClientStarted += onClientStarted;
                m_OnClientStarted = onClientStarted;
                return true;
            }
            return false;
        }

        public static bool NetworkManager_SetOnTransportFailure(Action onTransportFailure) {
            if (NetworkManager.Singleton != null) {
                if (m_OnTransportFailure != null)
                    NetworkManager.Singleton.OnTransportFailure -= m_OnTransportFailure;
                if (onTransportFailure != null)
                    NetworkManager.Singleton.OnTransportFailure += onTransportFailure;
                m_OnTransportFailure = onTransportFailure;
                return true;
            }
            return false;
        }

        public static bool NetworkManager_SetOnConnectionEvent(Action<NetworkManager, ConnectionEventData> onConnectionEvent) {
            if (NetworkManager.Singleton != null) {
                if (m_OnConnectionEvent != null)
                    NetworkManager.Singleton.OnConnectionEvent -= m_OnConnectionEvent;
                if (onConnectionEvent != null)
                    NetworkManager.Singleton.OnConnectionEvent += onConnectionEvent;
                m_OnConnectionEvent = onConnectionEvent;
                return true;
            }
            return false;
        }

        public static void NetworkManager_ClearOnClientStopped() {
            if (m_OnClientStopped  != null && NetworkManager.Singleton != null) {
                NetworkManager.Singleton.OnClientStopped -= m_OnClientStopped;
            }
            m_OnClientStopped = null;
        }

        public static void NetworkManager_ClearOnClientStarted() {
            if (m_OnClientStarted != null && NetworkManager.Singleton != null) {
                NetworkManager.Singleton.OnClientStarted -= m_OnClientStarted;
            }
            m_OnClientStarted = null;
        }

        public static void NetworkManager_ClearOnTransportFailure() {
            if (m_OnTransportFailure != null && NetworkManager.Singleton != null) {
                NetworkManager.Singleton.OnTransportFailure -= m_OnTransportFailure;
            }
            m_OnTransportFailure = null;
        }

        public static void NetworkManager_ClearOnConnectionEvent() {
            if (m_OnConnectionEvent != null && NetworkManager.Singleton != null) {
                NetworkManager.Singleton.OnConnectionEvent -= m_OnConnectionEvent;
            }
            m_OnConnectionEvent = null;
        }

        public static void NetworkManager_ClearAllEvents() {
            NetworkManager_ClearOnClientStopped();
            NetworkManager_ClearOnClientStarted();
            NetworkManager_ClearOnTransportFailure();
            NetworkManager_ClearOnConnectionEvent();
            NetworkManager_ClearOnLoadEventCompleted();
            NetworkManager_ClearOnUnloadEventCompleted();
        }
    }
}
