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
        private WXTCPSocket m_TcpSocket = null; // н╒пе Tcp Socket

        public WXTcpClient() {
            m_TcpSocket = WX.CreateTCPSocket();
        }

        public Action<TcpClient> OnThreadBufferProcess { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool Connect(string pRemoteIp, int uRemotePort, int mTimeOut = -1) {
            throw new NotImplementedException();
        }

        public void Dispose() {
            throw new NotImplementedException();
        }

        public int GetReadDataNoLock(byte[] pBuffer, int offset) {
            throw new NotImplementedException();
        }

        public eClientState GetState() {
            throw new NotImplementedException();
        }

        public bool HasReadData() {
            throw new NotImplementedException();
        }

        public void Release() {
            throw new NotImplementedException();
        }

        public bool Send(byte[] pData, int bufSize = -1) {
            throw new NotImplementedException();
        }
    }

}

#endif