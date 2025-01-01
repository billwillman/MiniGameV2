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

    protected string GenerateCDN_AppResVersion_Url(string fileName) {
        string ret = string.Format("{0}/{1}/{2}", CDNRoot, AppResVersion, fileName);
        return ret;
    }

    public bool RequestStart(Action<bool> onFinish, Action onAbort) {
        ResVersion = string.Empty;
        StopAllCoroutines(); // 禁用所有Coroutines
        if (string.IsNullOrEmpty(CDNRoot) || string.IsNullOrEmpty(AppResVersion))
            return false;
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
            ResVersion = versionDataLoader.Version; // 资源版本号
            string fileListUrl = GenerateCDN_AppResVersion_Url(versionDataLoader.FileListFileName);
            RequestFile(fileListUrl, (MiniGame_HttpRequest req, bool isResult) =>
            {
                if (!isResult) {
                    if (onFinish != null)
                        onFinish(false);
                    return;
                }
                // fileList数据
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
