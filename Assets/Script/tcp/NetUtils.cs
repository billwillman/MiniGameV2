#if UNITY_WEIXINMINIGAME
#else
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;

namespace NsTcpClient
{

    public static class NetUtils
    {

        // 返回 0 表示无效的地址
        public static int GetFreePort_TcpV4()
        {
            try
            {
                IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
                TcpListener listener = new TcpListener(ipAddress, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            } catch
            {
                return 0;
            }
        }
    }
}

#endif
