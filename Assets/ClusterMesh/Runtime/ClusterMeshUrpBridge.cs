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
        static readonly PropertyInfo DeferredLightsProperty = typeof(UniversalRenderer)
            .GetProperty("deferredLights", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        static readonly FieldInfo MotionVectorColorField = typeof(UniversalRenderer)
            .GetField("m_MotionVectorColor", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo MotionVectorDepthField = typeof(UniversalRenderer)
            .GetField("m_MotionVectorDepth", BindingFlags.Instance | BindingFlags.NonPublic);
        static System.Type _deferredLightsType;
        static PropertyInfo _gbufferAttachmentsProperty;
        static FieldInfo _gbufferAttachmentsField;
        static PropertyInfo _depthAttachmentProperty;
        static PropertyInfo _depthAttachmentHandleProperty;
        static FieldInfo _depthAttachmentField;
        static PropertyInfo _gbufferFormatsProperty;
        static FieldInfo _gbufferFormatsField;

        public static string LastDeferredBindingError { get; private set; }

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
            if (!IsDeferred(renderer) || (DeferredLightsField == null && DeferredLightsProperty == null))
                return FailDeferredBinding("active renderer is not URP Deferred/Deferred+, or DeferredLights is unavailable");
            try
            {
                object deferredLights = DeferredLightsProperty != null
                    ? DeferredLightsProperty.GetValue(renderer)
                    : DeferredLightsField.GetValue(renderer);
                if (deferredLights == null)
                    return FailDeferredBinding("URP DeferredLights has not been initialized");
                CacheDeferredProperties(deferredLights.GetType());
                colors = ReadMember<RTHandle[]>(
                    deferredLights, _gbufferAttachmentsProperty, _gbufferAttachmentsField);
                depth = ReadMember<RTHandle>(
                    deferredLights, _depthAttachmentProperty, _depthAttachmentField);
                if (depth == null && _depthAttachmentHandleProperty != null)
                    depth = _depthAttachmentHandleProperty.GetValue(deferredLights) as RTHandle;
                formats = ReadMember<GraphicsFormat[]>(
                    deferredLights, _gbufferFormatsProperty, _gbufferFormatsField);

                if (colors == null || colors.Length < 4)
                    return FailDeferredBinding("URP returned fewer than the four required GBuffer MRT attachments");
                if (depth == null)
                    return FailDeferredBinding("URP returned no GBuffer depth attachment");
                if (formats == null || formats.Length != colors.Length)
                    return FailDeferredBinding("URP GBuffer format count does not match the MRT attachment count");
                if (colors.Length > SystemInfo.supportedRenderTargetCount)
                    return FailDeferredBinding("GBuffer MRT count exceeds this device's supported render-target count");
                for (int i = 0; i < colors.Length; i++)
                {
                    if (colors[i] == null)
                        return FailDeferredBinding("URP GBuffer MRT attachment " + i + " is null");
                    // URP index 3 is the lighting/camera-color attachment and
                    // intentionally reports None because its format is inherited.
                    if (formats[i] == GraphicsFormat.None && i != 3)
                        return FailDeferredBinding("URP GBuffer MRT format " + i + " is invalid");
                }

                LastDeferredBindingError = null;
                return true;
            }
            catch (System.Exception exception)
            {
                return FailDeferredBinding("reflection failed: " + exception.GetType().Name);
            }
        }

        public static bool TryGetMotionVectorTargets(
            ScriptableRenderer renderer,
            out RTHandle color,
            out RTHandle depth)
        {
            color = null;
            depth = null;
            if (!(renderer is UniversalRenderer) ||
                MotionVectorColorField == null || MotionVectorDepthField == null)
                return false;
            try
            {
                color = MotionVectorColorField.GetValue(renderer) as RTHandle;
                depth = MotionVectorDepthField.GetValue(renderer) as RTHandle;
                return color != null && depth != null;
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
            _gbufferAttachmentsProperty = FindProperty(type, flags, "GbufferAttachments", "GBufferAttachments");
            _gbufferAttachmentsField = FindField(type, flags, "m_GbufferAttachments", "m_GBufferAttachments");
            _depthAttachmentProperty = FindProperty(type, flags, "DepthAttachment");
            _depthAttachmentHandleProperty = FindProperty(type, flags, "DepthAttachmentHandle");
            _depthAttachmentField = FindField(type, flags, "m_DepthAttachment", "m_DepthAttachmentHandle");
            _gbufferFormatsProperty = FindProperty(type, flags, "GbufferFormats", "GBufferFormats");
            _gbufferFormatsField = FindField(type, flags, "m_GbufferFormats", "m_GBufferFormats");
        }

        static PropertyInfo FindProperty(System.Type type, BindingFlags flags, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                PropertyInfo property = type.GetProperty(names[i], flags);
                if (property != null)
                    return property;
            }
            return null;
        }

        static FieldInfo FindField(System.Type type, BindingFlags flags, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                FieldInfo field = type.GetField(names[i], flags);
                if (field != null)
                    return field;
            }
            return null;
        }

        static T ReadMember<T>(object instance, PropertyInfo property, FieldInfo field) where T : class
        {
            if (property != null)
                return property.GetValue(instance) as T;
            return field != null ? field.GetValue(instance) as T : null;
        }

        static bool FailDeferredBinding(string reason)
        {
            LastDeferredBindingError = reason;
            return false;
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
