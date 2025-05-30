// 任务队列

#if WEIXINMINIGAME && !UNITY_EDITOR
	#define _USE_WX
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;
using UnityEngine.Networking;
#if UNITY_WEIXINMINIGAME
using WeChatWASM;
//using Unity.InstantGame;
//using Unity.AutoStreaming;
#endif

// 任务接口
public abstract class ITask
{
	// 是否已经好了
	public bool IsDone {
		get
		{
			return (mResult != 0);
		}
	}

	// 结果
	public int Result {
		get
		{
			return mResult;
		}
		set
		{
			mResult = value;
		}
	}

	public bool IsDoing
	{
		get {
			return mResult == 0;
		}
	}

	public bool IsOk
	{
		get {
			return mResult > 0;
		}
	}

	public bool IsFail
	{
		get {
			return mResult < 0;
		}
	}

	public void AddResultEvent(Action<ITask> evt)
	{
		if (OnResult == null)
			OnResult = evt;
		else
			OnResult += evt;
	}

	// 执行回调
	public Action<ITask> OnResult {
		get;
		protected set;
	}

	// 用户数据
	public System.Object UserData {
		get;
		set;
	}

	// 设置拥有者
	public TaskList _Owner
	{
		get {
			return mOwner;
		}

		set {
			mOwner = value;
		}
	}

	// 处理
	public abstract void Process();
	public virtual void Release()
	{
	}

	public virtual void QuickLoaded() { }

	protected void TaskOk()
	{
		mResult = 1;
	}

	protected void TaskFail()
	{
		mResult = -1;
	}

	protected int mResult = 0;
	private TaskList mOwner = null;
}

#if UNITY_5_3 || UNITY_5_4 || UNITY_5_5 || UNITY_5_6 || UNITY_2018 || UNITY_2019 || UNITY_2017 || UNITY_2017_1_OR_NEWER

public abstract class IAssetBundleAsyncTask: ITask
{
	public abstract AssetBundle StartLoad();
	public abstract AssetBundle Bundle {
		get;
    }
}

// 使用UnityWebRequest来加载AB
public class WebAsseetBundleAsyncTask: IAssetBundleAsyncTask
{
	public static string CDN_RootDir = string.Empty; // 设置CDN的地址
	public static IWebAssetBundleMapper Mapper = null;
	public WebAsseetBundleAsyncTask() { }
	public WebAsseetBundleAsyncTask(string createFileName, int priority = 0)
    {
		if (string.IsNullOrEmpty(createFileName))
		{
			TaskFail();
			return;
		}
		if (Mapper != null)
		{
			string urlFileName = GetCDNFileName(createFileName);
			if (!string.IsNullOrEmpty(urlFileName))
				createFileName = urlFileName;
		}
		m_FileName = createFileName;
		m_Priority = priority;
	}

	public static int GetPoolCount()
	{
		return m_Pool.Count;
	}

	public Action<WebAsseetBundleAsyncTask> OnProcess
	{
		get;
		set;
	}

	public string FileName
	{
		get
		{
			return m_FileName;
		}
	}

	private static WebAsseetBundleAsyncTask GetNewTask()
	{
		if (m_UsePool)
		{
			InitPool();
			WebAsseetBundleAsyncTask ret = m_Pool.GetObject();
			if (ret != null)
				ret.m_IsInPool = false;
			return ret;
		}

		return new WebAsseetBundleAsyncTask();
	}

	public static WebAsseetBundleAsyncTask Create(string createFileName, int priority = 0)
	{
		if (string.IsNullOrEmpty(createFileName))
			return null;
		if (Mapper != null)
		{
			string urlFileName = GetCDNFileName(createFileName);
			if (!string.IsNullOrEmpty(urlFileName))
				createFileName = urlFileName;
		}
		WebAsseetBundleAsyncTask ret = GetNewTask();
		ret.m_FileName = createFileName;
		ret.m_Priority = priority;
		return ret;
	}

	public override AssetBundle Bundle
	{
		get
		{
			return m_Bundle;
		}
	}

	public float Progress
	{
		get
		{
			return m_Progress;
		}
	}



	public override void QuickLoaded()
	{
		if (m_Req != null)
		{
			m_Bundle = (m_Req.downloadHandler as DownloadHandlerAssetBundle).assetBundle; // 快速加载下，这样打断异步立马加载
		}
	}

	public override void Release()
	{
		base.Release();
		ItemPoolReset();
		InPool(this);
	}

