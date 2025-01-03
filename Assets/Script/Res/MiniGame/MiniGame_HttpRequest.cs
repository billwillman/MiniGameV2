using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Utils;

public class MiniGame_HttpRequest: DisposeObject
{
    public MiniGame_HttpRequest(string url, bool isByteBufferRet = true) {
        m_Url = url;
        m_IsByteBufferRet = isByteBufferRet;
    }

    private void _CallResult() {
        if (m_Req == null)
            return;

        if (m_Req.isHttpError || m_Req.isNetworkError || m_Req.isNetworkError) {
            m_Req = null;
            if (OnResult != null)
                OnResult(this, false);
        } else if (m_Req.isDone) {
            if (m_IsByteBufferRet)
                m_ResponeBuffer = m_Req.downloadHandler.data;
            else
                m_ResponeText = m_Req.downloadHandler.text;
            m_Req = null;
            if (OnResult != null)
                OnResult(this, true);
        }
    }

    public IEnumerator Get() {
        Dispose();
        if (!string.IsNullOrEmpty(m_Url)) {
            m_Req = UnityWebRequest.Get(m_Url);
            yield return m_Req.SendWebRequest();
            _CallResult();
        } else {
            if (OnResult != null) {
                OnResult(this, false);
            }
            yield break;
        }
    }

    public IEnumerator Post(Dictionary<string, string> paramDict) {
        Dispose();
        if (paramDict == null || paramDict.Count <= 0 || string.IsNullOrEmpty(m_Url)) {
            if (OnResult != null) {
                OnResult(this, false);
            }
            yield break;
        }
        WWWForm form = new WWWForm();
        foreach (var item in paramDict) {
            if (!string.IsNullOrEmpty(item.Key)) {
                form.AddField(item.Key, item.Value);
            }
        }
        m_Req = UnityWebRequest.Post(m_Url, form);
        yield return m_Req.SendWebRequest();
        _CallResult();
    }

    protected override void OnFree(bool isManual) {
        if (m_Req != null) {
            bool isAbort = false;
            if (!m_Req.isDone && !m_Req.isHttpError && !m_Req.isNetworkError && !m_Req.isNetworkError) {
                m_Req.Abort();
                m_Req.Dispose();
                isAbort = true;
            }
            m_Req = null;
            if (isAbort && OnAbort != null)
                OnAbort(this);
        }
        m_ResponeBuffer = null;
        m_ResponeText = string.Empty;
    }

    public Action<MiniGame_HttpRequest> OnAbort {
        get;
        set;
    }

    public Action<MiniGame_HttpRequest, bool> OnResult {
        get;
        set;
    }

    public byte[] ResponeBuffer {
        get {
            return m_ResponeBuffer;
        }
    }

    public string ResponeText {
        get {
            return m_ResponeText;
        }

    }

    private string m_Url = string.Empty;
    private bool m_IsByteBufferRet = true;
    private UnityWebRequest m_Req = null;
    private byte[] m_ResponeBuffer = null;
    private string m_ResponeText = string.Empty;
}
