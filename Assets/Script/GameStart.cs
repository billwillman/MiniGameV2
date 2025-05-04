#if UNITY_WEIXINMINIGAME && !UNITY_EDITOR
    #define _USE_WX
#endif

using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;
using XLua;
#if UNITY_WEIXINMINIGAME
using WeChatWASM;
using UnityEditor;
#endif

namespace SOC.GamePlay
{

    [LuaCallCSharp]
    [RequireComponent(typeof(MiniGame_ResProxyMgr))]
    public class GameStart : MonoBehaviour
    {

        public static GameStart Instance = null;
        private LuaFunction m_LuaUpdateFunc = null;

        [BlackList]
        public bool m_DS_OutputOldLogHandle = true;

        [BlackList]
        public bool m_ClientOutputOldLogHandle = true;

        [BlackList]
        public bool m_LogWriteAsync = true;

        private MiniGame_ResProxyMgr m_ResProxyMgr = null;

        protected MiniGame_ResProxyMgr ResProxyMgr {
            get {
                if (m_ResProxyMgr == null)
                    m_ResProxyMgr = GetComponent<MiniGame_ResProxyMgr>();
                return m_ResProxyMgr;
            }
        }

        // Start is called before the first frame update
        void Awake() {
            DontDestroyOnLoad(this.gameObject);
            m_LuaLoaderCallBack = new LuaEnv.CustomLoader(OnLuaFileLoad);
            Instance = this;
        }

        void OnRequestStartFinish(bool isOk) {
            if (isOk) {
                if (AssetLoader.UseCDNMapper)
                {
                    ResourceMgr.Instance.WebLoadConfigs(OnResConfigResult, this);
                }
                else
                    ResourceMgr.Instance.LoadConfigs(OnResConfigResult, null, true);
            } else {
                Debug.LogError("[OnRequestStartFinish] Failed.");
            }
        }

        private void Start() {
            ServerAttachLogFile();
            InitLuaSearchFormatPath();
            var resMgr = this.ResProxyMgr;
            resMgr.RequestStart(OnRequestStartFinish, null);
        }

        void InitLuaSearchFormatPath() {
            string luaSearchPath = string.Empty;
#if !UNITY_WEIXINMINIGAME // 微信小游戏不要从可写目录读取
            if (IsDS) {
                luaSearchPath = Application.dataPath + "/Lua/{0}.lua.bytes";
            } else {
                if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.LinuxPlayer)
                    luaSearchPath = Application.dataPath + "/Lua/{0}.lua.bytes";
                else
                    luaSearchPath = Application.persistentDataPath + "/Lua/{0}.lua.bytes";
            }
#endif
#if !UNITY_EDITOR
            if (!string.IsNullOrEmpty(luaSearchPath)) {
                Debug.LogFormat("AddDynicLuaSearchPath: {0}", luaSearchPath);
                _cLuaRootPathFormats.Add(new LuaPathFormatData(luaSearchPath, true));
            }
#endif
            _cLuaRootPathFormats.Add(new LuaPathFormatData("Resources/@Lua/{0}.lua.bytes", false));
            _cLuaRootPathFormats.Add(new LuaPathFormatData("Resources/@Lua/_BehaviourTree/{0}.lua.bytes", false));
        }

        void OnResConfigResult(bool isOk) {
            Debug.Log("[OnResConfigResult] isOk: " + isOk.ToString());
            if (isOk) {
                OnInit();
            } else {
#if UNITY_EDITOR
                // 编辑器模式一定会成功
                OnInit();
#endif
            }
        }

        void OnInit() {
#if _USE_WX
            WX.InitSDK((int code) =>
            {
                Debug.Log("WX InitSDK code: " + code);
                InitLuaEnv();
            });
#else
            InitLuaEnv();
#endif
        }

        void ServerAttachLogFile() {
#if !UNITY_WEIXINMINIGAME
            if (IsDS) {
#if !UNITY_EDITOR
                // DS才能才存储
                m_LogFileWriter = new LogFileWriter("dsRuntimeLog", m_DS_OutputOldLogHandle);
                m_LogFileWriter.IsLogWriteAsync = m_LogWriteAsync;
#endif
            } else {
#if !UNITY_EDITOR
                m_LogFileWriter = new LogFileWriter("ClientRuntimeLog", m_ClientOutputOldLogHandle);
                m_LogFileWriter.IsLogWriteAsync = m_LogWriteAsync;
#endif
            }
#endif
        }

        // 初始化Lua环境
        void InitLuaEnv() {
            // 1.初始化Lua环境
            m_LuaEnv = new LuaEnv();
            m_LuaEnv.AddLoader(m_LuaLoaderCallBack);
            // 2.调用Lua 入口函数
            Lua_DoMain();
        }