	public static string GetCDNFileName(string fileName)
	{
		if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(CDN_RootDir))
			return fileName;
		if (Mapper != null)
		{
			string onlyFileName = System.IO.Path.GetFileName(fileName);
			string targetFileName = Mapper.GetCDNFileName(onlyFileName);
			if (!string.IsNullOrEmpty(targetFileName))
			{
				fileName = targetFileName;
				string url = CDN_RootDir;
				if (url[url.Length - 1] != '/')
					url += "/";
				fileName = url + fileName;
			}
		}
		return fileName;
	}

	public static bool HasCDNFile(string fileName)
	{
		if (string.IsNullOrEmpty(fileName))
			return false;
		if (Mapper != null)
		{
			string onlyFileName = System.IO.Path.GetFileName(fileName);
			string targetFileName = Mapper.GetCDNFileName(onlyFileName);
			bool ret = targetFileName != fileName;
			return ret;
		}
		return false;
	}

	public override AssetBundle StartLoad()
	{
		if (m_Req == null)
		{
			string url = GetCDNFileName(m_FileName);
			/*
			string url = CDN_RootDir;
			if (string.IsNullOrEmpty(url))
				url = m_FileName;
			else {
				if (url[url.Length - 1] != '/')
					url += "/";
				url += m_FileName;
			}
			*/
			m_Req = UnityWebRequestAssetBundle.GetAssetBundle(url);
			m_AsyncOpt = m_Req.SendWebRequest();
			if (m_Req.isDone)
				return (m_Req.downloadHandler as DownloadHandlerAssetBundle).assetBundle;
			if (m_AsyncOpt != null)
				m_AsyncOpt.priority = m_Priority;
		}
		else if (m_Req.isDone)
			return (m_Req.downloadHandler as DownloadHandlerAssetBundle).assetBundle;

		return null;
	}

	public override void Process()
	{
		// 可以加载后面的LOAD
		StartLoad();

		if (m_Req == null)
		{
			TaskFail();
			return;
		}

		if (m_Req.isDone)
		{
			DownloadHandlerAssetBundle handler = m_Req.downloadHandler as DownloadHandlerAssetBundle;
			if (handler.assetBundle != null)
			{
				m_Progress = 1.0f;
				TaskOk();
				m_Bundle = handler.assetBundle;
			}
			else
				TaskFail();

			m_Req = null;
		}
		else if (m_Req.isHttpError || m_Req.isNetworkError || m_Req.isNetworkError)
		{
			TaskFail();
		}
		else
		{
			if (m_AsyncOpt != null)
				m_Progress = m_AsyncOpt.progress;
			else
				m_Progress = 0;
		}

		if (OnProcess != null)
			OnProcess(this);

	}


	private static void PoolReset(WebAsseetBundleAsyncTask task)
	{
		if (task == null)
			return;
		task.ItemPoolReset();
	}

	private static void InitPool()
	{
		if (m_PoolInited)
			return;
		m_PoolInited = true;
		m_Pool.Init(0, null, PoolReset);
	}

	private static void InPool(WebAsseetBundleAsyncTask task)
	{
		if (!m_UsePool || task == null || task.m_IsInPool)
			return;
		InitPool();
		m_Pool.Store(task);
		task.m_IsInPool = true;
	}

	private void ItemPoolReset()
	{
		if (m_Req != null)
		{
			m_Req = null;
		}

		m_AsyncOpt = null;
		m_Priority = 0;
		OnResult = null;
		OnProcess = null;
		m_Bundle = null;
		m_Progress = 0;
		m_FileName = string.Empty;
		mResult = 0;
		UserData = null;
		_Owner = null;
	}

	private string m_FileName = string.Empty;
	private bool m_IsInPool = false;
	private UnityWebRequest m_Req = null;
	private UnityWebRequestAsyncOperation m_AsyncOpt = null;
	private int m_Priority = 0;

	private float m_Progress = 0;
	private AssetBundle m_Bundle = null;

	private static bool m_UsePool = true;
	private static bool m_PoolInited = false;
	private static Utils.ObjectPool<WebAsseetBundleAsyncTask> m_Pool = new Utils.ObjectPool<WebAsseetBundleAsyncTask>();

}

#if UNITY_WEIXINMINIGAME
public class WXAssetBundleAsyncTask: IAssetBundleAsyncTask
{
	public static string CDN_RootDir = string.Empty; // 设置CDN的地址
	public static IWebAssetBundleMapper Mapper = null;
	public WXAssetBundleAsyncTask() {}
	public WXAssetBundleAsyncTask(string createFileName, int priority = 0) {
		if (string.IsNullOrEmpty(createFileName)) {
			TaskFail();
			return;
		}
		if (Mapper != null) {
			string urlFileName = GetCDNFileName(createFileName);
			if (!string.IsNullOrEmpty(urlFileName))
				createFileName = urlFileName;
		}
		m_FileName = createFileName;
		m_Priority = priority;
	}

	public static int GetPoolCount() {
		return m_Pool.Count;
	}

	public Action<WXAssetBundleAsyncTask> OnProcess {
		get;
		set;
	}

