using System;
using System.Collections;

namespace NsTcpClient
{

	public enum eClientState
	{
		eCLIENT_STATE_NONE = 0,			
		eClient_STATE_CONNECTING,	
		eClient_STATE_CONNECTED,	
		eClient_STATE_CONNECT_FAIL,	
		eClient_STATE_ABORT,		
		eClient_STATE_DISCONNECT,
	};

	// Tcp Client Socket接口
	public interface ITcpClient: IDisposable
	{
		void Release();
		bool Connect(string pRemoteIp, int uRemotePort, int mTimeOut = -1);
		eClientState GetState();
		bool Send(byte[] pData, int bufSize = -1);
		bool HasReadData();

		Action<ITcpClient> OnThreadBufferProcess {
			get;
			set;
		}

		int GetReadDataNoLock(byte[] pBuffer, int offset);
	}  
}
