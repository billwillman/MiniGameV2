using System.IO;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    public sealed class ClusterMeshSettingsWindow : EditorWindow
    {
        ClusterMeshSettings _settings;
        Vector2 _scroll;

        [MenuItem("Tools/ClusterMesh/通用设置", priority = 10)]
        public static void Open()
        {
            GetWindow<ClusterMeshSettingsWindow>("ClusterMesh 通用设置");
        }

        public static ClusterMeshSettings GetOrCreate()
        {
            ClusterMeshSettings settings = AssetDatabase.LoadAssetAtPath<ClusterMeshSettings>(
                ClusterMeshSettings.AssetPath);
            if (settings != null)
                return settings;

            string folder = Path.GetDirectoryName(ClusterMeshSettings.AssetPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }

            settings = CreateInstance<ClusterMeshSettings>();
            AssetDatabase.CreateAsset(settings, ClusterMeshSettings.AssetPath);
            AssetDatabase.SaveAssets();
            ClusterMeshSettings.ClearCacheForTests();
            return settings;
        }

        void OnEnable()
        {
            _settings = GetOrCreate();
        }

        void OnGUI()
        {
            if (_settings == null)
                _settings = GetOrCreate();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "项目级设置。管线时机对所有 ClusterMesh / Skinned ClusterMesh 生效，不要写在单个 Renderer 上。\n" +
                "物体是否写出 Motion Vector 仍由各 Renderer 的 enableMotionVectors 决定；这里只决定写入时机。\n" +
                "本工程 Radiant GI 在 AfterRenderingSkybox + 2 读 _MotionVectorTexture，URP TAA 用同一张图。" +
                "必须先于它写入，Temporal 才认 ClusterMesh 在动。推荐默认 After Skybox + 1。",
                MessageType.Info);

            EditorGUILayout.LabelField("Motion Vector Pass", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "ClusterMesh 物体速度写入 URP Motion Vector 图的时机。勾选物体 MV 后，选对挂点 Radiant GI / TAA 才有效。",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            DrawMotionVectorSlot(
                ClusterMeshMotionVectorSlot.AfterSkyboxPlus1, _settings);
            DrawMotionVectorSlot(
                ClusterMeshMotionVectorSlot.AfterOpaques, _settings);
            DrawMotionVectorSlot(
                ClusterMeshMotionVectorSlot.AfterSkybox, _settings);
            DrawMotionVectorSlot(
                ClusterMeshMotionVectorSlot.BeforePostProcessingMinus1, _settings);

            EditorGUILayout.Space(12);
            DrawStreamingSettings(_settings);
            EditorGUILayout.EndScrollView();
        }

        static void DrawStreamingSettings(ClusterMeshSettings settings)
        {
            EditorGUILayout.LabelField("Page Streaming", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "设计目的：Bake 时勾选 Page Streaming 的 Static Mesh / Skinned Mesh 会把几何页写入独立 .cmstream 文件，运行时使用异步 FileStream 按需读取。根 LOD 常驻，细节页缺失时回退到已驻留父 LOD，避免模型空洞。\n" +
                "文件位置不绑定 StreamingAssets：ClusterMeshStreaming.RootPath / FilePathResolver 可映射到 persistentDataPath、热更目录或任意真实文件路径。\n" +
                "未勾选 Streaming 的旧资产不会创建流式 Runtime、请求 Buffer 或页池，下列设置对非 Streaming 路径完全无效。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            bool shared = EditorGUILayout.Toggle(
                new GUIContent("Global Shared GPU Pool",
                    "兼容开关。开启：相同顶点格式、权重格式和页规格的多个流式资产共享大型 Vertex/Index/Weight GPU Buffer。关闭：回退为每个流式资产独立 GPU Pool，便于排查特殊平台或驱动兼容问题。不会影响非 Streaming 资产。"),
                settings.EnableGlobalSharedGpuPool);
            int poolPages = EditorGUILayout.IntSlider(
                new GUIContent("Shared Pool Pages",
                    "每个兼容 Pool 分片的物理页数。页数越大，LOD 抖动和重复 I/O 越少，但常驻显存越高；当根页总量加一个完整细节节点的预留空间不足时会自动创建额外分片。"),
                settings.SharedGpuPoolPageCapacity, 32, 2048);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("I/O 与上传预算", EditorStyles.boldLabel);
            int reads = EditorGUILayout.IntSlider(
                new GUIContent("Concurrent Reads", "所有流式资产共用的异步 FileStream 在途读取上限，防止资产数量把并发放大。"),
                settings.MaxConcurrentPageReads, 1, 32);
            int uploads = EditorGUILayout.IntSlider(
                new GUIContent("Uploads / Update", "所有流式资产每次中央更新最多上传的页面数，限制主线程与 GPU 上传尖峰。"),
                settings.MaxPageUploadsPerUpdate, 1, 16);
            int readMb = EditorGUILayout.IntSlider(
                new GUIContent("Read MB / Update", "每次中央更新允许新启动的压缩文件读取字节预算。"),
                settings.MaxReadMegabytesPerUpdate, 1, 64);
            int uploadMb = EditorGUILayout.IntSlider(
                new GUIContent("Upload MB / Update", "每次中央更新允许上传到 GPU 的解压后字节预算。"),
                settings.MaxUploadMegabytesPerUpdate, 1, 64);
            int decodedMb = EditorGUILayout.IntSlider(
                new GUIContent("Decoded Queue MB", "读取中与已解压等待上传页面的全局内存上限，防止上传速度落后时堆积。"),
                settings.MaxDecodedMegabytes, 4, 256);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("调度与稳定性", EditorStyles.boldLabel);
            bool prefetch = EditorGUILayout.Toggle(
                new GUIContent("Prefetch Next Detail", "按相机距离和 LOD 请求优先级，低优先级预取目标节点的下一层细节，降低靠近物体时的 LOD 跳变等待；代价是少量额外 I/O。"),
                settings.EnableStreamingPrefetch);
            int grace = EditorGUILayout.IntSlider(
                new GUIContent("Resident Grace Updates", "页面最近使用后至少保留的中央更新次数。用于抑制相机在 LOD 临界距离附近时反复加载/淘汰。数值越高越稳定，但可回收速度越慢。"),
                settings.StreamingResidentGraceUpdates, 0, 120);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(settings, "ClusterMesh Streaming Settings");
                settings.EnableGlobalSharedGpuPool = shared;
                settings.SharedGpuPoolPageCapacity = poolPages;
                settings.MaxConcurrentPageReads = reads;
                settings.MaxPageUploadsPerUpdate = uploads;
                settings.MaxReadMegabytesPerUpdate = readMb;
                settings.MaxUploadMegabytesPerUpdate = uploadMb;
                settings.MaxDecodedMegabytes = decodedMb;
                settings.EnableStreamingPrefetch = prefetch;
                settings.StreamingResidentGraceUpdates = grace;
                EditorUtility.SetDirty(settings);
                if (!Application.isPlaying)
                    ClusterMeshStreaming.DisposeAll();
            }

            EditorGUILayout.HelpBox(
                "请求链路：GPU 使用双缓冲位集回读需求；回读未完成时继续显示父 LOD，不做每帧全节点 CPU 扫描。请求按相机距离与等待时间排序。\n" +
                "内存链路：Deflate 直接解压到 ArrayPool 缓冲，上传复用 staging 数组；CRC、页长度和重叠区间均在上传前验证。\n" +
                "多页节点：同一节点的页面加载期间受保护；共享池除常驻根页外始终预留至少一个完整细节节点空间，只有全部页面到齐才标记 Resident。",
                MessageType.None);
        }

        static void DrawMotionVectorSlot(ClusterMeshMotionVectorSlot slot, ClusterMeshSettings settings)
        {
            bool selected = settings.MotionVectorSlot == slot;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();
            bool next = EditorGUILayout.ToggleLeft(
                ClusterMeshUrpBridge.MotionVectorSlotLabel(slot), selected);
            if (EditorGUI.EndChangeCheck() && next && !selected)
            {
                Undo.RecordObject(settings, "ClusterMesh Motion Vector Pass");
                settings.MotionVectorSlot = slot;
                EditorUtility.SetDirty(settings);
            }

            EditorGUILayout.LabelField(
                ClusterMeshUrpBridge.MotionVectorSlotDescription(slot),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }
    }
}
