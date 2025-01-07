#if UNITY_WEIXINMINIGAME

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WeChatWASM;

namespace NsTcpClient
{
    public class WXTcpClient: ITcpClient
    {
        private WXTCPSocket m_TcpSocket = null; // 微信 Tcp Socket
        private bool m_IsDispose = false;
        private eClientState m_State = eClientState.eCLIENT_STATE_NONE;
        private int m_HasReadSize = 0;

        // --------------------------- 微信TCP事件回调 ------------------
        private void WXTcpSocket_OnConnect(GeneralCallbackResult result) {
            Debug.LogWarning("[WXTcpSocket] OnConnect: " + result.errMsg);
            SetClientState(eClientState.eClient_STATE_CONNECTED);
        }
        private void WXTcpSocket_OnAbortOrConnectFail(GeneralCallbackResult result) {
            Debug.LogWarning("[WXTcpSocket] OnAbort: " + result.errMsg);
            if (GetState() == eClientState.eClient_STATE_CONNECTING)
                SetClientState(eClientState.eClient_STATE_CONNECT_FAIL);
            else
                SetClientState(eClientState.eClient_STATE_ABORT);
        }

        private void WXTcpSocket_OnMessage(TCPSocketOnMessageListenerResult msg) {

        }
        // --------------------------------------------------------------

        public WXTcpClient() {
            m_TcpSocket = WX.CreateTCPSocket();
            m_TcpSocket.OnConnect(WXTcpSocket_OnConnect);
            m_TcpSocket.OnError(WXTcpSocket_OnAbortOrConnectFail);
            m_TcpSocket.OnClose(WXTcpSocket_OnAbortOrConnectFail);
            m_TcpSocket.OnMessage(WXTcpSocket_OnMessage);
        }

        ~WXTcpClient() {
            Dispose(false);
        }

        public eClientState GetState() {
            return m_State;
        }

        private void SetClientState(eClientState uState) {
            m_State = uState;
        }

        public Action<TcpClient> OnThreadBufferProcess {
            get;
            set;
        }

        public bool Connect(string pRemoteIp, int uRemotePort, int mTimeOut = -1) {
            eClientState state = GetState();
            if ((state == eClientState.eClient_STATE_CONNECTING) || (state == eClientState.eClient_STATE_CONNECTED))
                return false;
            if (m_TcpSocket != null) {
                TCPSocketConnectOption opt = new TCPSocketConnectOption();
                opt.address = pRemoteIp;
                opt.port = uRemotePort;
                opt.timeout = ((double)mTimeOut) / 1000.0f;
                m_TcpSocket.Connect(opt);
                return true;
            }
            return false;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool Diposing) {
            if (!m_IsDispose) {

                if (m_TcpSocket != null) {
                    m_TcpSocket.OffConnect(WXTcpSocket_OnConnect);
                    m_TcpSocket.OffError(WXTcpSocket_OnAbortOrConnectFail);
                    m_TcpSocket.OffClose(WXTcpSocket_OnAbortOrConnectFail);
                    m_TcpSocket.OffMessage(WXTcpSocket_OnMessage);
                }

                if (Diposing) {
                    if (m_TcpSocket != null) {
                        m_TcpSocket.Close();
                        m_TcpSocket = null;
                    }

                    m_HasReadSize = 0;
                }

                // 释放非托管对象资源
                m_IsDispose = true;
            }
        }

        public int GetReadDataNoLock(byte[] pBuffer, int offset) {
            throw new NotImplementedException();
        }

        public bool HasReadData() {
            return (m_HasReadSize > 0);
        }

        public void Release() {
            Dispose();
        }

        public bool Send(byte[] pData, int bufSize = -1) {
            throw new NotImplementedException();
        }
    }

}

#endif