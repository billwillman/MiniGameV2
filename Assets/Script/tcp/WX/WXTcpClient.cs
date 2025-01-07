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

        public WXTcpClient() {
            m_TcpSocket = WX.CreateTCPSocket();
        }

        ~WXTcpClient() {
            Dispose(false);
        }

        public eClientState GetState() {
            return m_State;
        }

        public Action<TcpClient> OnThreadBufferProcess {
            get;
            set;
        }

        public bool Connect(string pRemoteIp, int uRemotePort, int mTimeOut = -1) {
            throw new NotImplementedException();
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool Diposing) {
            if (!m_IsDispose) {

                if (Diposing) {
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