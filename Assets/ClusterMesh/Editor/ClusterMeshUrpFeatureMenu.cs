using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ClusterMesh
{
    public static class ClusterMeshUrpFeatureMenu
    {
        const string FeatureName = "ClusterMesh URP";
        const string SettingsFolderName = "Settings";
        const string DeferredPipelineFile = "ClusterMeshURPDeferred.asset";
        const string DeferredRendererFile = "ClusterMeshURPDeferredRenderer.asset";
        const string RuntimeAsmdefName = "ClusterMesh.Runtime";

        public static bool? RadiantInstalledOverrideForTests;

        [MenuItem("Tools/ClusterMesh/Setup URP Deferred (One Click)", priority = 0)]
        public static void SetupUrpDeferred()
        {
            SetupUrpDeferredCore(null,
                "ClusterMesh: URP Deferred 一键配置完成。管线资产 {0} 个，Universal Renderer {1} 个，已启用 ClusterMesh URP Feature。" +
                " 延迟渲染仅支持 URP Deferred/Deferred+，不支持 Built-in/HDRP 延迟。");
        }

        [MenuItem("Tools/ClusterMesh/Setup Radiant URP 延迟渲染", priority = 2)]
        public static void SetupRadiantUrpDeferred()
        {
            if (!IsRadiantInstalled())
            {
                EditorUtility.DisplayDialog(
                    "ClusterMesh",
                    "没有安装 Radiant。请先把 Radiant GI 导入项目后再执行 Setup Radiant URP 延迟渲染。",
                    "确定");
                Debug.LogWarning("ClusterMesh: 没有安装 Radiant，已取消 Setup Radiant URP 延迟渲染。");
                return;
            }

            SetupUrpDeferredCore(EnableRadiantOn,
                "ClusterMesh: Radiant URP 延迟渲染已配置。管线资产 {0} 个，Universal Renderer {1} 个，已启用 ClusterMesh URP Feature 和 RadiantRenderFeature。" +
                " Volume 里仍需 Radiant Global Illumination 才会出 GI。");
        }

        [MenuItem("Tools/ClusterMesh/Setup URP Forward (One Click)", priority = 1)]
        public static void SetupUrpForward()
        {
            List<UniversalRenderPipelineAsset> assets = CollectProjectUrpAssets();
            if (assets.Count == 0)
            {
                Debug.LogError("ClusterMesh: 当前没有 URP Asset，无法切回 Forward。请先指定管线资产或使用 Setup URP Deferred。");
                return;
            }

            UniversalRenderPipelineAsset primary = assets[0];
            int configuredRenderers = 0;
            for (int i = 0; i < assets.Count; i++)
                configuredRenderers += ConfigurePipeline(assets[i], RenderingMode.Forward);

            GraphicsSettings.renderPipelineAsset = primary;
            AssignAllQualityLevelsToUrp(primary);
            EditorUtility.SetDirty(primary);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = primary;
            Debug.Log($"ClusterMesh: URP Forward 一键配置完成。管线资产 {assets.Count} 个，Universal Renderer {configuredRenderers} 个，ClusterMesh URP Feature 保持启用。");
        }

        [MenuItem("Tools/ClusterMesh/Setup URP Forward (One Click)", true)]
        public static bool SetupUrpForwardValidate()
        {
            return CollectProjectUrpAssets().Count > 0;
        }

        [MenuItem("Tools/ClusterMesh/Enable URP Feature")]
        public static void Enable()
        {
            int count = ForEachDefaultRenderer(EnableOn);
            if (count <= 0)
            {
                Debug.LogError("ClusterMesh: 当前没有已指定的 URP Asset 或 Default Renderer。");
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("ClusterMesh: 已在 " + count + " 个 URP Default Renderer 上启用 Feature。延迟渲染仅支持 URP Deferred/Deferred+，不支持 Built-in/HDRP 延迟。");
        }

        [MenuItem("Tools/ClusterMesh/Enable URP Feature", true)]
        public static bool EnableValidate()
        {
            return CountDefaultRenderers() > 0;
        }

        [MenuItem("Tools/ClusterMesh/Disable URP Feature")]
        public static void Disable()
        {
            int count = ForEachDefaultRenderer(DisableOn);
            if (count <= 0)
            {
                Debug.LogError("ClusterMesh: 当前没有已指定的 URP Asset 或 Default Renderer。");
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("ClusterMesh: 已从 " + count + " 个 URP Default Renderer 卸下 Feature。");
        }

        [MenuItem("Tools/ClusterMesh/Disable URP Feature", true)]
        public static bool DisableValidate()
        {
            return ForEachDefaultRenderer(null, onlyIfHasFeature: true) > 0;
        }

        public static bool TryGetDefaultRenderer(out UniversalRendererData data)
        {
            data = null;
            List<UniversalRenderPipelineAsset> assets = CollectProjectUrpAssets();
            for (int i = 0; i < assets.Count; i++)
            {
                if (TryGetDefaultRenderer(assets[i], out data))
                    return true;
            }

            return false;
        }

        public static bool TryGetDefaultRenderer(UniversalRenderPipelineAsset pipeline, out UniversalRendererData data)
        {
            data = null;
            if (pipeline == null)
                return false;
            var serialized = new SerializedObject(pipeline);
            SerializedProperty list = serialized.FindProperty("m_RendererDataList");
            SerializedProperty defaultIndex = serialized.FindProperty("m_DefaultRendererIndex");
            if (list == null || defaultIndex == null || list.arraySize == 0)
                return false;
            int index = defaultIndex.intValue;
            if (index < 0 || index >= list.arraySize)
                index = 0;
            data = list.GetArrayElementAtIndex(index).objectReferenceValue as UniversalRendererData;
            return data != null;
        }

        public static bool HasFeature(ScriptableRendererData data)
        {
            return FindFeature(data) != null;
        }

        public static void EnableOn(ScriptableRendererData data)
        {
            if (data == null)
                return;
            ClusterMeshUrpFeature existing = FindFeature(data);
            if (existing != null)
            {
                existing.SetActive(true);
                data.SetDirty();
                return;
            }

            var feature = ScriptableObject.CreateInstance<ClusterMeshUrpFeature>();
            feature.name = FeatureName;
            feature.SetActive(true);
            string path = AssetDatabase.GetAssetPath(data);
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.AddObjectToAsset(feature, data);
            data.rendererFeatures.Add(feature);
            data.SetDirty();
        }

        public static bool IsRadiantInstalled()
        {
            if (RadiantInstalledOverrideForTests.HasValue)
                return RadiantInstalledOverrideForTests.Value;
            return FindRadiantRenderFeatureType() != null;
        }

        public static Type FindRadiantRenderFeatureType()
        {
            const string fullName = "RadiantGI.Universal.RadiantRenderFeature";
            Type direct = Type.GetType(fullName);
            if (IsRendererFeatureType(direct))
                return direct;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type found = assemblies[i].GetType(fullName);
                if (IsRendererFeatureType(found))
                    return found;
            }

            return null;
        }

        public static bool HasRadiantFeature(ScriptableRendererData data)
        {
            Type type = FindRadiantRenderFeatureType();
            if (type == null || data == null || data.rendererFeatures == null)
                return false;
            for (int i = 0; i < data.rendererFeatures.Count; i++)
            {
                if (data.rendererFeatures[i] != null && type.IsInstanceOfType(data.rendererFeatures[i]))
                    return true;
            }

            return false;
        }

        public static void EnableRadiantOn(ScriptableRendererData data)
        {
            if (data == null)
                return;
            Type type = FindRadiantRenderFeatureType();
            if (type == null)
                return;
            if (HasRadiantFeature(data))
            {
                ActivateExistingRadiant(data, type);
                data.SetDirty();
                return;
            }

            var feature = ScriptableObject.CreateInstance(type) as ScriptableRendererFeature;
            if (feature == null)
                return;
            feature.name = "RadiantRenderFeature";
            feature.SetActive(true);
            FieldInfo pathField = type.GetField("renderingPath");
            if (pathField != null && pathField.FieldType.IsEnum)
                pathField.SetValue(feature, Enum.ToObject(pathField.FieldType, 1));
            string path = AssetDatabase.GetAssetPath(data);
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.AddObjectToAsset(feature, data);
            data.rendererFeatures.Add(feature);
            data.SetDirty();
        }

        public static string ResolveModuleFolder()
        {
            string runtime = FolderFromAsmdef(RuntimeAsmdefName);
            if (!string.IsNullOrEmpty(runtime))
                return ParentFolder(runtime);
            string editor = FolderFromAsmdef("ClusterMesh.Editor");
            if (!string.IsNullOrEmpty(editor))
                return ParentFolder(editor);
            return null;
        }

        public static string ResolveSettingsFolder()
        {
            string module = ResolveModuleFolder();
            return string.IsNullOrEmpty(module) ? null : module + "/" + SettingsFolderName;
        }

        static string DeferredPipelinePath()
        {
            return ResolveSettingsFolder() + "/" + DeferredPipelineFile;
        }

        static string DeferredRendererPath()
        {
            return ResolveSettingsFolder() + "/" + DeferredRendererFile;
        }

        static void ActivateExistingRadiant(ScriptableRendererData data, Type type)
        {
            for (int i = 0; i < data.rendererFeatures.Count; i++)
            {
                ScriptableRendererFeature feature = data.rendererFeatures[i];
                if (feature != null && type.IsInstanceOfType(feature))
                    feature.SetActive(true);
            }
        }

        static bool IsRendererFeatureType(Type type)
        {
            return type != null && typeof(ScriptableRendererFeature).IsAssignableFrom(type);
        }

        static int CountDefaultRenderers()
        {
            return ForEachDefaultRenderer(null);
        }

        static int ForEachDefaultRenderer(Action<ScriptableRendererData> action, bool onlyIfHasFeature = false)
        {
            List<UniversalRenderPipelineAsset> assets = CollectProjectUrpAssets();
            int count = 0;
            for (int i = 0; i < assets.Count; i++)
            {
                if (!TryGetDefaultRenderer(assets[i], out UniversalRendererData data))
                    continue;
                if (onlyIfHasFeature && !HasFeature(data))
                    continue;
                action?.Invoke(data);
                if (action != null)
                    EditorUtility.SetDirty(data);
                count++;
            }

            return count;
        }

        static string FolderFromAsmdef(string asmdefName)
        {
            string[] guids = AssetDatabase.FindAssets(asmdefName + " t:asmdef");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;
                if (Path.GetFileNameWithoutExtension(path) != asmdefName)
                    continue;
                return Path.GetDirectoryName(path).Replace('\\', '/');
            }

            return null;
        }

        static string ParentFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return null;
            string parent = Path.GetDirectoryName(folder);
            return string.IsNullOrEmpty(parent) ? folder : parent.Replace('\\', '/');
        }

        static void SetupUrpDeferredCore(Action<UniversalRendererData> extra, string logFormat)
        {
            List<UniversalRenderPipelineAsset> assets = CollectProjectUrpAssets();
            if (assets.Count == 0)
            {
                UniversalRenderPipelineAsset created = CreateUrpDeferredAssets();
                if (created == null)
                {
                    Debug.LogError("ClusterMesh: 找不到 ClusterMesh 模块目录，无法创建 URP 资产。请把 ClusterMesh 作为完整模块导入。");
                    return;
                }

                assets.Add(created);
            }

            UniversalRenderPipelineAsset primary = assets[0];
            int configuredRenderers = 0;
            for (int i = 0; i < assets.Count; i++)
                configuredRenderers += ConfigurePipeline(assets[i], RenderingMode.Deferred, extra);

            GraphicsSettings.renderPipelineAsset = primary;
            AssignAllQualityLevelsToUrp(primary);
            EditorUtility.SetDirty(primary);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = primary;
            Debug.Log(string.Format(logFormat, assets.Count, configuredRenderers));
        }

        public static void ConfigureDeferredOn(UniversalRendererData data)
        {
            if (data == null)
                return;
            data.renderingMode = RenderingMode.Deferred;
            EnableOn(data);
            EditorUtility.SetDirty(data);
        }

        public static void ConfigureForwardOn(UniversalRendererData data)
        {
            if (data == null)
                return;
            data.renderingMode = RenderingMode.Forward;
            EnableOn(data);
            EditorUtility.SetDirty(data);
        }

        public static void DisableOn(ScriptableRendererData data)
        {
            if (data == null || data.rendererFeatures == null)
                return;
            for (int i = data.rendererFeatures.Count - 1; i >= 0; i--)
            {
                if (!(data.rendererFeatures[i] is ClusterMeshUrpFeature feature))
                    continue;
                data.rendererFeatures.RemoveAt(i);
                string path = AssetDatabase.GetAssetPath(feature);
                if (!string.IsNullOrEmpty(path))
                    AssetDatabase.RemoveObjectFromAsset(feature);
                UnityEngine.Object.DestroyImmediate(feature, true);
            }

            data.SetDirty();
        }

        static ClusterMeshUrpFeature FindFeature(ScriptableRendererData data)
        {
            if (data == null || data.rendererFeatures == null)
                return null;
            for (int i = 0; i < data.rendererFeatures.Count; i++)
            {
                if (data.rendererFeatures[i] is ClusterMeshUrpFeature feature)
                    return feature;
            }

            return null;
        }

        static List<UniversalRenderPipelineAsset> CollectProjectUrpAssets()
        {
            var result = new List<UniversalRenderPipelineAsset>();
            AddUnique(result, UniversalRenderPipeline.asset);
            AddUnique(result, GraphicsSettings.renderPipelineAsset as UniversalRenderPipelineAsset);
            for (int i = 0; i < QualitySettings.names.Length; i++)
                AddUnique(result, QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset);
            return result;
        }

        static void AddUnique(List<UniversalRenderPipelineAsset> result, UniversalRenderPipelineAsset asset)
        {
            if (asset != null && !result.Contains(asset))
                result.Add(asset);
        }

        static UniversalRenderPipelineAsset CreateUrpDeferredAssets()
        {
            string settingsFolder = ResolveSettingsFolder();
            if (string.IsNullOrEmpty(settingsFolder))
                return null;
            string pipelineAssetPath = DeferredPipelinePath();
            string rendererAssetPath = DeferredRendererPath();
            EnsureAssetFolder(settingsFolder);
            UniversalRenderPipelineAsset existing =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelineAssetPath);
            if (existing != null)
                return existing;
            string rendererPath = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(rendererAssetPath) == null
                ? rendererAssetPath
                : AssetDatabase.GenerateUniqueAssetPath(rendererAssetPath);
            UniversalRendererData rendererData = CreateUniversalRendererData(rendererPath);
            ConfigureDeferredOn(rendererData);

            string pipelinePath = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(pipelineAssetPath) == null
                ? pipelineAssetPath
                : AssetDatabase.GenerateUniqueAssetPath(pipelineAssetPath);
            UniversalRenderPipelineAsset pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            pipeline.name = Path.GetFileNameWithoutExtension(pipelinePath);
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
            return pipeline;
        }

        static UniversalRendererData CreateUniversalRendererData(string path)
        {
            MethodInfo method = typeof(UniversalRenderPipelineAsset).GetMethod(
                "CreateRendererAsset", BindingFlags.Static | BindingFlags.NonPublic);
            if (method != null)
            {
                ParameterInfo[] parameters = method.GetParameters();
                object rendererType = Enum.Parse(parameters[1].ParameterType, "UniversalRenderer");
                var created = method.Invoke(null, new[] { (object)path, rendererType, false, "Renderer" }) as UniversalRendererData;
                if (created != null)
                    return created;
            }

            var fallback = ScriptableObject.CreateInstance<UniversalRendererData>();
            fallback.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(fallback, path);
            return fallback;
        }

        static int ConfigurePipeline(
            UniversalRenderPipelineAsset pipeline,
            RenderingMode mode = RenderingMode.Deferred,
            Action<UniversalRendererData> extra = null)
        {
            if (pipeline == null)
                return 0;
            var serialized = new SerializedObject(pipeline);
            SerializedProperty list = serialized.FindProperty("m_RendererDataList");
            SerializedProperty defaultIndex = serialized.FindProperty("m_DefaultRendererIndex");
            if (list == null || defaultIndex == null)
                return 0;

            int universalDefault = -1;
            int configured = 0;
            int currentDefault = defaultIndex.intValue;
            if (currentDefault >= 0 && currentDefault < list.arraySize &&
                list.GetArrayElementAtIndex(currentDefault).objectReferenceValue is UniversalRendererData)
            {
                universalDefault = currentDefault;
            }
            for (int i = 0; i < list.arraySize; i++)
            {
                var data = list.GetArrayElementAtIndex(i).objectReferenceValue as UniversalRendererData;
                if (data == null)
                    continue;
                if (universalDefault < 0)
                    universalDefault = i;
                if (mode == RenderingMode.Deferred)
                    ConfigureDeferredOn(data);
                else
                    ConfigureForwardOn(data);
                extra?.Invoke(data);
                configured++;
            }

            if (universalDefault < 0 && mode == RenderingMode.Deferred)
            {
                string pipelinePath = AssetDatabase.GetAssetPath(pipeline);
                string directory = string.IsNullOrEmpty(pipelinePath)
                    ? ResolveSettingsFolder()
                    : Path.GetDirectoryName(pipelinePath).Replace('\\', '/');
                EnsureAssetFolder(directory);
                string rendererPath = AssetDatabase.GenerateUniqueAssetPath(
                    directory + "/" + pipeline.name + "_ClusterMeshDeferredRenderer.asset");
                UniversalRendererData data = CreateUniversalRendererData(rendererPath);
                ConfigureDeferredOn(data);
                extra?.Invoke(data);
                int index = list.arraySize;
                list.InsertArrayElementAtIndex(index);
                list.GetArrayElementAtIndex(index).objectReferenceValue = data;
                universalDefault = index;
                configured++;
            }

            if (universalDefault >= 0)
                defaultIndex.intValue = universalDefault;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            return configured;
        }

        static void AssignAllQualityLevelsToUrp(UniversalRenderPipelineAsset fallback)
        {
            int originalQuality = QualitySettings.GetQualityLevel();
            try
            {
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    RenderPipelineAsset current = QualitySettings.GetRenderPipelineAssetAt(i);
                    if (current is UniversalRenderPipelineAsset)
                        continue;
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = fallback;
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(originalQuality, false);
            }
        }

        static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }

    [CustomEditor(typeof(ClusterMeshUrpFeature))]
    sealed class ClusterMeshUrpFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "延迟渲染支持范围：仅 URP Deferred / Deferred+。不支持 Built-in、HDRP 或其他渲染管线的延迟路径。URP Forward 路径保持兼容。\n\n" +
                "Motion Vector Pass 等管线时机在 Tools/ClusterMesh/通用设置，不在单个 Renderer 上。" +
                "默认 After Skybox + 1：早于 Radiant + 2，Radiant GI / TAA 才能采到 ClusterMesh 速度。",
                MessageType.Info);
            if (GUILayout.Button("打开通用设置"))
                ClusterMeshSettingsWindow.Open();
        }
    }
}
