#if UNITY_WEIXINMINIGAME

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using WeChatWASM;

namespace NsTcpClient
{
    public class WXTcpClient: ITcpClient
    {
        static public readonly int MAX_TCP_CLIENT_BUFF_SIZE = (64 * 1024);

        private WXTCPSocket m_TcpSocket = null; // 微信 Tcp Socket
        private bool m_IsDispose = false;
        private eClientState m_State = eClientState.eCLIENT_STATE_NONE;
        private int m_HasReadSize = 0;
        private byte[] m_ReadBuffer = new byte[MAX_TCP_CLIENT_BUFF_SIZE];

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

                    m_ReadBuffer = null;
                    m_HasReadSize = 0;
                }

                // 释放非托管对象资源
                m_IsDispose = true;
            }
        }

        public int GetReadDataNoLock(byte[] pBuffer, int offset) {
            if (pBuffer == null)
                return 0;

            int bufSize = pBuffer.Length;
            if (bufSize <= 0)
                return 0;

            int ret = bufSize - offset;

            if (ret <= 0) {
                // mei you chu li wan
                ret = 0;
                return ret;
            }

            if (ret > m_HasReadSize)
                ret = m_HasReadSize;

            Buffer.BlockCopy(m_ReadBuffer, 0, pBuffer, offset, ret);
            int uLast = m_HasReadSize - ret;

            Buffer.BlockCopy(m_ReadBuffer, ret, m_ReadBuffer, 0, uLast);
            m_HasReadSize = uLast;

            return ret;
        }

        public bool HasReadData() {
            return (m_HasReadSize > 0);
        }

        public void Release() {
            Dispose();
        }

        public unsafe bool Send(byte[] pData, int bufSize = -1) {
            if (pData == null || m_TcpSocket == null)
                return false;
            if (bufSize < 0 || bufSize >= pData.Length) {
                m_TcpSocket.Write(pData);
            } else {
                NativeArray<byte> tempBuffer = new NativeArray<byte>(bufSize, Allocator.Temp);
                try {
                    fixed (byte* src = pData) {
                        Buffer.MemoryCopy(src, tempBuffer.GetUnsafePtr(), bufSize, bufSize);
                    }
                    m_TcpSocket.Write(tempBuffer.ToArray());
                } finally {
                    tempBuffer.Dispose();
                }
            }
            return true;
        }
    }

}

#endif