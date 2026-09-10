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
        DefaultAsset _outputFolder;
        string _assetName = "ClusterMeshAsset";
        ClusterMeshBakeSettings _settings = new ClusterMeshBakeSettings();
        string _error;
        string _info;
        Vector2 _scroll;

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
            if (asset == null || geometry == null)
                throw new InvalidOperationException("Skinned ClusterMesh output assets are missing.");
            settings = settings ?? new ClusterMeshBakeSettings();
            settings.useQemSimplify = true;
            ClusterSkinnedMeshBakeResult result = ClusterSkinnedMeshBaker.Bake(renderer, clips, settings);
            geometry.CopyFrom(result.geometry, renderer.sharedMesh, settings);
            asset.geometry = geometry;
            asset.packedSkinWeights = ClusterSkinnedMeshBaker.PackSkinWeights(result.skinWeights);
            asset.bindPoses = result.bindPoses;
            asset.bonePaths = result.bonePaths;
            asset.boneParentIndices = result.boneParentIndices;
            asset.clips = result.clips;
            asset.gpuPaletteTextures = result.gpuPaletteTextures;
            asset.cpuCurveHeaders = result.cpuCurveHeaders;
            asset.cpuCurveSegments = result.cpuCurveSegments;
            asset.boneEvaluationOrder = result.boneEvaluationOrder;
            asset.cullFrames = result.cullFrames;
            asset.skinningVersion = ClusterSkinnedMeshAsset.CurrentSkinningVersion;
            asset.animationSamplingVersion = ClusterSkinnedMeshAsset.CurrentAnimationSamplingVersion;
            asset.gpuAnimationVersion = ClusterSkinnedMeshAsset.CurrentGpuAnimationVersion;
            asset.cpuBurstAnimationVersion = ClusterSkinnedMeshAsset.CurrentCpuBurstAnimationVersion;
            asset.skinVertexCount = result.skinWeights != null ? result.skinWeights.Length : 0;
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("怎么用", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "普通模式把静态 Mesh 拆成 ClusterMeshAsset；勾选蒙皮动画后会生成独立的 ClusterSkinnedMeshAsset。\n\n" +
                "1. 普通模式拖 Mesh/MeshFilter；蒙皮模式拖 SkinnedMeshRenderer 并添加一个或多个 AnimationClip。\n" +
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
                    ? "当前：蒙皮。拖 SkinnedMeshRenderer + AnimationClip 列表，生成 ClusterSkinnedMeshAsset。"
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
            if (GUILayout.Button("Bake", GUILayout.Height(28)))
                Bake();

            if (!string.IsNullOrEmpty(_error))
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            if (!string.IsNullOrEmpty(_info))
                EditorGUILayout.HelpBox(_info + (_bakeSkinnedAnimation
                    ? "\n下一步：选中资产，在 Inspector 点「加入场景」，然后在 ClusterSkinnedMeshRenderer 上预览动画/Cull。"
                    : "\n下一步：选中这个资产 → 加到 ClusterMeshRenderer.asset，或打开 Tools/ClusterMesh/Viewer。"), MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        void DrawAnimationClipList()
        {
            if (_animationClips == null)
                _animationClips = new List<AnimationClip>();

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
            WriteSkinnedAsset(asset, geometry, _skinnedRenderer, clips, _settings);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + _assetName + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.AddObjectToAsset(geometry, asset);
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
