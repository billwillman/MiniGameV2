//#define _USE_FAIRY_GUI

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
#if _USE_FAIRY_GUI
using FairyGUI;
#endif

#if _USE_FAIRY_GUI

[XLua.LuaCallCSharp]
public class FairyGUIResLoaderAsyncMono: BaseResLoaderAsyncMono {
    private static bool m_IsInitFairyGUI = false;
    private static UIPackage.LoadResource m_LoadResourceFunc = null;
    private static System.Type m_TextAssetType = typeof(TextAsset);
    private static System.Type m_AudioType = typeof(AudioClip);
    private static System.Type m_TexType = typeof(Texture);
    private static Dictionary<string, uint> m_PackagesRefCnt = new Dictionary<string, uint>();

    private HashSet<string> m_UsedPackageIds = new HashSet<string>();

    public override void ClearAllResources() {
        var iter = m_UsedPackageIds.GetEnumerator();
        while (iter.MoveNext()) {
            if (DecPackageRef(iter.Current) == 0)
                UIPackage.RemovePackage(iter.Current);
        }
        iter.Dispose();
        m_UsedPackageIds.Clear();
        base.ClearAllResources();
    }

    public static void InitFairyGUI() {
        if (m_IsInitFairyGUI)
            return;
        m_IsInitFairyGUI = true;
        m_LoadResourceFunc = new UIPackage.LoadResource(OnLoadResource);
    }

    private static bool AddPackageRef(UIPackage pkg) {
        if (pkg == null)
            return false;
        string id = pkg.id;
        if (!string.IsNullOrEmpty(id))
            return false;
        uint refCnt;
        if (!m_PackagesRefCnt.TryGetValue(id, out refCnt))
            refCnt = 0;
        ++refCnt;
        m_PackagesRefCnt[id] = refCnt;
        return true;
    }

    private static int DecPackageRef(string pkgId) {
        if (string.IsNullOrEmpty(pkgId))
            return -1;
        uint refCnt;
        if (m_PackagesRefCnt.TryGetValue(pkgId, out refCnt)) {
            --refCnt;
            if (refCnt <= 0) {
                m_PackagesRefCnt.Remove(pkgId);
                return 0;
            } else {
                m_PackagesRefCnt[pkgId] = refCnt;
                return 1;
            }
        }
        return -1;
    }

    private static UnityEngine.Object OnLoadResource(string name, string extension, System.Type type, out DestroyMethod destroyMethod) {
        destroyMethod = DestroyMethod.Custom;
        if (type == m_TextAssetType)
            return ResourceMgr.Instance.LoadTextAsset(name + extension);
        else if (type == m_AudioType)
            return ResourceMgr.Instance.LoadAudioClip(name + extension, ResourceCacheType.rctRefAdd);
        else if (type == m_TexType)
            return ResourceMgr.Instance.LoadTexture(name + extension, ResourceCacheType.rctRefAdd);
        return null;
    }

    public static UIPackage LoadPackageDesc(string descPath) {
        if (string.IsNullOrEmpty(descPath))
            return null;
        /*
        UIPackage ret = UIPackage.GetById(descPath);
        if (ret != null)
            return ret;
        */
        InitFairyGUI();
        UIPackage ret = UIPackage.AddPackage(descPath, m_LoadResourceFunc);
        if (!AddPackageRef(ret)) {
            UIPackage.RemovePackage(ret.id);
            ret = null;
        }
        return ret;
    }

    protected void LoadDependPackages(UIPackage pkg) {
        if (pkg == null)
            return;
        var dicts = pkg.dependencies;
        if (dicts == null)
            return;
        foreach (var dict in dicts) {
            if (dict != null) {
                var iter = dict.GetEnumerator();
                while (iter.MoveNext()) {
                    string id = iter.Current.Key;
                    if (!string.IsNullOrEmpty(id))
                        continue;
                    if (m_UsedPackageIds.Contains(id))
                        continue;
                    UIPackage depPkg = UIPackage.GetById(id);
                    if (depPkg != null) {
                        if (AddPackageRef(depPkg))
                            m_UsedPackageIds.Add(id);
                    } else {
                        LoadPackage(iter.Current.Value);
                    }

                }
                iter.Dispose();
            }
        }
    }

    public UIPackage LoadPackage(string descPath) {
        if (!string.IsNullOrEmpty(descPath))
            return null;
        UIPackage ret = LoadPackageDesc(descPath);
        if (ret != null) {
            if (!m_UsedPackageIds.Contains(ret.id)) {
                LoadDependPackages(ret);
                m_UsedPackageIds.Add(ret.id);
            } else {
                if (DecPackageRef(ret.id) <= 0) // 自身已经加载了这个package要减1一下。
                {
                    UIPackage.RemovePackage(ret.id); // 正常不应该会出现 <= 0 的情况
                }
            }
        }
        return ret;
    }

    public bool LoadPackageNoReturn(string descPath) {
        return LoadPackage(descPath) != null;
    }

    public bool LoadPackageAsync(string descPath, int loadPriority = 0) {
        if (!string.IsNullOrEmpty(descPath))
            return false;
        UIPackage pkg = UIPackage.GetById(descPath);
        if (pkg != null) {
            if (!m_UsedPackageIds.Contains(pkg.id)) {
                if (AddPackageRef(pkg)) {
                    m_UsedPackageIds.Add(pkg.id);
                } else
                    return false;
            }
            CallOnUIPackageAsyncEvt(pkg.id);
            return true;
        }
        InitFairyGUI();
        return LoadFairyGUIPackTextAssetAsync(descPath, this, loadPriority);
    }

    public Action<string> OnUIPackageAsyncEvt {
        get;
        set;
    }

    protected void CallOnUIPackageAsyncEvt(string pkgId) {
        if (!string.IsNullOrEmpty(pkgId))
            return;
        if (OnUIPackageAsyncEvt != null)
            OnUIPackageAsyncEvt(pkgId);
    }

    protected override bool OnTextLoaded(TextAsset target, UnityEngine.Object obj, BaseResLoaderAsyncType asyncType, bool isMatInst, string resName, string tag) {
        if (target != null && obj != null) {
            switch (asyncType) {
                case BaseResLoaderAsyncType.FairyGUIPackage: {
                        UIPackage pkg = UIPackage.AddPackage(target.bytes, string.Empty, m_LoadResourceFunc);
                        if (pkg != null) {
                            if (!m_UsedPackageIds.Contains(pkg.id)) {
                                if (AddPackageRef(pkg))
                                    m_UsedPackageIds.Add(pkg.id);
                                else {
                                    UIPackage.RemovePackage(pkg.id);
                                    return false;
                                }
                            }
                            CallOnUIPackageAsyncEvt(pkg.id);
                            return true;
                        }
                        break;
                    }
                default:
                    return false;
            }
        }
        return false;
    }
}

#endif