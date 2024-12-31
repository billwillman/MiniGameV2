using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Utils;

public class MiniGame_HttpRequest: DisposeObject
{
    public MiniGame_HttpRequest(string url) {
        m_Url = url;
    }

    public IEnumerator Get() {
        Dispose();
        if (!string.IsNullOrEmpty(m_Url)) {
            m_Req = UnityWebRequest.Get(m_Url);
            yield return m_Req.SendWebRequest();
            if (m_Req.isHttpError || m_Req.isNetworkError) {
                m_Req = null;
                if (OnResult != null)
                    OnResult(false);
            } else if (m_Req.isDone) {
                m_ResponeBuffer = m_Req.downloadHandler.data;
                m_Req = null;
                if (OnResult != null)
                    OnResult(true);
            }
        } else {
            if (OnResult != null) {
                OnResult(false);
            }
            yield break;
        }
    }

    protected override void OnFree(bool isManual) {
        if (m_Req != null) {
            bool isAbort = false;
            if (!m_Req.isDone && !m_Req.isHttpError && !m_Req.isNetworkError) {
                m_Req.Abort();
                m_Req.Dispose();
                isAbort = true;
            }
            m_Req = null;
            if (isAbort && OnAbort != null)
                OnAbort();
        }
        m_ResponeBuffer = null;
    }

    public Action OnAbort {
        get;
        set;
    }

    public Action<bool> OnResult {
        get;
        set;
    }

    public byte[] ResponeBuffer {
        get {
            return m_ResponeBuffer;
        }
    }

    private string m_Url = string.Empty;
    private UnityWebRequest m_Req = null;
    private byte[] m_ResponeBuffer = null;
}
