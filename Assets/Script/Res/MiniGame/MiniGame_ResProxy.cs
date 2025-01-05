using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEIXINMINIGAME
using WeChatWASM;
#endif
// 文件代理管理
public class MiniGame_ResProxyMgr: SingetonMono<MiniGame_ResProxyMgr> 
{
    public string CDNRoot = string.Empty;
    public string AppResVersion = string.Empty;
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

    protected string GenerateCDN_AppResVersion_Url(string fileName, bool isAddTimer = false) {
        string ret = string.Format("{0}/{1}/{2}", CDNRoot, AppResVersion, fileName);
        if (isAddTimer) {
            string timeStr = NsHttpClient.HttpHelper.GetTimeStampStr();
            if (ret.IndexOf('?') > 0)
                ret = StringHelper.Concat(ret, StringHelper.Format("&t={0}", timeStr));
            else
                ret = StringHelper.Concat(ret, StringHelper.Format("?t={0}", timeStr));
        }
        return ret;
    }

    protected void Dispose() {
        ResVersion = string.Empty;
        StopAllCoroutines(); // 禁用所有Coroutines
#if UNITY_WEIXINMINIGAME
        WXAssetBundleAsyncTask.CDN_RootDir = string.Empty;
        WXAssetBundleAsyncTask.Mapper = null;
#endif
    }

    public bool RequestStart(Action<bool> onFinish, Action onAbort) {
#if UNITY_WEIXINMINIGAME
        Dispose();
        if (string.IsNullOrEmpty(CDNRoot) || string.IsNullOrEmpty(AppResVersion)) {
            onFinish(true); // 认为是本地读取
            return true;
        }
        WXAssetBundleAsyncTask.CDN_RootDir = string.Format("{0}/{1}", CDNRoot, AppResVersion);
        string versionUrl = GenerateCDN_AppResVersion_Url("version.txt", true);
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
            ResVersion = versionDataLoader.Version; // 资源版本号
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
                // fileList数据
                WXAssetBundleAsyncTask.Mapper = new FileListDataLoader(req.ResponeText);
                //-------------------------
                onFinish(true);
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
#else
        onFinish(true);
        return true;
#endif
    }
}
