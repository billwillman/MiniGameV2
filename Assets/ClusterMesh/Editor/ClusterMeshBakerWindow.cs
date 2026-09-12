using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    public sealed class ClusterMeshBakerWindow : EditorWindow
    {
        Mesh _mesh;
        GameObject _sourceObject;
        bool _bakeSkinnedAnimation;
        SkinnedMeshRenderer _skinnedRenderer;
        [SerializeField] List<AnimationClip> _animationClips = new List<AnimationClip> { null };
        [SerializeField] DefaultAsset _animationClipFolder;
        DefaultAsset _outputFolder;
        string _assetName = "ClusterMeshAsset";
        ClusterMeshBakeSettings _settings = new ClusterMeshBakeSettings();
        [SerializeField] ClusterSkinnedMeshBakeOptions _skinnedBakeOptions = new ClusterSkinnedMeshBakeOptions();
        string _error;
        string _info;
        Vector2 _scroll;
        ClusterSkinnedCompressionAdviceSet _compressionAdvice;
        int _compressionAdviceHash = int.MinValue;

        public static bool ShowsQemToggle(bool buildLodHierarchy)
        {
            return buildLodHierarchy;
        }

        public static bool ShowsQemToggle(bool buildLodHierarchy, bool bakeSkinnedAnimation)
        {
            return buildLodHierarchy && !bakeSkinnedAnimation;
        }

        [MenuItem("Tools/ClusterMesh/Baker")]
        public static void Open()
        {
            GetWindow<ClusterMeshBakerWindow>("ClusterMesh Baker");
        }

        public static void WriteAsset(ClusterMeshAsset asset, Mesh mesh, Material[] materials, ClusterMeshBakeSettings settings)
        {
            var result = ClusterMeshBaker.Bake(mesh, materials, settings);
            asset.CopyFrom(result, mesh, settings);
        }

        public static void WriteSkinnedAsset(
            ClusterSkinnedMeshAsset asset,
            ClusterMeshAsset geometry,
            SkinnedMeshRenderer renderer,
            AnimationClip clip,
            ClusterMeshBakeSettings settings)
        {
            if (clip == null)
                throw new InvalidOperationException("Skinned ClusterMesh requires an AnimationClip.");
            WriteSkinnedAsset(asset, geometry, renderer, new[] { clip }, settings);
        }

        public static void WriteSkinnedAsset(
            ClusterSkinnedMeshAsset asset,
            ClusterMeshAsset geometry,
            SkinnedMeshRenderer renderer,
            AnimationClip[] clips,
            ClusterMeshBakeSettings settings)
        {
            WriteSkinnedAsset(asset, geometry, renderer, clips, settings, new ClusterSkinnedMeshBakeOptions());
        }

        public static void WriteSkinnedAsset(
            ClusterSkinnedMeshAsset asset,
            ClusterMeshAsset geometry,
            SkinnedMeshRenderer renderer,
            AnimationClip[] clips,
            ClusterMeshBakeSettings settings,
            ClusterSkinnedMeshBakeOptions bakeOptions)
        {
            if (asset == null || geometry == null)
                throw new InvalidOperationException("Skinned ClusterMesh output assets are missing.");
            settings = settings ?? new ClusterMeshBakeSettings();
            settings.useQemSimplify = true;
            bakeOptions = bakeOptions ?? new ClusterSkinnedMeshBakeOptions();
            ClusterSkinnedMeshBakeResult result = ClusterSkinnedMeshBaker.Bake(renderer, clips, settings, bakeOptions);
            geometry.CopyFrom(result.geometry, renderer.sharedMesh, settings, bakeOptions.packTightRestVertices);
            asset.geometry = geometry;
            bool packWeights8 = bakeOptions.packSkinWeights8 &&
                ClusterSkinnedMeshBaker.CanPackSkinWeights8(result.skinWeights);
            asset.packedSkinWeights = ClusterSkinnedMeshBaker.PackSkinWeights(
                result.skinWeights,
                packWeights8
                    ? ClusterSkinnedMeshAsset.PackedSkinWeightStride8
                    : ClusterSkinnedMeshAsset.PackedSkinWeightStride);
            asset.skinBoneCount = result.bindPoses != null ? result.bindPoses.Length : 0;
            asset.bindPoses = bakeOptions.IncludesCpu ? result.bindPoses : Array.Empty<Matrix4x4>();
            asset.boneParentIndices = bakeOptions.IncludesCpu
                ? result.boneParentIndices : Array.Empty<int>();
            asset.bonePaths = bakeOptions.IncludesCpu && bakeOptions.retainAnimationCurves
                ? result.bonePaths : Array.Empty<string>();
            asset.clips = result.clips;
            asset.gpuPaletteTextures = result.gpuPaletteTextures;
            asset.cpuCurveHeaders = result.cpuCurveHeaders;
            asset.cpuCurveSegments = result.cpuCurveSegments;
            asset.boneEvaluationOrder = bakeOptions.IncludesCpu
                ? result.boneEvaluationOrder : Array.Empty<int>();
            if (bakeOptions.compressCullFrames)
            {
                asset.cullFrames = Array.Empty<ClusterSkinnedCullFrame>();
                asset.packedCullFrames = ClusterSkinnedMeshBaker.PackCullFrames(result.cullFrames);
            }
            else
            {
                asset.cullFrames = result.cullFrames;
                asset.packedCullFrames = null;
            }
            asset.animationDataMode = bakeOptions.animationDataMode;
            asset.retainedAnimationCurves = bakeOptions.IncludesCpu && bakeOptions.retainAnimationCurves;
            asset.bakedGpuFramesPerSecond = bakeOptions.IncludesGpu
                ? Mathf.Clamp(bakeOptions.gpuFramesPerSecond, 1f, 60f)
                : 0f;
            asset.bakedCpuCurveTolerance = bakeOptions.IncludesCpu
                ? Mathf.Clamp(bakeOptions.cpuCurveTolerance, 0.000001f, 0.01f)
                : 0f;
            asset.skinWeightStride = packWeights8
                ? ClusterSkinnedMeshAsset.PackedSkinWeightStride8
                : ClusterSkinnedMeshAsset.PackedSkinWeightStride;
            asset.gpuCompactPalette = bakeOptions.IncludesGpu && bakeOptions.gpuCompactPalette;
            asset.cullFramesCompressed = bakeOptions.compressCullFrames;
            asset.tightRestVertices = bakeOptions.packTightRestVertices;
            asset.skinningVersion = ClusterSkinnedMeshAsset.CurrentSkinningVersion;
            asset.animationSamplingVersion = ClusterSkinnedMeshAsset.CurrentAnimationSamplingVersion;
            asset.gpuAnimationVersion = bakeOptions.IncludesGpu
                ? ClusterSkinnedMeshAsset.CurrentGpuAnimationVersion : 0;
            asset.cpuBurstAnimationVersion = bakeOptions.IncludesCpu
                ? ClusterSkinnedMeshAsset.CurrentCpuBurstAnimationVersion : 0;
            asset.skinVertexCount = result.skinWeights != null ? result.skinWeights.Length : 0;
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("怎么用", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "普通模式把静态 Mesh 拆成 ClusterMeshAsset；勾选蒙皮动画后会生成独立的 ClusterSkinnedMeshAsset。\n\n" +
                "1. 普通模式拖 Mesh/MeshFilter；蒙皮模式拖 SkinnedMeshRenderer，并逐条添加 AnimationClip，或选动画目录点「从目录追加」。\n" +
                "2. 选输出目录。空着则写到 Assets/ClusterMesh/Samples。\n" +
                "3. 填资产名，点 Bake。成功后 Project 会选中生成的 .asset。\n" +
                "4. 普通资产使用 ClusterMeshRenderer；蒙皮资产使用 ClusterSkinnedMeshRenderer。\n\n" +
                "蒙皮模式使用曲线拟合动画、VTF 蒙皮和 Skinning-Aware QEM，不使用 LODGroup。默认每 cluster 最多 64 顶点 / 124 三角。\n" +
                "Stats 里 SetPass/Batches 会含阴影、Depth、多相机。合批看 Frame Debugger 的 DrawMeshInstancedIndirect。投射/接收阴影可在 ClusterMeshRenderer 上分开关。",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("烘焙模式", EditorStyles.boldLabel);
            int mode = GUILayout.Toolbar(
                _bakeSkinnedAnimation ? 1 : 0,
                new[] { "静态 Mesh", "蒙皮动画" });
            _bakeSkinnedAnimation = mode == 1;
            EditorGUILayout.HelpBox(
                _bakeSkinnedAnimation
                    ? "当前：蒙皮。拖 SkinnedMeshRenderer + AnimationClip 列表（可从目录追加），生成 ClusterSkinnedMeshAsset。"
                    : "当前：静态。拖 Mesh / MeshFilter，生成 ClusterMeshAsset。",
                MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("输入", EditorStyles.boldLabel);
            if (_bakeSkinnedAnimation)
            {
                _settings.useQemSimplify = true;
                _skinnedRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
                    new GUIContent("Skinned Renderer", "场景或预制体中的 SkinnedMeshRenderer。"),
                    _skinnedRenderer, typeof(SkinnedMeshRenderer), true);
                DrawAnimationClipList();
            }
            else
            {
                _mesh = (Mesh)EditorGUILayout.ObjectField(
                    new GUIContent("Mesh", "直接指定要拆的网格。和下面的物体二选一，物体优先。"),
                    _mesh, typeof(Mesh), false);
                _sourceObject = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("MeshFilter 物体", "从场景/预制体拖一个带 MeshFilter 的物体，自动取 Mesh 和材质。"),
                    _sourceObject, typeof(GameObject), true);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("输出", EditorStyles.boldLabel);
            _outputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                new GUIContent("输出目录", "必须是 Project 里的文件夹。空 = Assets/ClusterMesh/Samples。"),
                _outputFolder, typeof(DefaultAsset), false);
            _assetName = EditorGUILayout.TextField(
                new GUIContent("资产名", "生成 Xxx.asset。重名会自动加数字。"),
                _assetName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cluster 预算", EditorStyles.boldLabel);
            _settings.maxVerticesPerCluster = EditorGUILayout.IntField(
                new GUIContent("Max Vertices", "每个 cluster 最多多少顶点。默认 64。"),
                _settings.maxVerticesPerCluster);
            _settings.maxTrianglesPerCluster = EditorGUILayout.IntField(
                new GUIContent("Max Triangles", "每个 cluster 最多多少三角。默认 124。"),
                _settings.maxTrianglesPerCluster);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("LOD", EditorStyles.boldLabel);
            _settings.buildLodHierarchy = EditorGUILayout.Toggle(
                new GUIContent(
                    "Build LOD Hierarchy",
                    "勾选：离线建锁边多层 DAG（资产更大，远处可换粗块）。不勾：只存叶子，文件更小，没有 cluster LOD。"),
                _settings.buildLodHierarchy);
            EditorGUILayout.HelpBox(
                _settings.buildLodHierarchy
                    ? "会分组、锁边、减半再切开，尽量收到根。重 Bake 后 Viewer / Renderer 拉阈值才能看到换层。"
                    : "只切叶子。资产大约能小一半，运行时始终画细块。",
                MessageType.None);
            if (ShowsQemToggle(_settings.buildLodHierarchy, _bakeSkinnedAnimation))
            {
                _settings.useQemSimplify = EditorGUILayout.Toggle(
                    new GUIContent(
                        "QEM Simplify",
                        "勾选：组内用 QEM（位置+法线+UV）折叠。不勾：最短边，和以前一样。阈值 T 仍在 Renderer 上调。"),
                    _settings.useQemSimplify);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("存储与流式加载", EditorStyles.boldLabel);
            _settings.enableStreaming = EditorGUILayout.Toggle(
                new GUIContent(
                    "Page Streaming",
                    "默认关闭。开启后生成独立 .cmstream 文件，根 LOD 常驻，细节 Cluster Page 通过异步 FileStream 按需进入有限 GPU Pool。关闭时完全保持旧 .asset 内嵌格式。"),
                _settings.enableStreaming);
            if (_settings.enableStreaming)
            {
                _settings.buildLodHierarchy = true;
                _settings.streamingPagePoolCapacity = EditorGUILayout.IntSlider(
                    new GUIContent(
                        "GPU Page Pool",
                        "每份流式资产最多使用的物理页槽数。根页永不驱逐；数值越大越不易抖动，但显存占用越高。"),
                    Mathf.Clamp(_settings.streamingPagePoolCapacity, 8, 512), 8, 512);
                EditorGUILayout.HelpBox(
                    "流式资产会在 .asset 旁生成版本化 .cmstream。运行时只保存逻辑文件名，由 ClusterMeshStreaming.RootPath / FilePathResolver 映射到 persistentDataPath、热更目录或其他真实文件路径；读取只使用异步 FileStream。Page 缺失时回退到最近已驻留父 LOD，不应出现空洞。",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "关闭：继续使用原有内嵌格式，不生成外部文件，也不进入 Page Streaming 代码路径。",
                    MessageType.None);
            }

            if (_bakeSkinnedAnimation)
                DrawSkinnedOptimizationGroup();
            else
                DrawStaticOptimizationGroup();

            EditorGUILayout.Space();
            if (GUILayout.Button("Bake", GUILayout.Height(28)))
                Bake();

            if (!string.IsNullOrEmpty(_error))
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            if (!string.IsNullOrEmpty(_info))
            {
                string next = _info.StartsWith("已写入", StringComparison.Ordinal)
                    ? (_bakeSkinnedAnimation
                        ? "\n下一步：选中资产，在 Inspector 点「加入场景」，然后在 ClusterSkinnedMeshRenderer 上预览动画/Cull。"
                        : "\n下一步：选中这个资产 → 加到 ClusterMeshRenderer.asset，或打开 Tools/ClusterMesh/Viewer。")
                    : "";
                EditorGUILayout.HelpBox(_info + next, MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawAnimationClipList()
        {
            if (_animationClips == null)
                _animationClips = new List<AnimationClip>();

            DrawAnimationClipFolder();
            EditorGUILayout.LabelField("Animation Clips", EditorStyles.boldLabel);
            int remove = -1;
            int moveFrom = -1;
            int moveTo = -1;
            for (int i = 0; i < _animationClips.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _animationClips[i] = (AnimationClip)EditorGUILayout.ObjectField(
                    new GUIContent("Clip " + i, "顺序对应 ClusterSkinnedMeshRenderer.clipIndex。"),
                    _animationClips[i], typeof(AnimationClip), false);
                using (new EditorGUI.DisabledScope(i == 0))
                {
                    if (GUILayout.Button("Up", GUILayout.Width(34f)))
                    {
                        moveFrom = i;
                        moveTo = i - 1;
                    }
                }
                using (new EditorGUI.DisabledScope(i + 1 >= _animationClips.Count))
                {
                    if (GUILayout.Button("Down", GUILayout.Width(44f)))
                    {
                        moveFrom = i;
                        moveTo = i + 1;
                    }
                }
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                    remove = i;
                EditorGUILayout.EndHorizontal();

                AnimationClip clip = _animationClips[i];
                if (clip != null)
                {
                    string rig = clip.isHumanMotion ? "Humanoid" : (clip.legacy ? "Legacy" : "Generic");
                    EditorGUILayout.LabelField("", clip.name + " · " + rig + " · " + clip.length.ToString("0.###") + "s");
                }
            }

            if (moveFrom >= 0)
            {
                AnimationClip clip = _animationClips[moveFrom];
                _animationClips.RemoveAt(moveFrom);
                _animationClips.Insert(moveTo, clip);
            }
            else if (remove >= 0)
            {
                _animationClips.RemoveAt(remove);
            }

            if (GUILayout.Button("Add Animation Clip"))
                _animationClips.Add(null);
            EditorGUILayout.HelpBox(
                "每个 Clip 会保存独立的拟合曲线与分段 Cull 数据；列表顺序就是运行时 Clip Index。",
                MessageType.None);
        }

        void DrawAnimationClipFolder()
        {
            _animationClipFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                new GUIContent("动画目录", "必须是 Project 里的文件夹。递归扫描其中的 AnimationClip 和 Prefab 内部 Clip。"),
                _animationClipFolder, typeof(DefaultAsset), false);
            if (GUILayout.Button("从目录追加"))
                AppendClipsFromFolder();
            EditorGUILayout.HelpBox(
                "递归扫描该目录及子目录。同时收集其中的 AnimationClip 资产，以及每个 Prefab 资产内部的 AnimationClip 子对象。两种都做，不用选择。结果追加到列表，已有的跳过。仍可一条条添加。",
                MessageType.None);
        }

        void AppendClipsFromFolder()
        {
            _error = null;
            _info = null;
            string folder = _animationClipFolder != null
                ? AssetDatabase.GetAssetPath(_animationClipFolder)
                : null;
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                _error = "请指定 Project 内文件夹。";
                return;
            }

            AnimationClip[] found = ClusterSkinnedAnimationClipFolderScan.Collect(folder);
            int added = ClusterSkinnedAnimationClipFolderScan.AppendUnique(_animationClips, found);
            _info = added == 0
                ? "目录里没有可追加的新 AnimationClip（已扫描 AnimationClip 资产与 Prefab 内部子对象）。"
                : "已追加 " + added + " 个 AnimationClip。";
        }

        AnimationClip[] ValidatedAnimationClips()
        {
            if (_animationClips == null || _animationClips.Count == 0)
                throw new InvalidOperationException("请至少添加一个 AnimationClip。");
            var result = new AnimationClip[_animationClips.Count];
            var unique = new HashSet<AnimationClip>();
            for (int i = 0; i < _animationClips.Count; i++)
            {
                AnimationClip clip = _animationClips[i];
                if (clip == null)
                    throw new InvalidOperationException("Animation Clip 列表第 " + i + " 项为空。");
                if (!unique.Add(clip))
                    throw new InvalidOperationException("Animation Clip 列表包含重复项：" + clip.name);
                result[i] = clip;
            }
            return result;
        }

        void Bake()
        {
            _error = null;
            _info = null;
            try
            {
                if (_bakeSkinnedAnimation)
                {
                    BakeSkinned();
                    return;
                }

                Mesh mesh = _mesh;
                Material[] materials = null;
                if (_sourceObject != null)
                {
                    if (_sourceObject.GetComponent<MeshFilter>() == null)
                        throw new InvalidOperationException("只支持 MeshFilter，不支持 SkinnedMesh。");
                    var filter = _sourceObject.GetComponent<MeshFilter>();
                    if (filter.GetComponent<SkinnedMeshRenderer>() != null && filter.sharedMesh == null)
                        throw new InvalidOperationException("SkinnedMesh 在 v1 不支持。");
                    mesh = filter.sharedMesh;
                    var renderer = _sourceObject.GetComponent<MeshRenderer>();
                    if (renderer != null)
                        materials = renderer.sharedMaterials;
                }

                if (mesh == null)
                    throw new InvalidOperationException("请指定 Mesh，或拖一个带 MeshFilter 的物体。");

                string folder = _outputFolder != null
                    ? AssetDatabase.GetAssetPath(_outputFolder)
                    : "Assets/ClusterMesh/Samples";
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/ClusterMesh"))
                        AssetDatabase.CreateFolder("Assets", "ClusterMesh");
                    if (!AssetDatabase.IsValidFolder("Assets/ClusterMesh/Samples"))
                        AssetDatabase.CreateFolder("Assets/ClusterMesh", "Samples");
                    folder = "Assets/ClusterMesh/Samples";
                }

                var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
                WriteAsset(asset, mesh, materials, _settings);
                string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + _assetName + ".asset");
                AssetDatabase.CreateAsset(asset, path);
                if (_settings.enableStreaming)
                    ClusterMeshStreamBaker.FinalizeStatic(asset, path, _settings);
                AssetDatabase.SaveAssets();
                int groupCount = asset.groups != null ? asset.groups.Length : 0;
                _info = "已写入 " + path + "，共 " + asset.clusters.Length + " 个 cluster" +
                    (_settings.buildLodHierarchy
                        ? "，" + groupCount + " 个 LOD 组（hierarchyVersion=" + asset.hierarchyVersion + "）。"
                        : "（未建层次，hierarchyVersion=" + asset.hierarchyVersion + "）。");
                Selection.activeObject = asset;
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
        }

        void BakeSkinned()
        {
            if (_skinnedRenderer == null || _skinnedRenderer.sharedMesh == null)
                throw new InvalidOperationException("请指定一个带 sharedMesh 的 SkinnedMeshRenderer。");
            AnimationClip[] clips = ValidatedAnimationClips();

            _settings.useQemSimplify = true;
            string folder = ResolveOutputFolder();
            var asset = ScriptableObject.CreateInstance<ClusterSkinnedMeshAsset>();
            var geometry = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            geometry.name = "Geometry";
            WriteSkinnedAsset(asset, geometry, _skinnedRenderer, clips, _settings, _skinnedBakeOptions);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + _assetName + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.AddObjectToAsset(geometry, asset);
            if (_settings.enableStreaming)
                ClusterMeshStreamBaker.FinalizeSkinned(asset, path, _settings);
            if (asset.gpuPaletteTextures != null)
            {
                for (int i = 0; i < asset.gpuPaletteTextures.Length; i++)
                {
                    if (asset.gpuPaletteTextures[i] != null)
                        AssetDatabase.AddObjectToAsset(asset.gpuPaletteTextures[i], asset);
                }
            }
            AssetDatabase.SaveAssets();
            int clusterCount = geometry.clusters != null ? geometry.clusters.Length : 0;
            int groupCount = geometry.groups != null ? geometry.groups.Length : 0;
            _info = "已写入 " + path + "，共 " + clusterCount + " 个蒙皮 cluster，" +
                groupCount + " 个 Skinning-Aware QEM LOD 组，" + asset.clips.Length + " 个动画 Clip。";
            Selection.activeObject = asset;
        }

        void DrawStaticOptimizationGroup()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("高级压缩（默认关闭）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "建议来自当前拖入的 Mesh / MeshFilter，只做参考，不会自动勾选。静态没有逐帧 Cull 表，不能压 Cull。",
                MessageType.None);
            _settings.packTightRestVertices = EditorGUILayout.Toggle(
                new GUIContent(
                    "紧凑 Rest 顶点",
                    "默认关闭。顶点从 32 字节改为 24 字节（位置仍 float，法线/切线改 oct）。旧资产保持 32 字节，不必重烤。"),
                _settings.packTightRestVertices);
            DrawCompressionAdvice(ClusterSkinnedCompressionAdvisor.AdviseTightRestVertices(ResolveStaticAdviceMesh()));
            EditorGUILayout.HelpBox(
                _settings.packTightRestVertices
                    ? "已开启：静态网格 GPU 更瘦。法线/切线是 oct 量化，法线贴图可能略有误差。\n优点：顶点显存约少 25%。缺点：改顶点格式，shader 走紧凑分支。"
                    : "默认关闭：顶点仍是 32 字节，和现有 ClusterMeshAsset 一致。",
                MessageType.None);
        }

        Mesh ResolveStaticAdviceMesh()
        {
            if (_sourceObject != null)
            {
                var filter = _sourceObject.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    return filter.sharedMesh;
            }

            return _mesh;
        }

        void DrawSkinnedOptimizationGroup()
        {
            if (_skinnedBakeOptions == null)
                _skinnedBakeOptions = new ClusterSkinnedMeshBakeOptions();

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("蒙皮动画优化", EditorStyles.boldLabel);
            _skinnedBakeOptions.animationDataMode = (ClusterSkinnedAnimationDataMode)EditorGUILayout.EnumPopup(
                new GUIContent(
                    "动画数据模式",
                    "决定资产中实际保存哪些动画数据。GPU Only 最小且最快；CPU Only 兼容无 VTF 的设备；GPU + CPU 允许运行时切换与回退，但资产最大。"),
                _skinnedBakeOptions.animationDataMode);

            switch (_skinnedBakeOptions.animationDataMode)
            {
                case ClusterSkinnedAnimationDataMode.GpuOnly:
                    EditorGUILayout.HelpBox(
                        "优点：只保存 VTF Palette Atlas，资产较小、运行时没有 CPU 曲线求值开销。\n" +
                        "缺点：设备不支持该 GPU 纹理格式或 VTF 路径时无法回退 CPU，并且 Renderer 只能使用 GPU Texture。",
                        MessageType.Info);
                    break;
                case ClusterSkinnedAnimationDataMode.CpuOnly:
                    EditorGUILayout.HelpBox(
                        "优点：不保存预烘焙 GPU Atlas，适合需要 CPU Job System + Burst 动态求值的路径。\n" +
                        "缺点：每帧需要在 CPU 求值骨骼曲线并上传浮点 Palette，仍需 Compute Shader 与顶点纹理读取；实例多时 CPU 和上传成本更高，Renderer 只能使用 CPU Curves。",
                        MessageType.Info);
                    break;
                default:
                    EditorGUILayout.HelpBox(
                        "优点：Renderer 可在 GPU Texture 与 CPU Curves 间切换，GPU 不可用时也能回退 CPU。\n" +
                        "缺点：同时保存 VTF Atlas 和 CPU Hermite 曲线，资产体积最大。",
                        MessageType.Info);
                    break;
            }

            RefreshCompressionAdvice();
            EditorGUILayout.HelpBox(
                "下面的建议来自当前拖入的 SkinnedMeshRenderer / AnimationClip，只做参考，不会自动勾选。默认仍全部关闭。",
                MessageType.None);

            if (_skinnedBakeOptions.IncludesGpu)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("GPU 优化", EditorStyles.boldLabel);
                _skinnedBakeOptions.gpuCompactPalette = EditorGUILayout.Toggle(
                    new GUIContent(
                        "紧凑 GPU Palette",
                        "默认关闭。每骨从 3 像素 3×4 矩阵改为 2 像素（四元数 + 位移/均匀缩放）。"),
                    _skinnedBakeOptions.gpuCompactPalette);
                DrawCompressionAdvice(_compressionAdvice.gpuCompactPalette);
                EditorGUILayout.HelpBox(
                    _skinnedBakeOptions.gpuCompactPalette
                        ? "已开启：图集约小三分之一。非均匀缩放会被平均成均匀缩放，骨骼拉伸可能歪。CPU palette 仍是 3×4，只影响 GPU Atlas。\n优点：显存/包体更小。缺点：有缩放的绑定姿势误差更大，需重烤。"
                        : "默认关闭：保持每骨 3×RGBAHalf 矩阵，和现有资产一致。",
                    MessageType.None);
                _skinnedBakeOptions.gpuFramesPerSecond = EditorGUILayout.Slider(
                    new GUIContent(
                        "VTF Bake FPS",
                        "GPU Palette Atlas 每秒保存的采样帧数。降低可近似线性减小纹理高度和资产大小；代价是快速动作的插值误差和抖动更明显。建议 PC 30，移动端可从 15 开始验证。"),
                    _skinnedBakeOptions.gpuFramesPerSecond, 1f, 60f);
                DrawCompressionAdvice(_compressionAdvice.gpuFramesPerSecond);
                EditorGUILayout.HelpBox(
                    "较低 FPS：纹理更小、显存和带宽更低，但快速动画精度下降。较高 FPS：动画更接近源 Clip，但纹理、包体和显存占用增加。",
                    MessageType.None);
            }

            if (_skinnedBakeOptions.IncludesCpu)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("CPU 优化", EditorStyles.boldLabel);
                _skinnedBakeOptions.cpuCurveTolerance = EditorGUILayout.Slider(
                    new GUIContent(
                        "Curve Fit Tolerance",
                        "CPU Hermite 曲线拟合允许的误差。值越大保留的曲线段越少、资产和求值成本越低；值越小越接近源动画，但数据更多。"),
                    _skinnedBakeOptions.cpuCurveTolerance, 0.000001f, 0.01f);
                _skinnedBakeOptions.retainAnimationCurves = EditorGUILayout.Toggle(
                    new GUIContent(
                        "保留 AnimationCurve",
                        "默认关闭。CPU 运行时读取的是已压缩的 Hermite 曲线段，并不需要原始 AnimationCurve。仅在需要人工检查拟合结果、调试或让外部编辑器工具读取原曲线时开启。"),
                    _skinnedBakeOptions.retainAnimationCurves);
                EditorGUILayout.HelpBox(
                    _skinnedBakeOptions.retainAnimationCurves
                        ? "已开启：资产会同时保存原始拟合 AnimationCurve 和 CPU Hermite 数据，方便检查，但属于重复数据，会明显增大多骨骼、多 Clip 资产。"
                        : "默认关闭：只保存运行时需要的 CPU Hermite 曲线段。不会影响 CPU Job System + Burst 播放，也不会影响 SceneView 预览。",
                    MessageType.None);
            }
            else
            {
                _skinnedBakeOptions.retainAnimationCurves = false;
            }

            if (!_skinnedBakeOptions.IncludesGpu)
                _skinnedBakeOptions.gpuCompactPalette = false;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("高级压缩（默认关闭）", EditorStyles.boldLabel);
            _skinnedBakeOptions.packSkinWeights8 = EditorGUILayout.Toggle(
                new GUIContent(
                    "权重 8 字节",
                    "默认关闭。把每顶点蒙皮权重从 16 字节压到 8 字节（8 位骨索引 + 8 位权重）。骨索引大于 255 时自动回退 16 字节，不会烘焙失败。"),
                _skinnedBakeOptions.packSkinWeights8);
            DrawCompressionAdvice(_compressionAdvice.packSkinWeights8);
            EditorGUILayout.HelpBox(
                _skinnedBakeOptions.packSkinWeights8
                    ? "已开启：权重显存减半。精度从 16 位降到 8 位，多骨骼混合可能略糊。超过 255 的骨索引会自动回退，不改坏烘焙。\n优点：顶点带宽小。缺点：骨索引 >255 的角色会回退 16 字节。"
                    : "默认关闭：保持 16 字节权重，和现有资产、shader 路径一致。",
                MessageType.None);

            _skinnedBakeOptions.compressCullFrames = EditorGUILayout.Toggle(
                new GUIContent(
                    "压缩 Cull 表",
                    "默认关闭。动画包围盒从每秒 4 段改为 2 段，并在磁盘上 Deflate。GPU 仍是 64 字节结构，锥剔除还在。"),
                _skinnedBakeOptions.compressCullFrames);
            DrawCompressionAdvice(_compressionAdvice.compressCullFrames);
            EditorGUILayout.HelpBox(
                _skinnedBakeOptions.compressCullFrames
                    ? "已开启：cull 段数减半，磁盘更小。盒会粗一点，可能少剔、多画。\n优点：长 clip × 多 cluster 时体积明显下降。缺点：剔得更松，极端姿态包围盒偏大。"
                    : "默认关闭：每秒 4 段、资产里存未压缩数组，和现有资产一致。",
                MessageType.None);

            _skinnedBakeOptions.packTightRestVertices = EditorGUILayout.Toggle(
                new GUIContent(
                    "紧凑 Rest 顶点",
                    "默认关闭。只改这份蒙皮 geometry：顶点从 32 字节改为 24 字节（位置仍 float，法线/切线改 oct）。静态 Baker 有独立开关。"),
                _skinnedBakeOptions.packTightRestVertices);
            DrawCompressionAdvice(_compressionAdvice.packTightRestVertices);
            EditorGUILayout.HelpBox(
                _skinnedBakeOptions.packTightRestVertices
                    ? "已开启：蒙皮 rest 网格 GPU 更瘦。法线/切线是 oct 量化，法线贴图可能略有误差。\n优点：顶点显存约少 25%。缺点：改顶点格式，shader 走紧凑分支。"
                    : "默认关闭：rest 顶点仍是 32 字节。",
                MessageType.None);

            EditorGUILayout.EndVertical();
        }

        void RefreshCompressionAdvice()
        {
            int hash = CompressionAdviceHash();
            if (hash == _compressionAdviceHash)
                return;
            _compressionAdviceHash = hash;
            _compressionAdvice = ClusterSkinnedCompressionAdvisor.Analyze(
                _skinnedRenderer, _animationClips, _settings);
        }

        int CompressionAdviceHash()
        {
            unchecked
            {
                int hash = _skinnedRenderer != null ? _skinnedRenderer.GetInstanceID() : 0;
                Mesh mesh = _skinnedRenderer != null ? _skinnedRenderer.sharedMesh : null;
                hash = hash * 31 + (mesh != null ? mesh.GetInstanceID() : 0);
                hash = hash * 31 + _settings.maxVerticesPerCluster;
                hash = hash * 31 + _settings.maxTrianglesPerCluster;
                hash = hash * 31 + (_settings.buildLodHierarchy ? 1 : 0);
                if (_animationClips == null)
                    return hash;
                hash = hash * 31 + _animationClips.Count;
                for (int i = 0; i < _animationClips.Count; i++)
                    hash = hash * 31 + (_animationClips[i] != null ? _animationClips[i].GetInstanceID() : 0);
                return hash;
            }
        }

        static void DrawCompressionAdvice(ClusterCompressionAdvice advice)
        {
            Color old = GUI.color;
            if (advice.kind == ClusterCompressionAdviceKind.Recommend)
                GUI.color = new Color(0.45f, 0.85f, 0.5f, 1f);
            else if (advice.kind == ClusterCompressionAdviceKind.NotRecommend)
                GUI.color = new Color(0.85f, 0.7f, 0.35f, 1f);
            else
                GUI.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            EditorGUILayout.LabelField(ClusterSkinnedCompressionAdvisor.Label(advice), EditorStyles.miniLabel);
            GUI.color = old;
        }

        string ResolveOutputFolder()
        {
            string folder = _outputFolder != null
                ? AssetDatabase.GetAssetPath(_outputFolder)
                : "Assets/ClusterMesh/Samples";
            if (AssetDatabase.IsValidFolder(folder))
                return folder;
            if (!AssetDatabase.IsValidFolder("Assets/ClusterMesh"))
                AssetDatabase.CreateFolder("Assets", "ClusterMesh");
            if (!AssetDatabase.IsValidFolder("Assets/ClusterMesh/Samples"))
                AssetDatabase.CreateFolder("Assets/ClusterMesh", "Samples");
            return "Assets/ClusterMesh/Samples";
        }
    }
}
