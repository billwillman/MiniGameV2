using System.Reflection;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ClusterMesh
{
    public static class ClusterMeshUrpBridge
    {
        static readonly FieldInfo RendererIndexField = typeof(UniversalAdditionalCameraData)
            .GetField("m_RendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo RendererDataListField = typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo DefaultRendererIndexField = typeof(UniversalRenderPipelineAsset)
            .GetField("m_DefaultRendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly PropertyInfo RenderingModeActualProperty = typeof(UniversalRenderer)
            .GetProperty("renderingModeActual", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo DeferredLightsField = typeof(UniversalRenderer)
            .GetField("m_DeferredLights", BindingFlags.Instance | BindingFlags.NonPublic);
        static System.Type _deferredLightsType;
        static PropertyInfo _gbufferAttachmentsProperty;
        static PropertyInfo _depthAttachmentProperty;
        static PropertyInfo _gbufferFormatsProperty;

        public static ScriptableRendererData RendererDataOverrideForTests;

        public const string DeferredSupportDescription =
            "ClusterMesh deferred rendering is supported only by URP Deferred; Built-in and HDRP deferred are not supported.";

        public static bool IsDeferred(ScriptableRenderer renderer)
        {
            if (!(renderer is UniversalRenderer) || RenderingModeActualProperty == null)
                return false;
            try
            {
                object value = RenderingModeActualProperty.GetValue(renderer);
                if (value == null)
                    return false;
                // Tuanjie URP 14 has Forward / ForwardPlus / Deferred only.
                // Compare by name so a later DeferredPlus member is optional.
                string name = value.ToString();
                return name == "Deferred" || name == "DeferredPlus";
            }
            catch
            {
                // A different URP implementation must keep the established Forward path alive.
                return false;
            }
        }

        public static bool TryGetDeferredTargets(
            ScriptableRenderer renderer,
            out RTHandle[] colors,
            out RTHandle depth,
            out GraphicsFormat[] formats)
        {
            colors = null;
            depth = null;
            formats = null;
            if (!IsDeferred(renderer) || DeferredLightsField == null)
                return false;
            try
            {
                object deferredLights = DeferredLightsField.GetValue(renderer);
                if (deferredLights == null)
                    return false;
                CacheDeferredProperties(deferredLights.GetType());
                colors = _gbufferAttachmentsProperty?.GetValue(deferredLights) as RTHandle[];
                depth = _depthAttachmentProperty?.GetValue(deferredLights) as RTHandle;
                formats = _gbufferFormatsProperty?.GetValue(deferredLights) as GraphicsFormat[];
                return colors != null && colors.Length > 0 && depth != null &&
                       formats != null && formats.Length == colors.Length;
            }
            catch
            {
                return false;
            }
        }

        static void CacheDeferredProperties(System.Type type)
        {
            if (_deferredLightsType == type)
                return;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _deferredLightsType = type;
            _gbufferAttachmentsProperty = type.GetProperty("GbufferAttachments", flags);
            _depthAttachmentProperty = type.GetProperty("DepthAttachment", flags);
            _gbufferFormatsProperty = type.GetProperty("GbufferFormats", flags);
        }

        public static bool HasActiveFeature(ScriptableRendererData data)
        {
            if (data == null || data.rendererFeatures == null)
                return false;
            for (int i = 0; i < data.rendererFeatures.Count; i++)
            {
                ScriptableRendererFeature feature = data.rendererFeatures[i];
                if (feature is ClusterMeshUrpFeature && feature.isActive)
                    return true;
            }

            return false;
        }

        public static bool ShouldSkipLegacyFlush(Camera camera)
        {
            return ShouldSubmitUrp(camera);
        }

        public static bool ShouldSubmitUrp(Camera camera)
        {
            // Game cameras (Play, Edit Mode Game View, and Player) use the Feature
            // when Setup URP attached an active ClusterMeshUrpFeature. Scene / Preview /
            // Reflection stay on Lifetime Update + Graphics.Draw.
            if (camera == null || camera.cameraType != CameraType.Game)
                return false;
            return TryGetRendererData(camera, out ScriptableRendererData data) && HasActiveFeature(data);
        }

        public static bool TryGetDefaultRendererData(out ScriptableRendererData data)
        {
            data = null;
            UniversalRenderPipelineAsset urp = UniversalRenderPipeline.asset;
            if (urp == null)
                return false;
            ScriptableRendererData[] list = RendererDataList(urp);
            if (list == null || list.Length == 0)
                return false;
            int index = DefaultRendererIndex(urp);
            if (index < 0 || index >= list.Length)
                index = 0;
            data = list[index];
            return data != null;
        }

        public static bool TryGetRendererData(Camera camera, out ScriptableRendererData data)
        {
            if (RendererDataOverrideForTests != null)
            {
                data = RendererDataOverrideForTests;
                return true;
            }

            data = null;
            if (camera == null)
                return false;
            UniversalRenderPipelineAsset urp = UniversalRenderPipeline.asset;
            if (urp == null)
                return false;
            ScriptableRendererData[] list = RendererDataList(urp);
            if (list == null || list.Length == 0)
                return false;
            int index = DefaultRendererIndex(urp);
            var extra = camera.GetUniversalAdditionalCameraData();
            if (extra != null && RendererIndexField != null)
            {
                int cameraIndex = (int)RendererIndexField.GetValue(extra);
                if (cameraIndex >= 0)
                    index = cameraIndex;
            }

            if (index < 0 || index >= list.Length)
                return false;
            data = list[index];
            return data != null;
        }

        static ScriptableRendererData[] RendererDataList(UniversalRenderPipelineAsset urp)
        {
            return RendererDataListField != null
                ? RendererDataListField.GetValue(urp) as ScriptableRendererData[]
                : null;
        }

        static int DefaultRendererIndex(UniversalRenderPipelineAsset urp)
        {
            if (DefaultRendererIndexField == null)
                return 0;
            return (int)DefaultRendererIndexField.GetValue(urp);
        }
    }
}