	public string FileName {
		get {
			return m_FileName;
		}
	}

	private static WXAssetBundleAsyncTask GetNewTask() {
		if (m_UsePool) {
			InitPool();
			WXAssetBundleAsyncTask ret = m_Pool.GetObject();
			if (ret != null)
				ret.m_IsInPool = false;
			return ret;
		}

		return new WXAssetBundleAsyncTask();
	}

	public static WXAssetBundleAsyncTask Create(string createFileName, int priority = 0) {
		if (string.IsNullOrEmpty(createFileName))
			return null;
		if (Mapper != null)
		{
			string urlFileName = GetCDNFileName(createFileName);
			if (!string.IsNullOrEmpty(urlFileName))
				createFileName = urlFileName;
		}
		WXAssetBundleAsyncTask ret = GetNewTask();
		ret.m_FileName = createFileName;
		ret.m_Priority = priority;
		return ret;
	}

	public override AssetBundle Bundle {
		get {
			return m_Bundle;
		}
	}

	public float Progress {
		get {
			return m_Progress;
		}
	}



	public override void QuickLoaded() {
		if (m_Req != null) {
			m_Bundle = (m_Req.downloadHandler as DownloadHandlerWXAssetBundle).assetBundle; // 快速加载下，这样打断异步立马加载
		}
	}

	public override void Release() {
		base.Release();
		ItemPoolReset();
		InPool(this);
	}

	public static string GetCDNFileName(string fileName) {
		if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(CDN_RootDir))
			return fileName;
		if (Mapper != null) {
			string onlyFileName = System.IO.Path.GetFileName(fileName);
			string targetFileName = Mapper.GetCDNFileName(onlyFileName);
			if (!string.IsNullOrEmpty(targetFileName)) {
				fileName = targetFileName;
				string url = CDN_RootDir;
				if (url[url.Length - 1] != '/')
					url += "/";
				fileName = url + fileName;
			}
        }
		return fileName;
	}

	public static bool HasCDNFile(string fileName) {
		if (string.IsNullOrEmpty(fileName))
			return false;
		if (Mapper != null) {
			string onlyFileName = System.IO.Path.GetFileName(fileName);
			string targetFileName = Mapper.GetCDNFileName(onlyFileName);
			bool ret = targetFileName != fileName;
			return ret;
		}
		return false;
    }

	public override AssetBundle StartLoad() {
		if (m_Req == null) {
			string url = GetCDNFileName(m_FileName);
			/*
			string url = CDN_RootDir;
			if (string.IsNullOrEmpty(url))
				url = m_FileName;
			else {
				if (url[url.Length - 1] != '/')
					url += "/";
				url += m_FileName;
			}
			*/
			m_Req = WXAssetBundle.GetAssetBundle(url);
			m_AsyncOpt = m_Req.SendWebRequest();
			if (m_Req.isDone)
				return (m_Req.downloadHandler as DownloadHandlerWXAssetBundle).assetBundle;
			if (m_AsyncOpt != null)
				m_AsyncOpt.priority = m_Priority;
		} else if (m_Req.isDone)
			return (m_Req.downloadHandler as DownloadHandlerWXAssetBundle).assetBundle;

		return null;
	}

	public override void Process() {
		// 可以加载后面的LOAD
		StartLoad();

		if (m_Req == null) {
			TaskFail();
			return;
		}

		if (m_Req.isDone) {
			DownloadHandlerWXAssetBundle handler = m_Req.downloadHandler as DownloadHandlerWXAssetBundle;
			if (handler.assetBundle != null) {
				m_Progress = 1.0f;
				TaskOk();
				m_Bundle = handler.assetBundle;
			} else
				TaskFail();

			m_Req = null;
		} else if (m_Req.isHttpError || m_Req.isNetworkError || m_Req.isNetworkError){
			TaskFail();
		} else {
			if (m_AsyncOpt != null)
				m_Progress = m_AsyncOpt.progress;
			else
				m_Progress = 0;
		}

		if (OnProcess != null)
			OnProcess(this);

	}


	private static void PoolReset(WXAssetBundleAsyncTask task) {
		if (task == null)
			return;
		task.ItemPoolReset();
	}

	private static void InitPool() {
		if (m_PoolInited)
			return;
		m_PoolInited = true;
		m_Pool.Init(0, null, PoolReset);
	}

	private static void InPool(WXAssetBundleAsyncTask task) {
		if (!m_UsePool || task == null || task.m_IsInPool)
			return;
		InitPool();
		m_Pool.Store(task);
		task.m_IsInPool = true;
	}

	private void ItemPoolReset() {
		if (m_Req != null) {
			m_Req = null;
		}

		m_AsyncOpt = null;
		m_Priority = 0;
		OnResult = null;
		OnProcess = null;
		m_Bundle = null;
		m_Progress = 0;
		m_FileName = string.Empty;
		mResult = 0;
		UserData = null;
		_Owner = null;
	}

	private string m_FileName = string.Empty;
	private bool m_IsInPool = false;
	private UnityWebRequest m_Req = null;
	private UnityWebRequestAsyncOperation m_AsyncOpt = null;
	private int m_Priority = 0;

	private float m_Progress = 0;
	private AssetBundle m_Bundle = null;

	private static bool m_UsePool = true;
	private static bool m_PoolInited = false;
	private static Utils.ObjectPool<WXAssetBundleAsyncTask> m_Pool = new Utils.ObjectPool<WXAssetBundleAsyncTask>();
}
#endif

