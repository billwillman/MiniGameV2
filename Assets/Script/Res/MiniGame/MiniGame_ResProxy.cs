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
}
