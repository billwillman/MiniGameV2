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
        const string SettingsFolder = "Assets/ClusterMesh/Settings";
        const string PipelineAssetPath = SettingsFolder + "/ClusterMeshURPDeferred.asset";
        const string RendererAssetPath = SettingsFolder + "/ClusterMeshURPDeferredRenderer.asset";

        [MenuItem("Tools/ClusterMesh/Setup URP Deferred (One Click)", priority = 0)]
        public static void SetupUrpDeferred()
        {
            List<UniversalRenderPipelineAsset> assets = CollectProjectUrpAssets();
            if (assets.Count == 0)
                assets.Add(CreateUrpDeferredAssets());

            UniversalRenderPipelineAsset primary = assets[0];
            int configuredRenderers = 0;
            for (int i = 0; i < assets.Count; i++)
                configuredRenderers += ConfigurePipeline(assets[i]);

            GraphicsSettings.renderPipelineAsset = primary;
            AssignAllQualityLevelsToUrp(primary);
            EditorUtility.SetDirty(primary);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = primary;
            Debug.Log($"ClusterMesh: URP Deferred 一键配置完成。管线资产 {assets.Count} 个，Universal Renderer {configuredRenderers} 个，已启用 ClusterMesh URP Feature。" +
                      " 延迟渲染仅支持 URP Deferred/Deferred+，不支持 Built-in/HDRP 延迟。");
        }

        [MenuItem("Tools/ClusterMesh/Enable URP Feature")]
        public static void Enable()
        {
            if (!TryGetDefaultRenderer(out UniversalRendererData data))
            {
                Debug.LogError("ClusterMesh: 当前没有 URP Asset 或 Default Renderer。");
                return;
            }

            EnableOn(data);
            EditorUtility.SetDirty(data);
            Debug.Log("ClusterMesh: 已在 Default Renderer 上启用 URP Feature。延迟渲染仅支持 URP Deferred/Deferred+，不支持 Built-in/HDRP 延迟。");
        }

        [MenuItem("Tools/ClusterMesh/Enable URP Feature", true)]
        public static bool EnableValidate()
        {
            return ClusterMeshUrpBridge.TryGetDefaultRendererData(out _);
        }

        [MenuItem("Tools/ClusterMesh/Disable URP Feature")]
        public static void Disable()
        {
            if (!TryGetDefaultRenderer(out UniversalRendererData data))
            {
                Debug.LogError("ClusterMesh: 当前没有 URP Asset 或 Default Renderer。");
                return;
            }

            DisableOn(data);
            EditorUtility.SetDirty(data);
            Debug.Log("ClusterMesh: 已从 Default Renderer 卸下 URP Feature。");
        }

        [MenuItem("Tools/ClusterMesh/Disable URP Feature", true)]
        public static bool DisableValidate()
        {
            return TryGetDefaultRenderer(out UniversalRendererData data) && HasFeature(data);
        }

        public static bool TryGetDefaultRenderer(out UniversalRendererData data)
        {
            data = null;
            if (!ClusterMeshUrpBridge.TryGetDefaultRendererData(out ScriptableRendererData raw))
                return false;
            data = raw as UniversalRendererData;
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

        public static void ConfigureDeferredOn(UniversalRendererData data)
        {
            if (data == null)
                return;
            data.renderingMode = RenderingMode.Deferred;
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
            AddUnique(result, GraphicsSettings.renderPipelineAsset as UniversalRenderPipelineAsset);
            for (int i = 0; i < QualitySettings.names.Length; i++)
                AddUnique(result, QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset);
            AddUnique(result, UniversalRenderPipeline.asset);
            return result;
        }

        static void AddUnique(List<UniversalRenderPipelineAsset> result, UniversalRenderPipelineAsset asset)
        {
            if (asset != null && !result.Contains(asset))
                result.Add(asset);
        }

        static UniversalRenderPipelineAsset CreateUrpDeferredAssets()
        {
            EnsureAssetFolder(SettingsFolder);
            string rendererPath = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(RendererAssetPath) == null
                ? RendererAssetPath
                : AssetDatabase.GenerateUniqueAssetPath(RendererAssetPath);
            UniversalRendererData rendererData = CreateUniversalRendererData(rendererPath);
            ConfigureDeferredOn(rendererData);

            string pipelinePath = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PipelineAssetPath) == null
                ? PipelineAssetPath
                : AssetDatabase.GenerateUniqueAssetPath(PipelineAssetPath);
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

        static int ConfigurePipeline(UniversalRenderPipelineAsset pipeline)
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
            for (int i = 0; i < list.arraySize; i++)
            {
                var data = list.GetArrayElementAtIndex(i).objectReferenceValue as UniversalRendererData;
                if (data == null)
                    continue;
                if (universalDefault < 0)
                    universalDefault = i;
                ConfigureDeferredOn(data);
                configured++;
            }

            if (universalDefault < 0)
            {
                string pipelinePath = AssetDatabase.GetAssetPath(pipeline);
                string directory = string.IsNullOrEmpty(pipelinePath)
                    ? SettingsFolder
                    : Path.GetDirectoryName(pipelinePath).Replace('\\', '/');
                EnsureAssetFolder(directory);
                string rendererPath = AssetDatabase.GenerateUniqueAssetPath(
                    directory + "/" + pipeline.name + "_ClusterMeshDeferredRenderer.asset");
                UniversalRendererData data = CreateUniversalRendererData(rendererPath);
                ConfigureDeferredOn(data);
                int index = list.arraySize;
                list.InsertArrayElementAtIndex(index);
                list.GetArrayElementAtIndex(index).objectReferenceValue = data;
                universalDefault = index;
                configured++;
            }

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
                "延迟渲染支持范围：仅 URP Deferred / Deferred+。不支持 Built-in、HDRP 或其他渲染管线的延迟路径。URP Forward 路径保持兼容。",
                MessageType.Info);
        }
    }
}