// LoadFromFileAsync
public class BundleCreateAsyncTask: IAssetBundleAsyncTask
{
	public BundleCreateAsyncTask(string createFileName, int priority = 0)
	{
		if (string.IsNullOrEmpty(createFileName))
		{
			TaskFail();
			return;
		}

		m_FileName = createFileName;
		m_Priority = priority;
	}

	public BundleCreateAsyncTask()
	{}

	public static int GetPoolCount()
	{
		return m_Pool.Count;
	}

	public static BundleCreateAsyncTask Create(string createFileName, int priority = 0)
	{
		if (string.IsNullOrEmpty(createFileName))
			return null;
		BundleCreateAsyncTask ret = GetNewTask();
		ret.m_FileName = createFileName;
		ret.m_Priority = priority;
		return ret;
	}

	public static BundleCreateAsyncTask LoadFileAtStreamingAssetsPath(string fileName, bool usePlatform, int priority = 0)
	{
		fileName = WWWFileLoadTask.GetStreamingAssetsPath(usePlatform, true) + "/" + fileName;
		BundleCreateAsyncTask ret = Create(fileName, priority);
		return ret;
	}

	public override AssetBundle StartLoad() {
		if (m_Req == null) {
//#if _USE_WX
//            m_Req = WXAssetBundle.LoadFromFileAsync(m_FileName);
//#else
            m_Req = AssetBundle.LoadFromFileAsync(m_FileName);
//#endif
			if (m_Req != null) {
				m_Req.priority = m_Priority;
				if (m_Req.isDone)
					return m_Req.assetBundle;
			}
		} else if (m_Req.isDone)
			return m_Req.assetBundle;

			return null;
	}

	public override void QuickLoaded() {
		if (m_Req != null) {
			m_Bundle = m_Req.assetBundle; // 快速加载下，这样打断异步立马加载
		}
	}

	public override void Release()
	{
		base.Release();
		ItemPoolReset();
		InPool(this);
	}

	public override void Process()
	{
		// 可以加载后面的LOAD
		StartLoad();

		if (m_Req == null)
		{
			TaskFail();
			return;
		}

		if (m_Req.isDone) 
		{
			if (m_Req.assetBundle != null) {
				m_Progress = 1.0f;
				TaskOk ();
				m_Bundle = m_Req.assetBundle;
			} else
				TaskFail ();

			m_Req = null;
		} else {
			m_Progress = m_Req.progress;
		}

		if (OnProcess != null)
			OnProcess (this);

	}

	public override AssetBundle Bundle
	{
		get {
			return m_Bundle;
		}
	}

	public float Progress
	{
		get {
			return m_Progress;
		}
	}

	public Action<BundleCreateAsyncTask> OnProcess {
		get;
		set;
	}

    public string FileName
    {
        get
        {
            return m_FileName;
        }
    }

	private static BundleCreateAsyncTask GetNewTask()
	{
		if (m_UsePool)
		{
			InitPool();
			BundleCreateAsyncTask ret = m_Pool.GetObject();
			if (ret != null)
				ret.m_IsInPool = false;
			return ret;
		}

		return new BundleCreateAsyncTask();
	}

	private static void InPool(BundleCreateAsyncTask task)
	{
		if (!m_UsePool || task == null || task.m_IsInPool)
			return;
		InitPool();
		m_Pool.Store(task);	
		task.m_IsInPool = true;
	}

	private void ItemPoolReset()
	{
		if (m_Req != null)
		{
			m_Req = null;
		}

		OnResult = null;
		OnProcess = null;
		m_Bundle = null;
		m_Progress = 0;
		m_FileName = string.Empty;
		m_Priority = 0;
		mResult = 0;
		UserData = null;
		_Owner = null;
	}

	private static void PoolReset(BundleCreateAsyncTask task)
	{
		if (task == null)
			return;
		task.ItemPoolReset();
	}

	private static void InitPool()
	{
		if (m_PoolInited)
			return;
		m_PoolInited = true;
		m_Pool.Init(0, null, PoolReset);
	}

