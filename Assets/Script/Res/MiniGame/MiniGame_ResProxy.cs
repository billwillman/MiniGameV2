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
}
