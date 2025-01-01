using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEIXINMINIGAME
using WeChatWASM;
#endif
// �ļ��������
public class MiniGame_ResProxyMgr: SingetonMono<MiniGame_ResProxyMgr> 
{
    public string CDNRoot = string.Empty;
    public string AppResVersion = string.Empty;
    private FileListDataLoader m_FileListLoader = null;
    public string ResVersion {
        get;
        set;
    }

    static IEnumerator DoRequestFile(string url, Action<MiniGame_HttpRequest, bool> onFinish, Action<MiniGame_HttpRequest> onAbort) {
        // string versionUrl = string.Format("{0}/{1}/version.txt");
        MiniGame_HttpRequest req = new MiniGame_HttpRequest(url, false);
        req.OnAbort = onAbort;
        req.OnResult = onFinish;
        return req.Get();
    }

    protected Coroutine RequestFile(string url, Action<MiniGame_HttpRequest, bool> onFinish, Action<MiniGame_HttpRequest> onAbort) {
        if (string.IsNullOrEmpty(url))
            return null;
        return StartCoroutine(DoRequestFile(url, onFinish, onAbort));
    }

    protected string GenerateCDN_AppResVersion_Url(string fileName) {
        string ret = string.Format("{0}/{1}/{2}", CDNRoot, AppResVersion, fileName);
        return ret;
    }

    protected void Dispose() {
        m_FileListLoader = null;
        ResVersion = string.Empty;
        StopAllCoroutines(); // ��������Coroutines
#if UNITY_WEIXINMINIGAME
        WXAssetBundleAsyncTask.CDN_RootDir = string.Empty;
#endif
    }

    public bool RequestStart(Action<bool> onFinish, Action onAbort) {
        Dispose();
        if (string.IsNullOrEmpty(CDNRoot) || string.IsNullOrEmpty(AppResVersion))
            return false;
#if UNITY_WEIXINMINIGAME
        WXAssetBundleAsyncTask.CDN_RootDir = string.Format("{0}/{1}", CDNRoot, AppResVersion);
#endif
        string versionUrl = GenerateCDN_AppResVersion_Url("version.txt");
        RequestFile(versionUrl, (MiniGame_HttpRequest req, bool isResult) =>
        {
            if (!isResult) {
                if (onFinish != null)
                    onFinish(false);
                return;
            }
            string versionData = req.ResponeText;
            if (string.IsNullOrEmpty(versionData)) {
                if (onFinish != null)
                    onFinish(false);
                return;
            }
            VersionDataLoader versionDataLoader = new VersionDataLoader(versionData);
            ResVersion = versionDataLoader.Version; // ��Դ�汾��
            string fileListUrl = GenerateCDN_AppResVersion_Url(versionDataLoader.FileListFileName);
            RequestFile(fileListUrl, (MiniGame_HttpRequest req, bool isResult) =>
            {
                if (!isResult) {
                    if (onFinish != null)
                        onFinish(false);
                    return;
                }
                if (string.IsNullOrEmpty(req.ResponeText)) {
                    if (onFinish != null)
                        onFinish(false);
                    return;
                }
                // fileList����
                m_FileListLoader = new FileListDataLoader(req.ResponeText);
                //-------------------------
            }, (MiniGame_HttpRequest req) =>
            {
                if (onAbort != null)
                    onAbort();
            });
        }, (MiniGame_HttpRequest req)=>
        {
            if (onAbort != null)
                onAbort();
        });
        return true;
    }
}