	private string m_FileName = string.Empty;
	private int m_Priority = 0;
//#if _USE_WX
//	private WXAssetBundleRequest m_Req = null;
//#else
    private AssetBundleCreateRequest m_Req = null;
//#endif
	private float m_Progress = 0;
	private AssetBundle m_Bundle = null;

	private bool m_IsInPool = false;

	private static bool m_UsePool = true;
	private static bool m_PoolInited = false;
	private static Utils.ObjectPool<BundleCreateAsyncTask> m_Pool = new Utils.ObjectPool<BundleCreateAsyncTask>();
}

#endif

	// WWW 文件读取任务
	public class WWWFileLoadTask: ITask
{
	// 注意：必须是WWW支持的文件名 PC上需要加 file:///
	public WWWFileLoadTask(string wwwFileName, ThreadPriority priority = ThreadPriority.Normal)
	{
		if (string.IsNullOrEmpty(wwwFileName)) {
			TaskFail();
			return;
		}

		mWWWFileName = wwwFileName;
		mPriority = priority;
	}

	public WWWFileLoadTask()
	{}

    // 是否使用LoadFromCacheOrDownload
    public bool IsUsedCached {
        get;
        set;
    }

	public static WWWFileLoadTask Create(string wwwFileName, ThreadPriority priority = ThreadPriority.Normal)
	{
		if (string.IsNullOrEmpty(wwwFileName))
			return null;
		WWWFileLoadTask ret = GetNewTask();
		ret.mWWWFileName = wwwFileName;
		ret.mPriority = priority;
		return ret;
	}

	
	// 传入为普通文件名(推荐使用这个函数)
	public static WWWFileLoadTask LoadFileName(string fileName, ThreadPriority priority = ThreadPriority.Normal)
	{
		string wwwFileName = ConvertToWWWFileName(fileName);
		WWWFileLoadTask ret = Create(wwwFileName, priority);
		return ret;
	}
	
	// 读取StreamingAssets目录下的文件，只需要相对于StreamingAssets的路径即可(推荐使用这个函数)
	public static WWWFileLoadTask LoadFileAtStreamingAssetsPath(string fileName, bool usePlatform,  ThreadPriority priority = ThreadPriority.Normal)
	{
		fileName = GetStreamingAssetsPath(usePlatform) + "/" + fileName;
		WWWFileLoadTask ret = LoadFileName(fileName, priority);
		return ret;
	}

    private class StreamingAssetsPathComparser : StructComparser<StreamingAssetsPathKey> { }

    // 优化GetStreamingAssetsPath
    // StreamingAssets的Path的Key
    private struct StreamingAssetsPathKey: IEquatable<StreamingAssetsPathKey> {
        public bool usePlatform {
            get;
            set;
        }

        public bool isUseABCreateFromFile {
            get;
            set;
        }

        public bool Equals(StreamingAssetsPathKey other) {
            return this == other;
        }

        public override bool Equals(object obj) {
            if (obj == null)
                return false;

            if (GetType() != obj.GetType())
                return false;

            if (obj is StreamingAssetsPathKey) {
                StreamingAssetsPathKey other = (StreamingAssetsPathKey)obj;
                return Equals(other);
            } else
                return false;

        }

        public override int GetHashCode() {
            int ret = FilePathMgr.InitHashValue();
            FilePathMgr.HashCode(ref ret, usePlatform);
            FilePathMgr.HashCode(ref ret, isUseABCreateFromFile);
            return ret;
        }

        public static bool operator ==(StreamingAssetsPathKey a, StreamingAssetsPathKey b) {
            return (a.usePlatform == b.usePlatform) && (a.isUseABCreateFromFile == b.isUseABCreateFromFile);
        }

        public static bool operator !=(StreamingAssetsPathKey a, StreamingAssetsPathKey b) {
            return !(a == b);
        }
    }

    // 优化StreamingAssetsPath
    private static Dictionary<StreamingAssetsPathKey, string> m_StreamingAssetsPathMap = 
                 new Dictionary<StreamingAssetsPathKey, string>(StreamingAssetsPathComparser.Default);