        public static bool IsDS {
            get {
#if UNITY_EDITOR
                var subTarget = UnityEditor.EditorUserBuildSettings.standaloneBuildSubtarget;
                if (UnityEditor.EditorUserBuildSettings.activeBuildTarget != UnityEditor.BuildTarget.StandaloneLinux64 && UnityEditor.EditorUserBuildSettings.activeBuildTarget != UnityEditor.BuildTarget.StandaloneWindows &&
                        UnityEditor.EditorUserBuildSettings.activeBuildTarget != UnityEditor.BuildTarget.StandaloneWindows64 && UnityEditor.EditorUserBuildSettings.activeBuildTarget != UnityEditor.BuildTarget.StandaloneOSX)
                    subTarget = UnityEditor.StandaloneBuildSubtarget.Player;
                bool isServer = (subTarget == UnityEditor.StandaloneBuildSubtarget.Server);
                return isServer;
#else
#if UNITY_SERVER
            return true;
#else
            return false; 
#endif
#endif
            }
        }

        [XLua.Hotfix]
        // 初始化NetCode的Lua全局变量
        void InitNetCodeLuaGlobalVars(LuaTable _MOE) {
#if UNITY_EDITOR
            _MOE.Set<string, bool>("IsEditor", true);
#else
            _MOE.Set<string, bool>("IsEditor", false);
#endif
            _MOE.Set<string, bool>("IsDS", IsDS); // 是否是DS 
            _MOE.Set<string, int>("Platform", (int)Application.platform); // 平台
        }

        void OnGameStartFinish() {
            Debug.Log("[GameStart] OnGameStartFinish");
            // ResourceMgr.Instance.LoadSceneAsync("TestBuldInScene", false, null);
        }

        void Lua_DoMain() {
            if (m_LuaEnv != null) {
                // 优先加载Preload.lua
                ResourceMgr.Instance.LoadTextAsync("Resources/@Lua/Preload.lua.bytes", (float process, bool isDone, TextAsset text) =>
                {
                    if (isDone)
                    {
                        byte[] lua = text.bytes;
                        System.Object[] result = m_LuaEnv.DoString(lua);
                        LuaTable _MOE = result[0] as LuaTable;
                        try
                        {
                            InitNetCodeLuaGlobalVars(_MOE);
                        }
                        finally
                        {
                            _MOE.Dispose();
                        }
                        //--
                        lua = ResourceMgr.Instance.LoadBytes("Resources/@Lua/Main.lua.bytes");
                        if (lua != null)
                        {
                            m_LuaEnv.DoString(lua);
                            LuaFunction MainFunc = m_LuaEnv.Global.Get<LuaFunction>("Main");
                            if (MainFunc != null)
                            {
                                try
                                {
                                    MainFunc.Call();
                                }
                                finally
                                {
                                    MainFunc.Dispose();
                                }
                            }
                            m_LuaUpdateFunc = m_LuaEnv.Global.Get<LuaFunction>("Update");

                            // 游戏正式开始
                            OnGameStartFinish();
                        }
                    }
                }, ResourceCacheType.rctRefAdd);
            }
        }

        [BlackList]
        public static LuaEnv EnvLua {
            get {
                if (Instance != null) {
                    return Instance.m_LuaEnv;
                }
                return null;
            }
        }

        private void Update() {
            TimerMgr.Instance.ScaleTick(Time.deltaTime);
            TimerMgr.Instance.UnScaleTick(Time.unscaledDeltaTime);
            if (m_LuaUpdateFunc != null) {
                m_LuaUpdateFunc.Call(Time.deltaTime);
            }
        }

        private void OnApplicationQuit() {
            ResourceMgr.Instance.OnApplicationQuit();
        }

        private void OnDestroy() {
            if (m_LuaEnv != null) {
                var QuitGame = m_LuaEnv.Global.Get<LuaFunction>("QuitGame");
                if (QuitGame != null) {
                    try {
                        QuitGame.Call();
                    } finally {
                        QuitGame.Dispose();
                    }
                }
                if (m_LuaUpdateFunc != null) {
                    m_LuaUpdateFunc.Dispose();
                    m_LuaUpdateFunc = null;
                }
                m_LuaEnv.Dispose();
                m_LuaEnv = null;
            }

            // 日志写入文件
            if (m_LogFileWriter != null) {
                m_LogFileWriter.Dispose();
                m_LogFileWriter = null;
            }

        }

        struct LuaPathFormatData
        {
            public string formatPath;
            public bool isFileMode;

            public LuaPathFormatData(string formatPath, bool isFileMode) {
                this.formatPath = formatPath;
                this.isFileMode = isFileMode;
            }
        }

        private static List<LuaPathFormatData> _cLuaRootPathFormats = new List<LuaPathFormatData>();

        private byte[] OnLuaFileLoad(ref string filepath) {
            filepath = filepath.Replace(".", "/");
            foreach (var pathData in _cLuaRootPathFormats) {
                string luaPath = string.Format(pathData.formatPath, filepath);
                if (pathData.isFileMode) {
                    if (File.Exists(luaPath)) {
                        byte[] ret = File.ReadAllBytes(luaPath);
                        if (ret != null)
                            return ret;
                    }
                } else {
                    byte[] ret = ResourceMgr.Instance.LoadBytes(luaPath);
                    if (ret != null)
                        return ret;
                }
            }
            return null;
        }

        private LuaEnv m_LuaEnv = null;
        private LuaEnv.CustomLoader m_LuaLoaderCallBack = null;
        private LogFileWriter m_LogFileWriter = null;
    }
}
