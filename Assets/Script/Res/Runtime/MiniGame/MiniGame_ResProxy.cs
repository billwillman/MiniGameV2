#if UNITY_WEIXINMINIGAME && !UNITY_EDITOR
    #define _USE_WX
#endif

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
    [Tooltip("编辑模式下使用CDN的Mapper")]
    public bool UseCDNMapper = false;
    public string ResVersion {
        get;
        set;
    }

    static IEnumerator DoRequestFile(string url, Action<WebGame_HttpRequest, bool> onFinish, Action<WebGame_HttpRequest> onAbort) {
        // string versionUrl = string.Format("{0}/{1}/version.txt");
        WebGame_HttpRequest req = new WebGame_HttpRequest(url, false);
        req.OnAbort = onAbort;
        req.OnResult = onFinish;
        return req.Get();
    }

    protected Coroutine RequestFile(string url, Action<WebGame_HttpRequest, bool> onFinish, Action<WebGame_HttpRequest> onAbort) {
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
        AssetLoader.UseCDNMapper = false;
        WebAsseetBundleAsyncTask.CDN_RootDir = string.Empty;
        WebAsseetBundleAsyncTask.Mapper = null;
#if _USE_WX
        WXAssetBundleAsyncTask.CDN_RootDir = string.Empty;
        WXAssetBundleAsyncTask.Mapper = null;
#endif
    }

    public bool RequestStart(Action<bool> onFinish, Action onAbort)
    {
        Dispose();
        if (!UseCDNMapper || string.IsNullOrEmpty(CDNRoot) || string.IsNullOrEmpty(AppResVersion))
        {
            AssetLoader.UseCDNMapper = UseCDNMapper;
            onFinish(true); // 认为是本地读取
            return true;
        }
        AssetLoader.UseCDNMapper = UseCDNMapper;
        string baseUrl = string.Format("{0}/{1}", CDNRoot, AppResVersion);
#if _USE_WX
        WXAssetBundleAsyncTask.CDN_RootDir = baseUrl;
#else
        WebAsseetBundleAsyncTask.CDN_RootDir = baseUrl;
#endif
        string versionUrl = GenerateCDN_AppResVersion_Url("version.txt", true);
        Debug.Log("[RequestStart] versionUrl: " + versionUrl);
        RequestFile(versionUrl, (WebGame_HttpRequest req, bool isResult) =>
        {
            if (!isResult)
            {
                if (onFinish != null)
                    onFinish(false);
                return;
            }
            string versionData = req.ResponeText;
            if (string.IsNullOrEmpty(versionData))
            {
                if (onFinish != null)
                    onFinish(false);
                return;
            }
            VersionDataLoader versionDataLoader = new VersionDataLoader(versionData);
            ResVersion = versionDataLoader.Version; // 资源版本号
            string fileListUrl = GenerateCDN_AppResVersion_Url(versionDataLoader.FileListFileName);
            Debug.Log("[RequestStart] fileListUrl: " + fileListUrl);
            RequestFile(fileListUrl, (WebGame_HttpRequest req, bool isResult) =>
            {
                if (!isResult)
                {
                    if (onFinish != null)
                        onFinish(false);
                    return;
                }
                if (string.IsNullOrEmpty(req.ResponeText))
                {
                    if (onFinish != null)
                        onFinish(false);
                    return;
                }
                // fileList数据
#if _USE_WX
                WXAssetBundleAsyncTask.Mapper = new FileListDataLoader(req.ResponeText);
#else
                WebAsseetBundleAsyncTask.Mapper = new FileListDataLoader(req.ResponeText);
#endif
                //-------------------------
                onFinish(true);
            }, (WebGame_HttpRequest req) =>
            {
                if (onAbort != null)
                    onAbort();
            });
        }, (WebGame_HttpRequest req) =>
        {
            if (onAbort != null)
                onAbort();
        });
        return true;
    }
}
