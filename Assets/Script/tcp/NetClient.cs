//#define USE_PROTOBUF_NET

using System;
using System.Collections.Generic;
using Utils;

namespace NsTcpClient
{

    [XLua.LuaCallCSharp]
    public class NetClient : DisposeObject
    {
        // 清理
        protected override void OnFree(bool isManual) {
            Disconnect();
        }

        public NetClient() {
            m_Client = new ClientSocket();
            m_Client.AddStateEvent(OnSocketStateEvent);
            m_Timer = TimerMgr.Instance.CreateTimer(0, true);
            m_Timer.AddListener(OnTimerEvent);

            NetByteArrayPool.InitMgr();
        }

        public string Ip {
            get {
                return m_Ip;
            }
        }

        public int Port {
            get {
                return m_Port;
            }
        }

        public bool ConnectLastServer() {
            return ConnectServer(m_Ip, m_Port);
        }

        public bool ConnectServer(string ip, int port, bool isMoonServer = false) {
            if (m_Client == null || string.IsNullOrEmpty(ip) || port <= 0)
                return false;

            if (string.Compare(m_Ip, ip) == 0 && m_Port == port) {
                var status = m_Client.GetState();
                if (status == eClientState.eClient_STATE_CONNECTED || status == eClientState.eClient_STATE_CONNECTING)
                    return true;
                else
                    m_Client.DisConnect();
            } else {
                if (m_Client != null)
                    m_Client.DisConnect();
            }

            ClearTempList();

            bool ret;
            if (isMoonServer)
                ret = m_Client.ConnectMoonServer(ip, port);
            else
                ret = m_Client.Connect(ip, port);

            if (ret) {
                m_Ip = ip;
                m_Port = port;
            }

            return ret;
        }


        void OnSocketStateEvent(eClientState status) {
            switch (status) {
                case eClientState.eClient_STATE_ABORT:
                    ClearTempList();
                    if (OnSocketAbort != null)
                        OnSocketAbort();
                    break;
                case eClientState.eClient_STATE_CONNECT_FAIL:
                    ClearTempList();
                    if (OnConnectResult != null)
                        OnConnectResult(false);
                    break;
                case eClientState.eClient_STATE_CONNECTED:
                    SendTempList();
                    if (OnConnectResult != null)
                        OnConnectResult(true);
                    break;
            }
        }

        private void SendTempList() {
            LinkedListNode<TempPacket> node = m_TempList.First;
            while (node != null) {
                if (node.Value.isMoonServer)
                    m_Client.SendMoonPacket(node.Value.data);
                else
                    m_Client.Send(node.Value.data, node.Value.handle);
                node = node.Next;
            }

            ClearTempList();
        }

        void OnTimerEvent(Timer obj, float timer) {
            if (m_Client != null) {
                m_Client.Execute();
            }
        }

        [XLua.CSharpCallLua]
        public Action OnSocketAbort {
            get;
            set;
        }

        // 连接返回
        [XLua.CSharpCallLua]
        public Action<bool> OnConnectResult {
            get;
            set;
        }

        [XLua.CSharpCallLua]
        public OnMoonPacketRead OnMoonPacketRead {
            get {
                if (m_Client != null)
                    return m_Client.MoonPacketRead;
                return null;
            }
            set {
                if (m_Client != null)
                    m_Client.MoonPacketRead = value;
            }
        }

        public eClientState ClietnState {
            get {
                if (m_Client == null)
                    return eClientState.eCLIENT_STATE_NONE;
                return m_Client.GetState();
            }
        }

        public void Disconnect() {
            if (m_Client != null)
                m_Client.DisConnect();
            ClearTempList();
        }

        private void ClearTempList() {
            m_TempList.Clear();
        }

        public void AddPacketListener(int header, OnPacketRead callBack) {
            if (m_Client == null)
                return;
            m_Client.AddPacketListener(header, callBack);
        }

        public void AddServerMessageClass(int header, System.Type messageClass) {
            if (m_Client == null || messageClass == null)
                return;
            m_Client.RegisterServerMessageClass(header, messageClass);
        }

        public void SendMoonServer(byte[] buf, int bufSize = -1) {
            if (m_Client == null)
                return;
            var status = m_Client.GetState();
            if (status == eClientState.eClient_STATE_CONNECTED) {
                m_Client.SendMoonPacket(buf, bufSize);
            } else if (status == eClientState.eClient_STATE_CONNECTING) {
                TempPacket temp = new TempPacket(buf, 0, bufSize, true);
                m_TempList.AddLast(temp);
            }
        }

        public void Send(byte[] buf, int packetHandle, int bufSize = -1) {
            if (m_Client == null)
                return;
            var status = m_Client.GetState();
            if (status == eClientState.eClient_STATE_CONNECTED) {
                m_Client.Send(buf, packetHandle, bufSize);
            } else if (status == eClientState.eClient_STATE_CONNECTING) {
                TempPacket temp = new TempPacket(buf, packetHandle, bufSize, false);
                m_TempList.AddLast(temp);
            }
        }

        public void Send(int packetHandle) {
            if (m_Client == null)
                return;
            Send(null, packetHandle);
        }

#if USE_PROTOBUF_NET
            public void SendProtoBuf<T>(T data, int packetHandle) where T: class, Google.Protobuf.IMessage<T>
        {
            if (data == null)
            {
                return;
            }

            // ProtoBuf 2.0接口
            /*
			System.IO.MemoryStream stream = new System.IO.MemoryStream ();
			ProtoBuf.Serializer.Serialize<T> (stream, data);
			byte[] buf = stream.ToArray ();
			stream.Close ();
			stream.Dispose ();
			Send (buf, packetHandle);
            */

            // protobuf 3.0接口
           //  byte[] buf = ProtoMessageMgr.GetInstance().ToBuffer<T>(data);
           // Send(buf, packetHandle);
            
            // 优化后版本使用byte[]池
            int outSize;
            var stream = ProtoMessageMgr.ToBufferNode<T>(data, out outSize);
            if (stream == null)
                return;
            try
            {
                if (outSize <= 0)
                    return;
                var buf = stream.GetBuffer();
                Send(buf, packetHandle, outSize);
            }
            finally
            {
                if (stream != null)
                {
                    stream.Dispose();
                    stream = null;
                }
            }
            
        }
#endif

        public void SendStr(string data, int packetHandle) {
            if (m_Client == null)
                return;
            byte[] src;
            if (string.IsNullOrEmpty(data))
                src = null;
            else
                src = System.Text.Encoding.UTF8.GetBytes(data);
            Send(src, packetHandle);
        }

        public void SendMoonStr(string data) {
            if (m_Client == null)
                return;
            byte[] src;
            if (string.IsNullOrEmpty(data))
                src = null;
            else
                src = System.Text.Encoding.UTF8.GetBytes(data);
            SendMoonServer(src);
        }

        // 发送AbstractClientMessage
        public void SendMessage(int packetHandle, AbstractClientMessage message) {
            if (m_Client == null)
                return;
            if (message != null) {
                try {
                    message.DoSend();
                    long bufSize;
                    byte[] buffer = message.GetBuffer(out bufSize);
                    if (bufSize > int.MaxValue)
                        return;
                    Send(buffer, packetHandle, (int)bufSize);
                } finally {
                    message.Dispose();
                }

            } else {
                Send(packetHandle);
            }
        }


        private LinkedList<TempPacket> m_TempList = new LinkedList<TempPacket>();
        private string m_Ip = string.Empty;
        private int m_Port = 0;
        private ClientSocket m_Client = null;
        private ITimer m_Timer = null;
    }
}