	public static string GetStreamingAssetsPath(bool usePlatform, bool isUseABCreateFromFile = false)
	{
        // 优化StreamingAssetsPath
        StreamingAssetsPathKey key = new StreamingAssetsPathKey();
        key.usePlatform = usePlatform;
        key.isUseABCreateFromFile = isUseABCreateFromFile;

        string ret;
        if (m_StreamingAssetsPathMap.TryGetValue(key, out ret))
            return ret;

        ret = string.Empty;
		switch (Application.platform)
		{
			case RuntimePlatform.OSXServer:
			case RuntimePlatform.OSXPlayer:
			{
				ret = Application.streamingAssetsPath;
				if (usePlatform)
					ret += "/Mac";
				break;
			}

			case RuntimePlatform.LinuxServer:
            {
					ret = Application.streamingAssetsPath;
					if (usePlatform)
						ret += "/Linux";
					break;
            }
#if UNITY_WEIXINMINIGAME
			case RuntimePlatform.WeixinMiniGamePlayer: {
					ret = Application.streamingAssetsPath;
					if (usePlatform)
						ret += "/MiniGame";
					break;
				}
#endif

			case RuntimePlatform.OSXEditor:
			{
				ret = "Assets/StreamingAssets";
				if (usePlatform)
                {
#if UNITY_EDITOR
                    var target = UnityEditor.EditorUserBuildSettings.activeBuildTarget;
                        if (target == UnityEditor.BuildTarget.StandaloneOSXIntel ||
                            target == UnityEditor.BuildTarget.StandaloneOSXIntel64 ||
#if UNITY_2018 || UNITY_2019 || UNITY_2017 || UNITY_2017_1_OR_NEWER
							target == UnityEditor.BuildTarget.StandaloneOSX)
#else
							target == UnityEditor.BuildTarget.StandaloneOSXUniversal)
#endif
                            ret += "/Mac";
                        else if (target == UnityEditor.BuildTarget.Android)
                            ret += "/Android";
                        else if (target == UnityEditor.BuildTarget.iOS)
                            ret += "/IOS";
                        else if (target == UnityEditor.BuildTarget.StandaloneWindows || target == UnityEditor.BuildTarget.StandaloneWindows64)
                            ret += "/Windows";
#if UNITY_WEIXINMINIGAME
						else if (target == UnityEditor.BuildTarget.WeixinMiniGame)
							ret += "/MiniGame";
#endif
#else
					ret += "/Mac";
#endif
					}
				break;
			}

			case RuntimePlatform.WindowsServer:
			case RuntimePlatform.WindowsPlayer:
			{
				ret = Application.streamingAssetsPath;
				if (usePlatform)
					ret += "/Windows";
				break;
			}

			case RuntimePlatform.WindowsEditor:
			{
				ret = "Assets/StreamingAssets";
				if (usePlatform)
                {
#if UNITY_EDITOR
                    var target = UnityEditor.EditorUserBuildSettings.activeBuildTarget;
						if (target == UnityEditor.BuildTarget.StandaloneWindows || target == UnityEditor.BuildTarget.StandaloneWindows64)
							ret += "/Windows";
						else if (target == UnityEditor.BuildTarget.Android)
							ret += "/Android";
#if UNITY_WEIXINMINIGAME
						else if (target == UnityEditor.BuildTarget.WeixinMiniGame)
							ret += "/MiniGame";
#endif
#else
					ret += "/Windows";
#endif
					}
				break;
			}
			case RuntimePlatform.Android:
			{
#if !TUANJIE_1_0_OR_NEWER
					if (isUseABCreateFromFile)
					ret = Application.dataPath + "!assets";
				else
#else
					ret = Application.streamingAssetsPath;
#endif
					if (usePlatform)
					ret += "/Android";
				break;
			}
			case RuntimePlatform.IPhonePlayer:
			{
				ret = Application.streamingAssetsPath;
				if (usePlatform)
					ret += "/IOS";
				break;
			}
			default:
				ret = Application.streamingAssetsPath;
				break;
		}

        m_StreamingAssetsPathMap.Add(key, ret);


        return ret;
	}
	
	// 普通文件名转WWW文件名
	public static string ConvertToWWWFileName(string fileName)
	{
		if (string.IsNullOrEmpty(fileName))
			return string.Empty;
		string ret = System.IO.Path.GetFullPath(fileName);
		if (string.IsNullOrEmpty(ret))
			return string.Empty;
		switch (Application.platform)
		{
			case RuntimePlatform.OSXEditor:
				ret = "file:///" + ret;
				break;
			case RuntimePlatform.WindowsEditor:
				ret = "file:///" + ret; 
				break;
			case RuntimePlatform.OSXPlayer:
				ret = "file:///" + ret; 
				break;
			case RuntimePlatform.WindowsPlayer:
				ret = "file:///" + ret; 
				break;
			case RuntimePlatform.IPhonePlayer:
                ret = "file:///" + ret;
                break;
			case RuntimePlatform.Android:
				ret = ret.Replace("/jar:file:/", "jar:file:///");
				break;
		}
		return ret;
	}

	public override void Release()
	{
		base.Release ();
		ItemPoolReset();
		InPool(this);
	}

	// 手动Load, 可以调也可以不调
	public AssetBundle StartLoad() {
		if (mLoader == null) {
			if (IsUsedCached)
				mLoader = WWW.LoadFromCacheOrDownload(mWWWFileName, 0);
			else
				mLoader = new WWW(mWWWFileName);

			mLoader.threadPriority = mPriority;
			if (mLoader.isDone)
				return mLoader.assetBundle;
		} else if (mLoader.isDone)
			return mLoader.assetBundle;
		return null;
	}

	// 快速打断异步加载
	public override void QuickLoaded() {
		if (mLoader != null) {
			mBundle = mLoader.assetBundle;
		}
	}

	public override void Process()
	{
        StartLoad();

        if (mLoader == null) {
			TaskFail();
			return;
		}

		if (mLoader.isDone) {
			if (mLoader.assetBundle != null) {
				mProgress = 1.0f;
				TaskOk ();
				mBundle = mLoader.assetBundle;
			} else
			if ((mLoader.bytes != null) && (mLoader.bytes.Length > 0)) {
				mProgress = 1.0f;
				TaskOk ();
				mByteData = mLoader.bytes;
                mText = mLoader.text;
            } else
				TaskFail ();

			mLoader.Dispose ();
			mLoader = null;
		} else {
			mProgress = mLoader.progress;
		}

		if (OnProcess != null)
			OnProcess (this);
	}

	public byte[] ByteData
	{
		get 
		{
			return mByteData;
		}
	}

    public string Text {
        get {
            return mText;
        }
    }

	public AssetBundle Bundle
	{
		get {
			return mBundle;
		}
	}

	public float Progress
	{
		get {
			return mProgress;
		}
	}

	public Action<WWWFileLoadTask> OnProcess {
		get;
		set;
	}

	public static int GetPoolCount()
	{
		return m_Pool.Count;
	}

	private void ItemPoolReset()
	{
		if (mLoader != null)
		{
			mLoader.Dispose();
			mLoader = null;
		}
		OnResult = null;
		OnProcess = null;
		mProgress = 0;
		mWWWFileName = string.Empty;
		mPriority = ThreadPriority.Normal;
		mResult = 0;
		UserData = null;
		_Owner = null;
		mByteData = null;
        mText = string.Empty;
        mBundle = null;
        IsUsedCached = false;
	}

	private static WWWFileLoadTask GetNewTask()
	{
		if (m_UsePool)
		{
			InitPool();
			WWWFileLoadTask ret = m_Pool.GetObject();
			if (ret != null)
				ret.m_IsInPool = false;
			return ret;
		}

		return new WWWFileLoadTask();
	}

	private static void InPool(WWWFileLoadTask task)
	{
		if (!m_UsePool || task == null || task.m_IsInPool)
			return;
		InitPool();
		m_Pool.Store(task);	
		task.m_IsInPool = true;
	}

	private static void PoolReset(WWWFileLoadTask task)
	{
		if (task == null)
			return;
		task.ItemPoolReset();
	}

	private static void InitPool()
	{
		if (m_PoolInited)
			return;
		m_PoolInited = true;
		m_Pool.Init(0, null, PoolReset);
	}

	private WWW mLoader = null;
	private byte[] mByteData = null;
    private string mText = string.Empty;
	private AssetBundle mBundle = null;
	private string mWWWFileName = string.Empty;
	private ThreadPriority mPriority = ThreadPriority.Normal;
	private float mProgress = 0;


	private bool m_IsInPool = false;

	private static bool m_UsePool = true;
	private static bool m_PoolInited = false;
	private static Utils.ObjectPool<WWWFileLoadTask> m_Pool = new Utils.ObjectPool<WWWFileLoadTask>();
}

// 加载场景任务
public class LevelLoadTask: ITask
{
	// 场景名 是否增加方式 是否是异步模式  onProgress(float progress, int result)
	// result: 0 表示进行中 1 表示加载完成 -1表示加载失败
	public LevelLoadTask(string sceneName, bool isAdd, bool isAsync, Action<float, int> onProcess)
	{
		if (string.IsNullOrEmpty (sceneName)) {
			TaskFail();
			return;
		}

		mSceneName = sceneName;
		mIsAdd = isAdd;
		mIsAsync = isAsync;
		mOnProgress = onProcess;
	}

	public override void Process()
	{
		// 同步
		if (!mIsAsync) {
            bool isResult = Application.CanStreamedLevelBeLoaded(mSceneName);

            if (isResult)
            {
                if (mIsAdd)
                    Application.LoadLevelAdditive(mSceneName);
                else
                    Application.LoadLevel(mSceneName);
            }

			
			if (isResult)
			{
				TaskOk();
			}
			else
				TaskFail();

			if (mOnProgress != null)
			{
				if (isResult)
					mOnProgress(1.0f, 1);
				else
					mOnProgress(0, -1);
			}
			
			return;
		}

		if (mOpr == null) {
			// 异步
			if (mIsAdd)
				mOpr = Application.LoadLevelAdditiveAsync (mSceneName);
			else
				mOpr = Application.LoadLevelAsync (mSceneName);
		}

		if (mOpr == null) {
			TaskFail();
			if (mOnProgress != null)
				mOnProgress(0, -1);
			return;
		}

		if (mOpr.isDone) {
			TaskOk();
			if (mOnProgress != null)
				mOnProgress(1.0f, 1);
		} else {
			if (mOnProgress != null)
				mOnProgress(mOpr.progress, 0);
		}

	}

	public string SceneName
	{
		get {
			return mSceneName;
		}
	}

	private string mSceneName = string.Empty;
	private bool mIsAdd = false;
	private bool mIsAsync = false;
	private AsyncOperation mOpr = null;
	private Action<float, int> mOnProgress = null;
}

// 任务列表(为了保证顺序执行)
public class TaskList
{
	public bool Contains(ITask task)
	{
		if (task == null)
			return false;
		int hashCode = task.GetHashCode();
		return mTaskIDs.Contains(hashCode);
	}

	// 保证不要加重复的
	public void AddTask(ITask task, bool isOwner)
	{
		if (task == null)
			return;
		int hashCode = task.GetHashCode();
		if (!mTaskIDs.Contains(hashCode))
		{
			mTaskIDs.Add(hashCode);
			mTaskList.AddLast (task);
			if (isOwner)
				task._Owner = this;
		}
	}

	// 保证不要加重复的
	public void AddTask(LinkedListNode<ITask> node, bool isOwner)
	{
		if ((node == null) || (node.Value == null))
			return;
		int hashCode = node.Value.GetHashCode();
		if (!mTaskIDs.Contains(hashCode))
		{
			mTaskIDs.Add(hashCode);
			mTaskList.AddLast (node);
			if (isOwner)
				node.Value._Owner = this;
		}
	}

	public void ProcessDoneContinue(Func<ITask, bool> onCheckTaskVaild = null)
	{
		LinkedListNode<ITask> node = mTaskList.First;
		while (node != null && node.Value != null)
		{
			if (onCheckTaskVaild != null)
			{
				if (!onCheckTaskVaild(node.Value))
				{
					RemoveTask(node);
					node = mTaskList.First;
					continue;
				}
			}

			if (node.Value.IsDone)
			{
				TaskEnd(node.Value);
				RemoveTask(node);
				node = mTaskList.First;
				continue;
			}

			TaskProcess(node.Value);

			if (node.Value.IsDone)
			{
				TaskEnd(node.Value);
				RemoveTask(node);
				node = mTaskList.First;
				continue;
			}

			break;
		}
	}

	public void Process(Func<ITask, bool> onCheckTaskVaild = null)
	{
		LinkedListNode<ITask> node = mTaskList.First;
		if ((node != null) && (node.Value != null)) {

			if (onCheckTaskVaild != null)
			{
				if (!onCheckTaskVaild(node.Value))
				{
					RemoveTask(node);
					return;
				}
			}

			if (node.Value.IsDone)
			{
				TaskEnd(node.Value);
				RemoveTask(node);
				return;
			}

			TaskProcess(node.Value);

			if (node.Value.IsDone)
			{
				TaskEnd(node.Value);
				RemoveTask(node);
			}
		}
	}

	public bool IsEmpty
	{
		get{
			return mTaskList.Count <= 0;
		}
	}

	public System.Object UserData
	{
		get;
		set;
	}
		
	public void Clear()
	{
		var node = mTaskList.First;
		while (node != null) {
			var next = node.Next;
			if (node.Value != null && node.Value._Owner == this)
				node.Value.Release();
			node = next;
		}

		mTaskList.Clear ();
		mTaskIDs.Clear();
	}

	private void RemoveTask(LinkedListNode<ITask> node)
	{
		if (node == null || node.Value == null)
			return;
		int hashCode = node.Value.GetHashCode();
		if (mTaskIDs.Contains(hashCode))
		{
			mTaskIDs.Remove(hashCode);
			mTaskList.Remove(node);
		}
	}

	public void RemoveTask(ITask task)
	{
		if (task == null)
			return;
		int hashCode = task.GetHashCode();
		if (mTaskIDs.Contains(hashCode))
		{
			mTaskIDs.Remove(hashCode);
			mTaskList.Remove(task);
		}
	}

	public int Count
	{
		get
		{
			return mTaskList.Count;
		}
	}

	private void TaskEnd(ITask task)
	{
		if ((task == null) || (!task.IsDone))
			return;
		if ((task._Owner == this) && (task.OnResult != null))
		{
			task.OnResult (task);
			task.Release();
		}
	}

	private void TaskProcess(ITask task)
	{
		if (task == null)
			return;

		if (task._Owner == this)
			task.Process ();
	}
	
	// 任务必须是顺序执行
	private LinkedList<ITask> mTaskList = new LinkedList<ITask>();
	private HashSet<int> mTaskIDs = new HashSet<int>();
}