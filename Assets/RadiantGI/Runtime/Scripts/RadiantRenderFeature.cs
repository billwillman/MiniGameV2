using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_2023_3_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif
using UnityEngine.Rendering.Universal;
using DebugView = RadiantGI.Universal.RadiantGlobalIllumination.DebugView;
using DeferredLights = UnityEngine.Rendering.Universal.Internal.DeferredLights;

namespace RadiantGI.Universal {

    [HelpURL("https://kronnect.com/docs/radiant-gi-urp/")]
    public partial class RadiantRenderFeature : ScriptableRendererFeature {

        public static bool IsAmbientProbeValid(SphericalHarmonicsL2 probe) {
            if (RenderSettings.ambientIntensity < 0.001f) return true;
            if (RenderSettings.ambientMode == AmbientMode.Flat && RenderSettings.ambientLight.maxColorComponent < 0.001f) return true;
            if (RenderSettings.ambientMode == AmbientMode.Trilight && RenderSettings.ambientSkyColor.maxColorComponent < 0.001f && RenderSettings.ambientEquatorColor.maxColorComponent < 0.001f && RenderSettings.ambientGroundColor.maxColorComponent < 0.001f) return true;

            float sumSq = 0f;
            for (int rgb = 0; rgb < 3; rgb++) {
                for (int i = 0; i < 9; i++) {
                    float v = probe[rgb, i];
                    sumSq += v * v;
                }
            }
            return sumSq > 1e-6f;
        }

        public enum RenderingPath {
            [InspectorName("Forward | Forward+")]
            Forward,
            [InspectorName("Deferred | Deferred+")]
            Deferred,
            Both
        }

        enum Pass {
            CopyExact,
            Raycast,
            TemporalAccum,
            Albedo,
            Normals,
            Compose,
            Compare,
            FinalGIDebug,
            Copy,
            WideFilter,
            Depth,
            CopyDepth,
            RSM,
            NFO,
            NFOBlur,
            RayAccum,
            MotionVectors,
            AmbientStrip,
            RenderingLayerMask
        }

        static readonly List<RadiantVirtualEmitter> emitters = new List<RadiantVirtualEmitter>();
        static bool emittersForceRefresh;
        static readonly List<RadiantTransparentSupport> transparentSupport = new List<RadiantTransparentSupport>();

        static class ShaderParams {

            // targets
            public static int MainTex = Shader.PropertyToID("_MainTex");
            public static int BlurSampleStep = Shader.PropertyToID("_BlurSampleStep");
            public static int ResolveRT = Shader.PropertyToID("_ResolveRT");
            public static int SourceSize = Shader.PropertyToID("_SourceSize");
            public static int NoiseTex = Shader.PropertyToID("_NoiseTex");
            public static int Downscaled1RT = Shader.PropertyToID("_Downscaled1RT");
            public static int Downscaled2RT = Shader.PropertyToID("_Downscaled2RT");
            public static int InputRT = Shader.PropertyToID("_InputRTGI");
            public static int CompareTex = Shader.PropertyToID("_CompareTexGI");
            public static int PrevResolve = Shader.PropertyToID("_RadiantPrevResolve");
            public static int RayBufferRT = Shader.PropertyToID("_RadiantRayBufferRT");
            public static int RayConfidenceRT = Shader.PropertyToID("_RadiantRayConfidenceRT");
            public static int DownscaledDepthRT = Shader.PropertyToID("_DownscaledDepthRT");
            public static int TransparentDepthTexture = Shader.PropertyToID("_RadiantTransparentDepthTexture");
            public static int RadiantGITexture = Shader.PropertyToID("_RadiantGITexture");
            public static int Probe1Cube = Shader.PropertyToID("_Probe1Cube");
            public static int Probe2Cube = Shader.PropertyToID("_Probe2Cube");
            public static int NFO_RT = Shader.PropertyToID("_NFO_RT");
            public static int NFOBlurRT = Shader.PropertyToID("_NFOBlurRT");
            public static int RSMBuffer = Shader.PropertyToID("_RadiantRSMBuffer");

            // RG supplemental ids
            public static int CameraDepthTexture = Shader.PropertyToID("_CameraDepthTexture");
            public static int CameraNormalsTexture = Shader.PropertyToID("_CameraNormalsTexture");
            public static int MotionVectorTexture = Shader.PropertyToID("_MotionVectorTexture");
            public static int CameraGBuffer0 = Shader.PropertyToID("_GBuffer0");
            public static int CameraGBuffer1 = Shader.PropertyToID("_GBuffer1");
            public static int CameraGBuffer2 = Shader.PropertyToID("_GBuffer2");

            // uniforms
            public static int IndirectData = Shader.PropertyToID("_IndirectData");
            public static int RayData = Shader.PropertyToID("_RayData");
            public static int TemporalData = Shader.PropertyToID("_TemporalData");
            public static int TemporalData2 = Shader.PropertyToID("_TemporalData2");
            public static int WorldToViewDir = Shader.PropertyToID("_WorldToViewDir");
            public static int PrevInvViewProj = Shader.PropertyToID("_PrevInvViewProj");
            public static int PrevInvViewProjStereo = Shader.PropertyToID("_PrevInvViewProjStereo");
            public static int RadiantCurrVP = Shader.PropertyToID("_RadiantCurrVP");
            public static int RadiantPrevVP = Shader.PropertyToID("_RadiantPrevVP");
            public static int RadiantEditorMV = Shader.PropertyToID("_RadiantEditorMV");
            public static int CompareParams = Shader.PropertyToID("_CompareParams");
            public static int ExtraData = Shader.PropertyToID("_ExtraData");
            public static int ExtraData2 = Shader.PropertyToID("_ExtraData2");
            public static int ExtraData3 = Shader.PropertyToID("_ExtraData3");
            public static int ExtraData4 = Shader.PropertyToID("_ExtraData4");
            public static int ExtraData5 = Shader.PropertyToID("_ExtraData5");
            public static int EmittersPositions = Shader.PropertyToID("_EmittersPositions");
            public static int EmittersBoxMin = Shader.PropertyToID("_EmittersBoxMin");
            public static int EmittersBoxMax = Shader.PropertyToID("_EmittersBoxMax");
            public static int EmittersShapeData = Shader.PropertyToID("_EmittersShapeData");
            public static int EmittersRotation = Shader.PropertyToID("_EmittersRotation");
            public static int EmittersColors = Shader.PropertyToID("_EmittersColors");
            public static int EmittersLayerMasks = Shader.PropertyToID("_EmittersLayerMasks");
            public static int EmittersCount = Shader.PropertyToID("_EmittersCount");
            public static int CameraRenderingLayersTexture = Shader.PropertyToID("_CameraRenderingLayersTexture");
            public static int RSMIntensity = Shader.PropertyToID("_RadiantShadowMapIntensity");
            public static int StencilValue = Shader.PropertyToID("_StencilValue");
            public static int StencilCompareFunction = Shader.PropertyToID("_StencilCompareFunction");
            public static int ProbeData = Shader.PropertyToID("_ProbeData");
            public static int Probe1HDR = Shader.PropertyToID("_Probe1HDR");
            public static int Probe2HDR = Shader.PropertyToID("_Probe2HDR");
            public static int Probe1BoxMin = Shader.PropertyToID("_Probe1BoxMin");
            public static int Probe1BoxMax = Shader.PropertyToID("_Probe1BoxMax");
            public static int Probe1ProbePosition = Shader.PropertyToID("_Probe1ProbePosition");
            public static int Probe2BoxMin = Shader.PropertyToID("_Probe2BoxMin");
            public static int Probe2BoxMax = Shader.PropertyToID("_Probe2BoxMax");
            public static int Probe2ProbePosition = Shader.PropertyToID("_Probe2ProbePosition");
            public static int BoundsXZ = Shader.PropertyToID("_BoundsXZ");
            public static int DebugDepthMultiplier = Shader.PropertyToID("_DebugDepthMultiplier");
            public static int DebugMotionVectorMultiplier = Shader.PropertyToID("_DebugMotionVectorMultiplier");
            public static int NFOTint = Shader.PropertyToID("_NFOTint");
            public static int FallbackDefaultAmbient = Shader.PropertyToID("_FallbackDefaultAmbient");
            public static int AmbientStrippedRT = Shader.PropertyToID("_AmbientStrippedRT");
            public static int LegacyAmbientSubtract = Shader.PropertyToID("_LegacyAmbientSubtract");

            public static int OrganicLightData = Shader.PropertyToID("_OrganicLightData");
            public static int OrganicLightTint = Shader.PropertyToID("_OrganicLightTint");
            public static int OrganicLightOffset = Shader.PropertyToID("_OrganicLightOffset");

            // keywords
            public const string SKW_FORWARD = "_FORWARD";
            public const string SKW_FORWARD_AND_DEFERRED = "_FORWARD_AND_DEFERRED";
            public const string SKW_USES_MULTIPLE_RAYS = "_USES_MULTIPLE_RAYS";
            public const string SKW_REUSE_RAYS = "_REUSE_RAYS";
            public const string SKW_ONE_EXTRA_BOUNCE = "_ONE_EXTRA_BOUNCE";
            public const string SKW_FALLBACK_1_PROBE = "_FALLBACK_1_PROBE";
            public const string SKW_FALLBACK_2_PROBES = "_FALLBACK_2_PROBES";
            public const string SKW_FALLBACK_PROBE_ATLAS = "_FALLBACK_PROBE_ATLAS";
            public const string SKW_VIRTUAL_EMITTERS = "_VIRTUAL_EMITTERS";
            public const string SKW_VIRTUAL_EMITTERS_LAYERS = "_VIRTUAL_EMITTERS_LAYERS";
            public const string SKW_USES_NEAR_FIELD_OBSCURANCE = "_USES_NEAR_FIELD_OBSCURANCE";
            public const string SKW_ORTHO_SUPPORT = "_ORTHO_SUPPORT";
            public const string SKW_DISTANCE_BLENDING = "_DISTANCE_BLENDING";
            public const string SKW_FALLBACK_RSM = "_FALLBACK_RSM";
            public const string SKW_TRANSPARENT_DEPTH_PREPASS = "_TRANSPARENT_DEPTH_PREPASS";
        }

#if UNITY_2023_3_OR_NEWER
        static TextureHandle GetRenderingLayersTexture (UniversalResourceData resourceData, UniversalRenderer renderer, RenderingPath renderingPath) {
            if (renderingPath == RenderingPath.Deferred && renderer != null) {
                DeferredLights deferredLights = renderer.deferredLights;
                if (deferredLights != null && deferredLights.UseRenderingLayers) {
#if UNITY_6000_5_OR_NEWER
                    int renderingLayersIndex = deferredLights.GBufferRenderingLayersIndex;
#else
                    int renderingLayersIndex = deferredLights.GBufferRenderingLayers;
#endif
                    TextureHandle[] gBuffer = resourceData.gBuffer;
                    if (gBuffer != null && renderingLayersIndex >= 0 && renderingLayersIndex < gBuffer.Length && gBuffer[renderingLayersIndex].IsValid()) {
                        return gBuffer[renderingLayersIndex];
                    }
                }
            }

            return resourceData.renderingLayersTexture.IsValid() ? resourceData.renderingLayersTexture : TextureHandle.nullHandle;
        }
#endif

        static Mesh _fullScreenMesh;

        static Mesh fullscreenMesh {
            get {
                if (_fullScreenMesh != null) {
                    return _fullScreenMesh;
                }
                float num = 1f;
                float num2 = 0f;
                Mesh val = new Mesh();
                _fullScreenMesh = val;
                _fullScreenMesh.SetVertices(new List<Vector3> {
            new Vector3 (-1f, -1f, 0f),
            new Vector3 (-1f, 1f, 0f),
            new Vector3 (1f, -1f, 0f),
            new Vector3 (1f, 1f, 0f)
        });
                _fullScreenMesh.SetUVs(0, new List<Vector2> {
            new Vector2 (0f, num2),
            new Vector2 (0f, num),
            new Vector2 (1f, num2),
            new Vector2 (1f, num)
        });
                _fullScreenMesh.SetIndices(new int[6] { 0, 1, 2, 2, 1, 3 }, (MeshTopology)0, 0, false);
                _fullScreenMesh.UploadMeshData(true);
                return _fullScreenMesh;
            }
        }

        partial class RadiantPass : ScriptableRenderPass {

            const string RGI_CBUF_NAME = "RadiantGI";
            const float GOLDEN_RATIO = 0.618033989f;
            const int MAX_EMITTERS = 32;
            const float TEMPORAL_CAMERA_TRANSLATION_RESPONSE = 200f;

            class PerCameraData {
                public Vector3 prevCameraRefPos;
                public Matrix4x4[] prevInvViewProjStereo = new Matrix4x4[2];
                public bool prevInvViewProjValid;
                // Scene view camera motion vectors (editor only)
                public Matrix4x4 prevViewProj;
                public bool prevViewProjValid;
                // Guard against duplicate execution for identical camera state within the same frame.
                public int lastExecutedFrame = -1;
                public Matrix4x4 lastExecutionViewProj;
                public bool lastExecutionViewProjValid;
                // Double-buffered accumulation RTs
                public RenderTexture[] rtAcum = new RenderTexture[2];
                public int rtAcumIndex;
                public RenderTexture rtBounce;
                // Double-buffered ray temporal RTs
                public RenderTexture[] rtRayAcum = new RenderTexture[2];
                public RenderTexture[] rtRaySamples = new RenderTexture[2];
                public int rtRayIndex;
                public int bufferCreationFrame;
                // emitters
                public float emittersSortTime = float.MinValue;
                public Vector3 emittersLastCameraPosition;
                public readonly List<RadiantVirtualEmitter> emittersSorted = new List<RadiantVirtualEmitter>();
            }

            UniversalRenderer renderer;
            static RadiantRenderFeature settings;
            static RenderTextureDescriptor sourceDesc, cameraTargetDesc;
            static readonly Dictionary<Camera, PerCameraData> prevs = new Dictionary<Camera, PerCameraData>();

            static RadiantGlobalIllumination radiant;
            static float goldenRatioAcum;
            static bool usesCompareMode;
            static readonly Func<RadiantVirtualEmitter, RadiantVirtualEmitter, int> emittersSortFunction = EmittersDistanceComparer;
            static Vector3 camPos;
            static Volume[] volumes;
            static Material mat;
            static readonly Vector4 unlimitedBounds = new Vector4(-1e8f, -1e8f, 1e8f, 1e8f);
            static Vector4[] emittersBoxMin, emittersBoxMax, emittersShapeData, emittersRotation, emittersColors, emittersPositions, emittersLayerMasks;

            static readonly Plane[] cameraPlanes = new Plane[6];
            static public RenderTargetIdentifier computedGIRT;
            static public bool computedGIRTValid;

            // SH coefficients for ambient lighting
            static readonly List<SphericalHarmonicsL2> probes = new List<SphericalHarmonicsL2>();
            public static MaterialPropertyBlock probesProps;

            // Cached gradient ambient probe
            static SphericalHarmonicsL2 cachedGradientProbe;
            static bool cachedGradientProbeValid;
            static bool cachedAmbientSettingsValid;
            static AmbientMode cachedAmbientMode;
            static Color cachedAmbientSkyColor;
            static Color cachedAmbientEquatorColor;
            static Color cachedAmbientGroundColor;
            static Color cachedAmbientLightColor;
            static float cachedAmbientIntensity;

            static readonly RenderTargetIdentifier[] rayTemporalMRT = new RenderTargetIdentifier[2];

            public bool Setup (RadiantGlobalIllumination radiant, ScriptableRenderer renderer, RadiantRenderFeature settings, bool isSceneView) {
                if (!radiant.IsActive()) return false;
                RadiantPass.radiant = radiant;

#if UNITY_EDITOR
                if (isSceneView && !radiant.showInSceneView.value) return false;
                if (!Application.isPlaying && !radiant.showInEditMode.value) return false;
#endif

                usesCompareMode = radiant.compareMode.value && !isSceneView;
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox + 2; // just after depth texture and motion vectors

#if UNITY_2022_1_OR_NEWER && !UNITY_2022_3_OR_NEWER
                // when using motion vectors in RG, it needs to execute after motion vector pass which happens BeforeRenderingPostProcessing so we force Radiant to run just before post processing
                // in Unity 2022.3 it was fixed, so motion vectors just follow the depth texture mode pass so we can keep Radiant run before transparent
                int minRenderPassEvent = (int)RenderPassEvent.BeforeRenderingPostProcessing;
                if ((int)renderPassEvent < minRenderPassEvent) {
                    renderPassEvent = (RenderPassEvent)minRenderPassEvent;
                }
#endif
                this.renderer = renderer as UniversalRenderer;
                RadiantPass.settings = settings;
                if (mat == null) {
                    mat = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Kronnect/RadiantGI_URP"));
                    mat.SetTexture(ShaderParams.NoiseTex, Resources.Load<Texture>("RadiantGI/blueNoiseGI128RGB"));
                }
                mat.SetInt(ShaderParams.StencilValue, radiant.stencilValue.value);
                mat.SetInt(ShaderParams.StencilCompareFunction, radiant.stencilCheck.value ? (int)radiant.stencilCompareFunction.value : (int)CompareFunction.Always);

                return true;
            }

            ScriptableRenderPassInput GetRequiredInputs () {
                ScriptableRenderPassInput input = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Motion;
                // Only request Normal input in forward mode - in deferred, use G-buffer normals instead
                if (settings != null && settings.renderingPath != RenderingPath.Deferred) {
                    input |= ScriptableRenderPassInput.Normal;
                }
                return input;
            }

            static bool AreMatricesSimilar(Matrix4x4 a, Matrix4x4 b) {
                const float epsilon = 1e-6f;
                for (int i = 0; i < 16; i++) {
                    if (Mathf.Abs(a[i] - b[i]) > epsilon) {
                        return false;
                    }
                }
                return true;
            }

            readonly PassData passData = new PassData();

            class PassData {
                public CommandBuffer cmd;
                public Camera cam;
                public NativeArray<VisibleReflectionProbe> reflectionProbes;
                public bool usesProbeAtlas;
#if UNITY_2022_2_OR_NEWER
                public RTHandle source;
#else
                public RenderTargetIdentifier source;
#endif
#if UNITY_2023_3_OR_NEWER
                public TextureHandle colorTexture, depthTexture, cameraNormalsTexture, motionVectorTexture, gBuffer0, gBuffer1, gBuffer2;
                public TextureHandle renderingLayersTexture;
#endif
            }

#if UNITY_2023_3_OR_NEWER
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {

                using (var builder = renderGraph.AddUnsafePass<PassData>("Radiant GI RG Pass", out var passData)) {

                    builder.AllowPassCulling(false);

                    ConfigureInput(GetRequiredInputs());

                    UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                    UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                    passData.cam = cameraData.camera;
                    passData.reflectionProbes = renderingData.cullResults.visibleReflectionProbes;
                    sourceDesc = cameraData.cameraTargetDescriptor;
                    sourceDesc.colorFormat = RenderTextureFormat.ARGBHalf;
                    sourceDesc.useMipMap = false;
                    sourceDesc.msaaSamples = 1;
                    sourceDesc.depthBufferBits = 0;
                    cameraTargetDesc = sourceDesc;

                    float downsampling = radiant.downsampling.value;
                    sourceDesc.width = (int)(sourceDesc.width / downsampling);
                    sourceDesc.height = (int)(sourceDesc.height / downsampling);

                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                    passData.colorTexture = resourceData.activeColorTexture;
                    builder.UseTexture(resourceData.activeColorTexture, AccessFlags.ReadWrite);

                    if (settings.renderingPath == RenderingPath.Forward) {
                        builder.UseTexture(resourceData.cameraDepthTexture);
                        passData.depthTexture = resourceData.cameraDepthTexture;
                        builder.UseTexture(resourceData.cameraNormalsTexture);
                        passData.cameraNormalsTexture = resourceData.cameraNormalsTexture;
                    } else if (settings.renderingPath == RenderingPath.Deferred) {
                        if (resourceData.gBuffer[0].IsValid()) {
                        builder.UseTexture(resourceData.gBuffer[0]);
                        passData.gBuffer0 = resourceData.gBuffer[0];
                        }
                        if (resourceData.gBuffer[1].IsValid()) {
                        builder.UseTexture(resourceData.gBuffer[1]);
                        passData.gBuffer1 = resourceData.gBuffer[1];
                        }
                        if (resourceData.gBuffer[2].IsValid()) {
                        builder.UseTexture(resourceData.gBuffer[2]);
                        passData.gBuffer2 = resourceData.gBuffer[2];
                        }
                    } else {
                        // Both mode - use normals texture for forward compatibility
                        builder.UseTexture(resourceData.cameraNormalsTexture);
                        passData.cameraNormalsTexture = resourceData.cameraNormalsTexture;
                    }
                    builder.UseTexture(resourceData.motionVectorColor);
                    passData.motionVectorTexture = resourceData.motionVectorColor;

                    if (settings.useRenderingLayers) {
                        TextureHandle renderingLayersTexture = GetRenderingLayersTexture(resourceData, renderer, settings.renderingPath);
                        if (renderingLayersTexture.IsValid()) {
                            builder.UseTexture(renderingLayersTexture, AccessFlags.Read);
                            passData.renderingLayersTexture = renderingLayersTexture;
                        }
                    }
                    
#if UNITY_6000_1_OR_NEWER
                        bool usesProbeAtlas = false;
                        int renderingMode = (int)renderer.renderingModeActual;
                        bool isClusterLighting = (renderingMode == 2 || renderingMode == 3);
                        if (isClusterLighting) {
                            UniversalLightData lightData = frameData.Get<UniversalLightData>();
                            usesProbeAtlas = lightData.reflectionProbeAtlas;
                        }
                        passData.usesProbeAtlas = usesProbeAtlas;
#else
                        passData.usesProbeAtlas = false;
#endif

                    builder.SetRenderFunc((PassData passData, UnsafeGraphContext context) => {

                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                        if (passData.depthTexture.IsValid()) {
                            cmd.SetGlobalTexture(ShaderParams.CameraDepthTexture, passData.depthTexture);
                        }
                        if (passData.motionVectorTexture.IsValid()) {
                            cmd.SetGlobalTexture(ShaderParams.MotionVectorTexture, passData.motionVectorTexture);
                        }
                        if (settings.renderingPath == RenderingPath.Deferred) {
                            if (passData.gBuffer0.IsValid()) {
                                cmd.SetGlobalTexture(ShaderParams.CameraGBuffer0, passData.gBuffer0);
                            }
                            if (passData.gBuffer1.IsValid()) {
                                cmd.SetGlobalTexture(ShaderParams.CameraGBuffer1, passData.gBuffer1);
                            }
                            if (passData.gBuffer2.IsValid()) {
                                cmd.SetGlobalTexture(ShaderParams.CameraGBuffer2, passData.gBuffer2);
                                // Bind GBuffer2 to _CameraNormalsTexture so LoadSceneNormals can use it
                                cmd.SetGlobalTexture(ShaderParams.CameraNormalsTexture, passData.gBuffer2);
                            }
                        } else if (passData.cameraNormalsTexture.IsValid()) {
                            cmd.SetGlobalTexture(ShaderParams.CameraNormalsTexture, passData.cameraNormalsTexture);
                        }

                        if (passData.renderingLayersTexture.IsValid()) {
                            cmd.SetGlobalTexture(ShaderParams.CameraRenderingLayersTexture, passData.renderingLayersTexture);
                        }

                        passData.source = passData.colorTexture;
                        passData.cmd = cmd;
                        RenderGI(passData);

                    });
                }
            }
#endif

            static SphericalHarmonicsL2 CreateAmbientProbeFromGradient (Color skyColor, Color equatorColor, Color groundColor, float intensity) {
                SphericalHarmonicsL2 probe = new SphericalHarmonicsL2();

                Color linearSky = CoreUtils.ConvertSRGBToActiveColorSpace(skyColor) * intensity;
                Color linearEquator = CoreUtils.ConvertSRGBToActiveColorSpace(equatorColor) * intensity;
                Color linearGround = CoreUtils.ConvertSRGBToActiveColorSpace(groundColor) * intensity;

                probe[0, 0] = (linearSky.r + linearEquator.r + linearGround.r) / 3.0f;
                probe[1, 0] = (linearSky.g + linearEquator.g + linearGround.g) / 3.0f;
                probe[2, 0] = (linearSky.b + linearEquator.b + linearGround.b) / 3.0f;
                probe[0, 2] = (linearSky.r - linearGround.r) * 0.5f;
                probe[1, 2] = (linearSky.g - linearGround.g) * 0.5f;
                probe[2, 2] = (linearSky.b - linearGround.b) * 0.5f;

                return probe;
            }

            static SphericalHarmonicsL2 CreateAmbientProbeFromColor(Color ambientColor, float intensity) {
                Color linearAmbient = CoreUtils.ConvertSRGBToActiveColorSpace(ambientColor) * intensity;
                SphericalHarmonicsL2 probe = new SphericalHarmonicsL2();
                probe[0, 0] = linearAmbient.r;
                probe[1, 0] = linearAmbient.g;
                probe[2, 0] = linearAmbient.b;
                return probe;
            }

            static void RenderGI (PassData passData) {

                computedGIRTValid = false;

#if UNITY_2022_2_OR_NEWER
                RTHandle source = passData.source;
#else
                RenderTargetIdentifier source = passData.source;
#endif

                CommandBuffer cmd = passData.cmd;
                Camera cam = passData.cam;
                camPos = cam.transform.position;

                // Setup SH coefficients for ambient lighting (needed by SampleSH in shader)
                SphericalHarmonicsL2 ambientProbe = RenderSettings.ambientProbe;

                // Detect if ambient probe is available/baked
                bool isAmbientProbeAvailable = IsAmbientProbeValid(ambientProbe);
                bool realtimeAmbientProbe = false;

                // Fallback: If ambientProbe is empty, use cached gradient probe or create it once
                if (!isAmbientProbeAvailable) {
                    bool ambientSettingsChanged = !cachedAmbientSettingsValid ||
                                                  cachedAmbientMode != RenderSettings.ambientMode ||
                                                  cachedAmbientIntensity != RenderSettings.ambientIntensity ||
                                                  cachedAmbientSkyColor != RenderSettings.ambientSkyColor ||
                                                  cachedAmbientEquatorColor != RenderSettings.ambientEquatorColor ||
                                                  cachedAmbientGroundColor != RenderSettings.ambientGroundColor ||
                                                  cachedAmbientLightColor != RenderSettings.ambientLight;

                    if (!cachedGradientProbeValid || ambientSettingsChanged) {
                        if (RenderSettings.ambientMode == AmbientMode.Flat) {
                            cachedGradientProbe = CreateAmbientProbeFromColor(
                                RenderSettings.ambientLight,
                                RenderSettings.ambientIntensity
                            );
                        } else {
                            cachedGradientProbe = CreateAmbientProbeFromGradient(
                                RenderSettings.ambientSkyColor,
                                RenderSettings.ambientEquatorColor,
                                RenderSettings.ambientGroundColor,
                                RenderSettings.ambientIntensity
                            );
                        }
                        cachedGradientProbeValid = true;
                        cachedAmbientSettingsValid = true;
                        cachedAmbientMode = RenderSettings.ambientMode;
                        cachedAmbientIntensity = RenderSettings.ambientIntensity;
                        cachedAmbientSkyColor = RenderSettings.ambientSkyColor;
                        cachedAmbientEquatorColor = RenderSettings.ambientEquatorColor;
                        cachedAmbientGroundColor = RenderSettings.ambientGroundColor;
                        cachedAmbientLightColor = RenderSettings.ambientLight;
                    }
                    ambientProbe = cachedGradientProbe;
                    realtimeAmbientProbe = true;
                }

                if (probesProps == null) probesProps = new MaterialPropertyBlock();
                probes.Clear();
                probes.Add(ambientProbe);
                probesProps.CopySHCoefficientArraysFrom(probes);

                DebugView debugView = radiant.debugView.value;
                bool usesBounce = radiant.rayBounce.value;
                bool usesForward = settings.renderingPath != RenderingPath.Deferred;
                float normalMapInfluence = radiant.normalMapInfluence.value;
                float downsampling = radiant.downsampling.value;
                int currentFrame = GetRenderFrameId();
                bool usesRSM = RadiantShadowMap.installed && radiant.fallbackReflectiveShadowMap.value && radiant.reflectiveShadowMapIntensity.value > 0;
                bool usesEmitters = radiant.virtualEmitters.value;
                float fallbackAmbient = radiant.fallbackAmbient.value;
                float unityAmbientIntensity = radiant.unityAmbientLighting.value;
                bool transparencySupportEnabled = radiant.transparencySupport.value;
                float occlusionIntensity = radiant.occlusionIntensity.value;

                // pass radiant settings to shader
                mat.SetVector(ShaderParams.IndirectData, new Vector4(radiant.indirectIntensity.value, radiant.indirectMaxSourceBrightness.value, radiant.indirectDistanceAttenuation.value, radiant.rayReuse.value));
                mat.SetVector(ShaderParams.RayData, new Vector4(radiant.rayCount.value, radiant.rayMaxLength.value, radiant.rayMaxSamples.value, radiant.thickness.value));

                // some uniforms required by compare render feature so declared as global vectors instead of material properties
                cmd.SetGlobalVector(ShaderParams.ExtraData2, new Vector4(radiant.brightnessThreshold.value, radiant.brightnessMax.value, radiant.saturation.value, radiant.reflectiveShadowMapIntensity.value)); // global because these params are needed by the compare pass

                mat.DisableKeyword(ShaderParams.SKW_FORWARD_AND_DEFERRED);
                mat.DisableKeyword(ShaderParams.SKW_FORWARD);
                if (usesForward) {
                    if (settings.renderingPath == RenderingPath.Both) {
                        mat.EnableKeyword(ShaderParams.SKW_FORWARD_AND_DEFERRED);
                    }
                    else {
                        mat.EnableKeyword(ShaderParams.SKW_FORWARD);
                    }
                }

                if (radiant.rayCount.value > 1) {
                    mat.EnableKeyword(ShaderParams.SKW_USES_MULTIPLE_RAYS);
                }
                else {
                    mat.DisableKeyword(ShaderParams.SKW_USES_MULTIPLE_RAYS);
                }

                float nearFieldObscurance = radiant.nearFieldObscurance.value;
                bool useNFO = nearFieldObscurance > 0;
                cmd.SetGlobalVector(ShaderParams.ExtraData4, new Vector4(
                    useNFO ? radiant.nearFieldObscuranceMaxCameraDistance.value : 0f,
                    useNFO ? (1f - radiant.nearFieldObscuranceOccluderDistance.value) * 10f : 0f,
                    1f - unityAmbientIntensity,
                    realtimeAmbientProbe ? 1f : 0f));
                if (useNFO) {
                    cmd.SetGlobalColor(ShaderParams.NFOTint, radiant.nearFieldObscuranceTintColor.value);
                    mat.EnableKeyword(ShaderParams.SKW_USES_NEAR_FIELD_OBSCURANCE);
                }
                else {
                    mat.DisableKeyword(ShaderParams.SKW_USES_NEAR_FIELD_OBSCURANCE);
                }

                if (cam.orthographic) {
                    mat.EnableKeyword(ShaderParams.SKW_ORTHO_SUPPORT);
                }
                else {
                    mat.DisableKeyword(ShaderParams.SKW_ORTHO_SUPPORT);
                }
                cmd.SetGlobalVector(ShaderParams.ExtraData3, new Vector4(radiant.aoInfluence.value, radiant.nearFieldObscuranceSpread.value * 0.5f, 1f / (radiant.nearCameraAttenuation.value + 0.0001f), nearFieldObscurance));  // global because these params are needed by the compare pass

                // restricts to volume bounds?
                SetupVolumeBounds(cmd);

                // pass reprojection & other raymarch data
                goldenRatioAcum += GOLDEN_RATIO * radiant.rayCount.value;
                goldenRatioAcum %= 5000;
                cmd.SetGlobalVector(ShaderParams.SourceSize, new Vector4(cameraTargetDesc.width, cameraTargetDesc.height, goldenRatioAcum, currentFrame));
                bool hqFiltering = radiant.highQualityFiltering.value;
                float baseBlurSpread = Mathf.Max(radiant.blurSpread.value, sourceDesc.width / 1920f);
                if (hqFiltering) baseBlurSpread *= 2f;
                cmd.SetGlobalVector(ShaderParams.ExtraData, new Vector4(radiant.rayJitter.value, baseBlurSpread, normalMapInfluence, occlusionIntensity));
                Vector4 extraData5 = new Vector4(downsampling, radiant.sourceBrightness.value, radiant.giWeight.value, fallbackAmbient);
                cmd.SetGlobalVector(ShaderParams.ExtraData5, extraData5);

                // wide-kernel blur quality:
                // - Highest enables _BLUR_QUALITY_HIGHEST keyword -> fully unrolled 63-tap loop.
                // - High/Faster share the dynamic-loop variant; the uniform _BlurSampleStep
                //   selects 32 or 16 taps without recompiling.
                if (radiant.blurQuality.value == RadiantGlobalIllumination.BlurQuality.Highest) {
                    mat.EnableKeyword("_BLUR_QUALITY_HIGHEST");
                } else {
                    mat.DisableKeyword("_BLUR_QUALITY_HIGHEST");
                }
                cmd.SetGlobalInt(ShaderParams.BlurSampleStep, (int)radiant.blurQuality.value);

                // pass UNITY_MATRIX_V
                cmd.SetGlobalMatrix(ShaderParams.WorldToViewDir, cam.worldToCameraMatrix);

                // create downscaled depth
                RenderTextureDescriptor downDesc = cameraTargetDesc;
                if (hqFiltering) {
                    downDesc.width = sourceDesc.width;
                    downDesc.height = sourceDesc.height;
                } else {
                    downDesc.width = Mathf.Min(sourceDesc.width, downDesc.width / 2);
                    downDesc.height = Mathf.Min(sourceDesc.height, downDesc.height / 2);
                }

                int downHalfDescWidth = downDesc.width;
                int downHalfDescHeight = downDesc.height;

                // depth buffer handling
                if (transparencySupportEnabled) {
                    bool hasLayerMask = radiant.transparentLayerMask.value != 0;
                    bool hasScriptObjects = transparentSupport.Count > 0;
                    if (hasLayerMask || hasScriptObjects) {
                        mat.EnableKeyword(ShaderParams.SKW_TRANSPARENT_DEPTH_PREPASS);
                    }
                    else {
                        mat.DisableKeyword(ShaderParams.SKW_TRANSPARENT_DEPTH_PREPASS);
                    }
                }
                int downsamplingDepth = 9 - radiant.raytracerAccuracy.value;
                RenderTextureDescriptor rtDownDepth = sourceDesc;
                rtDownDepth.width = Mathf.CeilToInt((float)rtDownDepth.width / downsamplingDepth);
                rtDownDepth.height = Mathf.CeilToInt((float)rtDownDepth.height / downsamplingDepth);
#if UNITY_WEBGL
                    rtDownDepth.colorFormat = RenderTextureFormat.RFloat;
#else
                bool needsFullDepth = cam.orthographic
                    || radiant.depthPrecision.value == RadiantGlobalIllumination.DepthPrecision.Full;
                rtDownDepth.colorFormat = needsFullDepth
                    ? RenderTextureFormat.RFloat
                    : RenderTextureFormat.RHalf;
#endif
                rtDownDepth.sRGB = false;
                GetTemporaryRT(cmd, ShaderParams.DownscaledDepthRT, ref rtDownDepth, FilterMode.Point);
                FullScreenBlit(cmd, ShaderParams.DownscaledDepthRT, Pass.CopyDepth);

                // are we reusing rays?
                if (!prevs.TryGetValue(cam, out PerCameraData frameAcumData)) {
                    prevs[cam] = frameAcumData = new PerCameraData();
                    frameAcumData.bufferCreationFrame = currentFrame;
                }

                Matrix4x4 proj = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
                Matrix4x4 view = cam.worldToCameraMatrix;
                Matrix4x4 currViewProj = proj * view;

                // Only treat as duplicate when frame index and camera matrices match.
                // This avoids false positives during editor repaints/camera pivoting where
                // frame counters may not advance consistently.
                bool isSameFrame = frameAcumData.lastExecutedFrame == currentFrame;
                bool isSameViewProj = frameAcumData.lastExecutionViewProjValid && AreMatricesSimilar(frameAcumData.lastExecutionViewProj, currViewProj);
                bool isDoubleExecution = isSameFrame && isSameViewProj;
                frameAcumData.lastExecutedFrame = currentFrame;
                frameAcumData.lastExecutionViewProj = currViewProj;
                frameAcumData.lastExecutionViewProjValid = true;

                // Compute per-eye current inverse VP. In stereo, head matrices reproject
                // wrongly (cone-shaped disocclusion artifact), so we need the eye's own VP.
                Matrix4x4 currInvViewProj0, currInvViewProj1;
                if (cam.stereoEnabled) {
                    Matrix4x4 projL = GL.GetGPUProjectionMatrix(cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left), true);
                    Matrix4x4 viewL = cam.GetStereoViewMatrix(Camera.StereoscopicEye.Left);
                    currInvViewProj0 = (projL * viewL).inverse;
                    Matrix4x4 projR = GL.GetGPUProjectionMatrix(cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right), true);
                    Matrix4x4 viewR = cam.GetStereoViewMatrix(Camera.StereoscopicEye.Right);
                    currInvViewProj1 = (projR * viewR).inverse;
                } else {
                    currInvViewProj0 = currViewProj.inverse;
                    currInvViewProj1 = currInvViewProj0;
                }

                if (!frameAcumData.prevInvViewProjValid) {
                    frameAcumData.prevInvViewProjStereo[0] = currInvViewProj0;
                    frameAcumData.prevInvViewProjStereo[1] = currInvViewProj1;
                    frameAcumData.prevInvViewProjValid = true;
                }
                cmd.SetGlobalMatrixArray(ShaderParams.PrevInvViewProjStereo, frameAcumData.prevInvViewProjStereo);
                // also keep the legacy single-matrix global for any external shader still reading it
                cmd.SetGlobalMatrix(ShaderParams.PrevInvViewProj, frameAcumData.prevInvViewProjStereo[0]);
                frameAcumData.prevInvViewProjStereo[0] = currInvViewProj0;
                frameAcumData.prevInvViewProjStereo[1] = currInvViewProj1;

                // Scene view camera motion vectors fallback
#if UNITY_EDITOR
                if (cam.cameraType == CameraType.SceneView) {
                    if (!frameAcumData.prevViewProjValid) {
                        frameAcumData.prevViewProj = currViewProj;
                        frameAcumData.prevViewProjValid = true;
                    }
                    cmd.SetGlobalMatrix(ShaderParams.RadiantCurrVP, currViewProj);
                    cmd.SetGlobalMatrix(ShaderParams.RadiantPrevVP, frameAcumData.prevViewProj);
                    cmd.SetGlobalFloat(ShaderParams.RadiantEditorMV, 1f);
                    frameAcumData.prevViewProj = currViewProj;
                } else {
                    cmd.SetGlobalFloat(ShaderParams.RadiantEditorMV, 0f);
                }
#else
                cmd.SetGlobalFloat(ShaderParams.RadiantEditorMV, 0f);
#endif

                // early debug views
                switch (debugView) {
                    case DebugView.Albedo:
                        FullScreenBlit(cmd, source, Pass.Albedo);
                        return;
                    case DebugView.Normals:
                        FullScreenBlit(cmd, source, Pass.Normals);
                        return;
                    case DebugView.Depth:
                        mat.SetFloat(ShaderParams.DebugDepthMultiplier, radiant.debugDepthMultiplier.value);
                        FullScreenBlit(cmd, source, Pass.Depth);
                        return;
                    case DebugView.MotionVectors:
                        mat.SetFloat(ShaderParams.DebugMotionVectorMultiplier, radiant.debugMotionVectorMultiplier.value);
                        FullScreenBlit(cmd, source, Pass.MotionVectors);
                        return;
                    case DebugView.RenderingLayerMask:
                        FullScreenBlit(cmd, source, Pass.RenderingLayerMask);
                        return;
                }
                RenderTexture bounceRT = frameAcumData.rtBounce;

                RenderTargetIdentifier raycastInput = source;
                if (usesBounce) {
                    if (bounceRT != null && (bounceRT.width != cameraTargetDesc.width || bounceRT.height != cameraTargetDesc.height)) {
                        bounceRT.Release();
                        bounceRT = null;
                    }
                    if (bounceRT == null) {
                        bounceRT = new RenderTexture(cameraTargetDesc);
                        bounceRT.Create();
                        frameAcumData.rtBounce = bounceRT;
                        frameAcumData.bufferCreationFrame = currentFrame;
                    }
                    else {
                        if (currentFrame - frameAcumData.bufferCreationFrame > 2) {
                            raycastInput = bounceRT; // only uses bounce rt a few frames after it's created
                        }
                    }
                }
                else if (bounceRT != null) {
                    bounceRT.Release();
                    DestroyImmediate(bounceRT);
                }

                // Ambient stripping pre-pass (only when slider < 1 AND user opted in)
                bool useAmbientStrip = unityAmbientIntensity < 1f && radiant.ambientStripPrePass.value;
                RenderTargetIdentifier composeInput = source;
                if (useAmbientStrip) {
                    GetTemporaryRT(cmd, ShaderParams.AmbientStrippedRT, ref cameraTargetDesc, FilterMode.Point);
                    FullScreenBlit(cmd, source, ShaderParams.AmbientStrippedRT, Pass.AmbientStrip, probesProps);
                    bool bounceProvides = usesBounce && bounceRT != null && currentFrame - frameAcumData.bufferCreationFrame > 2;
                    if (!bounceProvides) {
                        raycastInput = ShaderParams.AmbientStrippedRT;
                    }
                    composeInput = ShaderParams.AmbientStrippedRT;
                }

                // Legacy ambient subtraction is active when slider < 1 AND prepass is OFF.
                // The compose pass branches dynamically on this uniform; no shader variants added.
                bool useLegacyAmbientSubtract = unityAmbientIntensity < 1f && !radiant.ambientStripPrePass.value;
                cmd.SetGlobalFloat(ShaderParams.LegacyAmbientSubtract, useLegacyAmbientSubtract ? 1f : 0f);

                // virtual emitters
                if (usesEmitters) {
                    float now = Time.time;
                    if (emittersForceRefresh) {
                        emittersForceRefresh = false;
                        foreach (PerCameraData cameraData in prevs.Values) {
                            cameraData.emittersSortTime = float.MinValue;
                        }
                    }
                    if (now - frameAcumData.emittersSortTime > 5 || (frameAcumData.emittersLastCameraPosition - camPos).sqrMagnitude > 25) {
                        frameAcumData.emittersSortTime = now;
                        frameAcumData.emittersLastCameraPosition = camPos;
                        SortEmitters();
                        frameAcumData.emittersSorted.Clear();
                        frameAcumData.emittersSorted.AddRange(emitters);
                    }
                    usesEmitters = SetupEmitters(cam, frameAcumData.emittersSorted);
                }
                // _VIRTUAL_EMITTERS and _VIRTUAL_EMITTERS_LAYERS are mutually exclusive (one shared multi_compile slot).
                if (usesEmitters && settings.useRenderingLayers) {
                    mat.DisableKeyword(ShaderParams.SKW_VIRTUAL_EMITTERS);
                    mat.EnableKeyword(ShaderParams.SKW_VIRTUAL_EMITTERS_LAYERS);
                }
                else if (usesEmitters) {
                    mat.EnableKeyword(ShaderParams.SKW_VIRTUAL_EMITTERS);
                    mat.DisableKeyword(ShaderParams.SKW_VIRTUAL_EMITTERS_LAYERS);
                }
                else {
                    mat.DisableKeyword(ShaderParams.SKW_VIRTUAL_EMITTERS);
                    mat.DisableKeyword(ShaderParams.SKW_VIRTUAL_EMITTERS_LAYERS);
                }

                // set the fallback mode
                mat.DisableKeyword(ShaderParams.SKW_REUSE_RAYS);
                mat.DisableKeyword(ShaderParams.SKW_ONE_EXTRA_BOUNCE);
                mat.DisableKeyword(ShaderParams.SKW_FALLBACK_1_PROBE);
                mat.DisableKeyword(ShaderParams.SKW_FALLBACK_2_PROBES);
                mat.DisableKeyword(ShaderParams.SKW_FALLBACK_RSM);
#if UNITY_6000_1_OR_NEWER
                    mat.DisableKeyword(ShaderParams.SKW_FALLBACK_PROBE_ATLAS);
#endif

                // Enable extra bounce keyword when bounce is active (for motion vector compensation)
                if (usesBounce && currentFrame - frameAcumData.bufferCreationFrame > 2) {
                    mat.EnableKeyword(ShaderParams.SKW_ONE_EXTRA_BOUNCE);
                }

                if (radiant.fallbackReflectionProbes.value) {
#if UNITY_6000_1_OR_NEWER
                if (passData.usesProbeAtlas)
                {
                    // Forward+/Deferred+ with probe atlas - use cluster-based sampling
                    // Skip calling SetupProbes() - no manual probe selection needed
                    mat.EnableKeyword(ShaderParams.SKW_FALLBACK_PROBE_ATLAS);
                    cmd.SetGlobalVector(ShaderParams.ProbeData, new Vector4(0, 0, radiant.probesIntensity.value, 0));
                }
                else
#endif
                    {
                        // Traditional Forward/Deferred path or Unity < 6.1
                        if (SetupProbes(cmd, passData.reflectionProbes, out int numProbes)) {
                            mat.EnableKeyword(numProbes == 1 ? ShaderParams.SKW_FALLBACK_1_PROBE : ShaderParams.SKW_FALLBACK_2_PROBES);
                        }
                    }
                }

                if (radiant.fallbackReuseRays.value && currentFrame - frameAcumData.bufferCreationFrame > 2 && radiant.rayReuse.value > 0 && frameAcumData.rtAcum[frameAcumData.rtAcumIndex] != null) {
                    RenderTargetIdentifier reuseRaysPrevRT = new RenderTargetIdentifier(frameAcumData.rtAcum[frameAcumData.rtAcumIndex], 0, CubemapFace.Unknown, -1);
                    cmd.SetGlobalTexture(ShaderParams.PrevResolve, reuseRaysPrevRT);
                    mat.EnableKeyword(ShaderParams.SKW_REUSE_RAYS);
                }

                if (usesRSM) {
                    GetTemporaryRT(cmd, ShaderParams.RSMBuffer, ref downDesc, FilterMode.Bilinear);
                    FullScreenBlit(cmd, ShaderParams.RSMBuffer, Pass.RSM);
                    mat.EnableKeyword(ShaderParams.SKW_FALLBACK_RSM);
                }

                float invIndirectIntensity = radiant.indirectIntensity.value > 0f ? 1f / radiant.indirectIntensity.value : 0f;
                cmd.SetGlobalColor(ShaderParams.FallbackDefaultAmbient, radiant.fallbackDefaultAmbient.value * invIndirectIntensity);

                // raycast & resolve
                GetTemporaryRT(cmd, ShaderParams.ResolveRT, ref sourceDesc, FilterMode.Bilinear);
                FullScreenBlit(cmd, raycastInput, ShaderParams.ResolveRT, Pass.Raycast, probesProps);

                // Double-buffer indices: read from current, write to next
                // On double execution, undo the swap from the first execution to read/write the same buffers
                int rayReadIndex = isDoubleExecution ? (1 - frameAcumData.rtRayIndex) : frameAcumData.rtRayIndex;
                int rayWriteIndex = 1 - rayReadIndex;

                RenderTexture rayRead = frameAcumData.rtRayAcum[rayReadIndex];
                RenderTexture rayWrite = frameAcumData.rtRayAcum[rayWriteIndex];
                RenderTexture raySamplesRead = frameAcumData.rtRaySamples[rayReadIndex];
                RenderTexture raySamplesWrite = frameAcumData.rtRaySamples[rayWriteIndex];

                // Check if buffers need resizing
                bool needsRayResize = (rayRead != null && (rayRead.width != sourceDesc.width || rayRead.height != sourceDesc.height)) ||
                                      (rayWrite != null && (rayWrite.width != sourceDesc.width || rayWrite.height != sourceDesc.height));

                RenderTextureDescriptor raySamplesDesc = sourceDesc;
#if UNITY_WEBGL
                raySamplesDesc.colorFormat = RenderTextureFormat.RFloat;
#else
                raySamplesDesc.colorFormat = RenderTextureFormat.RHalf;
#endif
                raySamplesDesc.sRGB = false;

                bool needsSamplesResize = (raySamplesRead != null && (raySamplesRead.width != sourceDesc.width || raySamplesRead.height != sourceDesc.height)) ||
                                          (raySamplesWrite != null && (raySamplesWrite.width != sourceDesc.width || raySamplesWrite.height != sourceDesc.height));

                // Release and recreate if size changed
                if (needsRayResize) {
                    for (int i = 0; i < 2; i++) {
                        if (frameAcumData.rtRayAcum[i] != null) {
                            frameAcumData.rtRayAcum[i].Release();
                            DestroyImmediate(frameAcumData.rtRayAcum[i]);
                            frameAcumData.rtRayAcum[i] = null;
                        }
                    }
                    frameAcumData.bufferCreationFrame = currentFrame;
                    rayRead = rayWrite = null;
                }
                if (needsSamplesResize) {
                    for (int i = 0; i < 2; i++) {
                        if (frameAcumData.rtRaySamples[i] != null) {
                            frameAcumData.rtRaySamples[i].Release();
                            DestroyImmediate(frameAcumData.rtRaySamples[i]);
                            frameAcumData.rtRaySamples[i] = null;
                        }
                    }
                    raySamplesRead = raySamplesWrite = null;
                }

                // Create double buffers if needed
                for (int i = 0; i < 2; i++) {
                    if (frameAcumData.rtRayAcum[i] == null) {
                        frameAcumData.rtRayAcum[i] = new RenderTexture(sourceDesc);
                        frameAcumData.rtRayAcum[i].Create();
                        frameAcumData.bufferCreationFrame = currentFrame;
                        // Clear new ray accumulation buffer
                        RenderTargetIdentifier initRT = new RenderTargetIdentifier(frameAcumData.rtRayAcum[i], 0, CubemapFace.Unknown, -1);
                        cmd.SetRenderTarget(initRT, 0, CubemapFace.Unknown, -1);
                        cmd.ClearRenderTarget(false, true, Color.clear);
                    }
                    if (frameAcumData.rtRaySamples[i] == null) {
                        frameAcumData.rtRaySamples[i] = new RenderTexture(raySamplesDesc) {
                            filterMode = FilterMode.Point,
                            wrapMode = TextureWrapMode.Clamp
                        };
                        frameAcumData.rtRaySamples[i].Create();
                        // Clear new sample buffer
                        RenderTargetIdentifier initRT = new RenderTargetIdentifier(frameAcumData.rtRaySamples[i], 0, CubemapFace.Unknown, -1);
                        cmd.SetRenderTarget(initRT, 0, CubemapFace.Unknown, -1);
                        cmd.ClearRenderTarget(false, true, Color.clear);
                    }
                }

                // Get current read/write buffers after potential creation
                rayRead = frameAcumData.rtRayAcum[rayReadIndex];
                rayWrite = frameAcumData.rtRayAcum[rayWriteIndex];
                raySamplesRead = frameAcumData.rtRaySamples[rayReadIndex];
                raySamplesWrite = frameAcumData.rtRaySamples[rayWriteIndex];

                RenderTargetIdentifier rayReadRT = new RenderTargetIdentifier(rayRead, 0, CubemapFace.Unknown, -1);
                RenderTargetIdentifier rayWriteRT = new RenderTargetIdentifier(rayWrite, 0, CubemapFace.Unknown, -1);
                RenderTargetIdentifier raySamplesReadRT = new RenderTargetIdentifier(raySamplesRead, 0, CubemapFace.Unknown, -1);
                RenderTargetIdentifier raySamplesWriteRT = new RenderTargetIdentifier(raySamplesWrite, 0, CubemapFace.Unknown, -1);

                cmd.SetGlobalTexture(ShaderParams.RayBufferRT, rayReadRT);
                cmd.SetGlobalTexture(ShaderParams.RayConfidenceRT, raySamplesReadRT);

                bool useTemporalFilter = radiant.temporalFilter.value;

                // On double execution, skip temporal draws - the persistent RTs already have
                // the correct result from the first execution
                if (!isDoubleExecution) {
                    float temporalBlend = 0.95f;
                    float motionScale = radiant.temporalResponseSpeed.value / 1080f;
                    float historyLength = radiant.temporalStabilization.value;
                    Vector3 refPos = camPos + cam.transform.forward;
                    float camTranslationDelta = 0f;

                if (frameAcumData.rtAcum[0] != null) {
                    camTranslationDelta = Vector3.Distance(refPos, frameAcumData.prevCameraRefPos) * TEMPORAL_CAMERA_TRANSLATION_RESPONSE;
                        camTranslationDelta = Mathf.Clamp01(camTranslationDelta);
                    }
                    frameAcumData.prevCameraRefPos = refPos;
                    mat.SetVector(ShaderParams.TemporalData, new Vector4(temporalBlend, motionScale, historyLength, camTranslationDelta));
                    mat.SetVector(ShaderParams.TemporalData2, new Vector4(radiant.darkThreshold.value, radiant.darkThresholdMultiplier.value, 0, 0));

                    if (useTemporalFilter) {
                        // Double buffering: render directly to write buffers (no copy needed)
                        rayTemporalMRT[0] = rayWriteRT;
                        rayTemporalMRT[1] = raySamplesWriteRT;
                        RenderTargetIdentifier mrtDepthSlot = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore
                            ? (RenderTargetIdentifier)rayWriteRT
                            : BuiltinRenderTextureType.None;
                        cmd.SetRenderTarget(rayTemporalMRT, mrtDepthSlot, 0, CubemapFace.Unknown, -1);
                        cmd.SetGlobalTexture(ShaderParams.MainTex, ShaderParams.ResolveRT);
                        cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, mat, 0, (int)Pass.RayAccum);
                        frameAcumData.rtRayIndex = rayWriteIndex;
                    }
                }

                // Prepare NFO
                if (useNFO) {
                    RenderTextureDescriptor nfoDesc = downDesc;
                    nfoDesc.colorFormat = RenderTextureFormat.RHalf;
                    GetTemporaryRT(cmd, ShaderParams.NFO_RT, ref nfoDesc, FilterMode.Bilinear);
                    GetTemporaryRT(cmd, ShaderParams.NFOBlurRT, ref nfoDesc, FilterMode.Bilinear);
                    FullScreenBlit(cmd, ShaderParams.NFOBlurRT, Pass.NFO);
                    FullScreenBlit(cmd, ShaderParams.NFOBlurRT, ShaderParams.NFO_RT, Pass.NFOBlur);
                    cmd.ReleaseTemporaryRT(ShaderParams.NFOBlurRT);
                }

                // downscale & blur
                GetTemporaryRT(cmd, ShaderParams.Downscaled2RT, ref downDesc, FilterMode.Bilinear);
                int lastBlurRT = ShaderParams.Downscaled2RT;

                RenderTargetIdentifier raySource = useTemporalFilter ? (RenderTargetIdentifier)rayWriteRT : new RenderTargetIdentifier(ShaderParams.ResolveRT);
                if (downsampling <= 1f) {
                    GetTemporaryRT(cmd, ShaderParams.Downscaled1RT, ref downDesc, FilterMode.Bilinear);
                    FullScreenBlit(cmd, raySource, ShaderParams.Downscaled1RT, Pass.Copy);
                    FullScreenBlit(cmd, ShaderParams.Downscaled1RT, ShaderParams.Downscaled2RT, Pass.WideFilter);
                }
                else {
                    cmd.SetGlobalVector(ShaderParams.ExtraData, new Vector4(radiant.rayJitter.value, baseBlurSpread * 1.5f, normalMapInfluence, occlusionIntensity));
                    FullScreenBlit(cmd, raySource, ShaderParams.Downscaled2RT, Pass.WideFilter);
                }
                computedGIRT = lastBlurRT;

                // Double-buffer indices for final accumulation
                // On double execution, undo the swap from the first execution
                int acumReadIndex = isDoubleExecution ? (1 - frameAcumData.rtAcumIndex) : frameAcumData.rtAcumIndex;
                int acumWriteIndex = 1 - acumReadIndex;

                RenderTextureDescriptor acumDesc = sourceDesc;
                acumDesc.width = downHalfDescWidth;
                acumDesc.height = downHalfDescHeight;

                // Check if buffers need resizing
                bool needsAcumResize = false;
                for (int i = 0; i < 2; i++) {
                    if (frameAcumData.rtAcum[i] != null &&
                        (frameAcumData.rtAcum[i].width != downHalfDescWidth || frameAcumData.rtAcum[i].height != downHalfDescHeight)) {
                        needsAcumResize = true;
                        break;
                    }
                }

                if (needsAcumResize) {
                    for (int i = 0; i < 2; i++) {
                        if (frameAcumData.rtAcum[i] != null) {
                            frameAcumData.rtAcum[i].Release();
                            DestroyImmediate(frameAcumData.rtAcum[i]);
                            frameAcumData.rtAcum[i] = null;
                        }
                    }
                    frameAcumData.bufferCreationFrame = currentFrame;
                }

                Pass acumPass = Pass.TemporalAccum;
                bool isFirstFrame = false;

                // Create double buffers if needed
                for (int i = 0; i < 2; i++) {
                    if (frameAcumData.rtAcum[i] == null) {
                        frameAcumData.rtAcum[i] = new RenderTexture(acumDesc);
                        frameAcumData.rtAcum[i].Create();
                        // Clear new accumulation buffer
                        RenderTargetIdentifier initRT = new RenderTargetIdentifier(frameAcumData.rtAcum[i], 0, CubemapFace.Unknown, -1);
                        cmd.SetRenderTarget(initRT, 0, CubemapFace.Unknown, -1);
                        cmd.ClearRenderTarget(false, true, Color.clear);
                        if (i == 0) {
                            frameAcumData.prevCameraRefPos = camPos + cam.transform.forward;
                            frameAcumData.bufferCreationFrame = currentFrame;
                            isFirstFrame = true;
                        }
                    }
                }

                if (isFirstFrame) {
                    acumPass = Pass.Copy;
                }

                RenderTexture acumRead = frameAcumData.rtAcum[acumReadIndex];
                RenderTexture acumWrite = frameAcumData.rtAcum[acumWriteIndex];
                RenderTargetIdentifier acumReadRT = new RenderTargetIdentifier(acumRead, 0, CubemapFace.Unknown, -1);
                RenderTargetIdentifier acumWriteRT = new RenderTargetIdentifier(acumWrite, 0, CubemapFace.Unknown, -1);

                // Double buffering: read from previous, write directly to next (no copy needed)
                // On double execution, skip temporal draw - persistent RT has correct result
                if (!isDoubleExecution) {
                    cmd.SetGlobalTexture(ShaderParams.PrevResolve, acumReadRT);
                    FullScreenBlit(cmd, computedGIRT, acumWriteRT, acumPass);

                    // Swap buffer index for next frame
                    frameAcumData.rtAcumIndex = acumWriteIndex;
                }
                computedGIRT = acumWriteRT;
                computedGIRTValid = true;

                // Expose GI texture globally for transparent shaders if transparency support is enabled
                if (transparencySupportEnabled) {
                    cmd.SetGlobalTexture(ShaderParams.RadiantGITexture, computedGIRT);
                }

                // Prepare output blending
                GetTemporaryRT(cmd, ShaderParams.InputRT, ref cameraTargetDesc, FilterMode.Point);
                FullScreenBlit(cmd, composeInput, ShaderParams.InputRT, Pass.CopyExact);

                if (usesCompareMode) {
                    GetTemporaryRT(cmd, ShaderParams.CompareTex, ref cameraTargetDesc, FilterMode.Point); // needed by the compare pass
                    if (usesBounce) {
                        FullScreenBlit(cmd, computedGIRT, bounceRT, Pass.Compose, probesProps);
                        FullScreenBlit(cmd, bounceRT, ShaderParams.CompareTex, Pass.CopyExact);
                    }
                }
                else if (usesBounce) {
                    FullScreenBlit(cmd, computedGIRT, bounceRT, Pass.Compose, probesProps);
                    FullScreenBlit(cmd, bounceRT, source, Pass.CopyExact);
                }
                else {
                    FullScreenBlit(cmd, computedGIRT, source, Pass.Compose, probesProps);
                }

                // Debug views that require all buffers to be generated
                switch (debugView) {
                    case DebugView.Downscaled:
                        FullScreenBlit(cmd, ShaderParams.Downscaled2RT, source, Pass.CopyExact);
                        return;
                    case DebugView.Raycast:
                        FullScreenBlit(cmd, ShaderParams.ResolveRT, source, Pass.CopyExact);
                        return;
                    case DebugView.RaycastAccumulated:
                        FullScreenBlit(cmd, rayWriteRT, source, Pass.CopyExact);
                        return;
                    case DebugView.ReflectiveShadowMap:
                        if (usesRSM) {
                            FullScreenBlit(cmd, source, Pass.RSM);
                        }
                        return;
                    case DebugView.TemporalAccumulationBuffer:
                        FullScreenBlit(cmd, computedGIRT, source, Pass.CopyExact);
                        return;
                    case DebugView.FinalGI:
                        FullScreenBlit(cmd, computedGIRT, source, Pass.FinalGIDebug);
                        return;
                }
            }


            static void GetTemporaryRT (CommandBuffer cmd, int nameID, ref RenderTextureDescriptor desc, FilterMode filterMode) {
                if (desc.width < 0) desc.width = 1;
                if (desc.height < 0) desc.height = 1;
                cmd.GetTemporaryRT(nameID, desc, filterMode);
                cmd.SetGlobalTexture(nameID, nameID);
            }

            static void FullScreenBlit (CommandBuffer cmd, RenderTargetIdentifier destination, Pass pass) {
                cmd.SetRenderTarget(destination, 0, CubemapFace.Unknown, -1);
                cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, mat, 0, (int)pass);
            }

            static void FullScreenBlit (CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, Pass pass) {
                cmd.SetRenderTarget(destination, 0, CubemapFace.Unknown, -1);
                cmd.SetGlobalTexture(ShaderParams.MainTex, source);
                cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, mat, 0, (int)pass);
            }

            static void FullScreenBlit (CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, Pass pass, MaterialPropertyBlock props) {
                cmd.SetRenderTarget(destination, 0, CubemapFace.Unknown, -1);
                cmd.SetGlobalTexture(ShaderParams.MainTex, source);
                cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, mat, 0, (int)pass, props);
            }
            static float CalculateProbeWeight (Vector3 wpos, Vector3 probeBoxMin, Vector3 probeBoxMax, float blendDistance) {
                Vector3 weightDir = Vector3.Min(wpos - probeBoxMin, probeBoxMax - wpos) / blendDistance;
                return Mathf.Clamp01(Mathf.Min(weightDir.x, Mathf.Min(weightDir.y, weightDir.z)));
            }


            static bool SetupProbes (CommandBuffer cmd, NativeArray<VisibleReflectionProbe> visibleProbes, out int numProbes) {

                numProbes = PickNearProbes(visibleProbes, out ReflectionProbe probe1, out ReflectionProbe probe2);
                if (numProbes == 0) return false;
                if (!probe1.bounds.Contains(camPos)) return false;
                if (numProbes >= 2 && !probe2.bounds.Contains(camPos)) numProbes = 1;

                float probe1Weight = 0, probe2Weight = 0;
                if (numProbes >= 1) {
                    cmd.SetGlobalTexture(ShaderParams.Probe1Cube, probe1.texture);
                    cmd.SetGlobalVector(ShaderParams.Probe1HDR, probe1.textureHDRDecodeValues);
                    Bounds probe1Bounds = probe1.bounds;
                    probe1Weight = CalculateProbeWeight(camPos, probe1Bounds.min, probe1Bounds.max, probe1.blendDistance);
                    if (probe1.boxProjection) {
                        Vector3 probe1Position = probe1.transform.position;
                        cmd.SetGlobalVector(ShaderParams.Probe1BoxMin, probe1Bounds.min);
                        cmd.SetGlobalVector(ShaderParams.Probe1BoxMax, probe1Bounds.max);
                        cmd.SetGlobalVector(ShaderParams.Probe1ProbePosition, new Vector4(probe1Position.x, probe1Position.y, probe1Position.z, 1));
                    }
                    else {
                        cmd.SetGlobalVector(ShaderParams.Probe1ProbePosition, Vector4.zero);
                    }
                }
                if (numProbes >= 2) {
                    cmd.SetGlobalTexture(ShaderParams.Probe2Cube, probe2.texture);
                    cmd.SetGlobalVector(ShaderParams.Probe2HDR, probe2.textureHDRDecodeValues);
                    Bounds probe2Bounds = probe2.bounds;
                    probe2Weight = CalculateProbeWeight(camPos, probe2Bounds.min, probe2Bounds.max, probe2.blendDistance);
                    if (probe2.boxProjection) {
                        Vector3 probe2Position = probe2.transform.position;
                        cmd.SetGlobalVector(ShaderParams.Probe2BoxMin, probe2Bounds.min);
                        cmd.SetGlobalVector(ShaderParams.Probe2BoxMax, probe2Bounds.max);
                        cmd.SetGlobalVector(ShaderParams.Probe2ProbePosition, new Vector4(probe2Position.x, probe2Position.y, probe2Position.z, 1));
                    }
                    else {
                        cmd.SetGlobalVector(ShaderParams.Probe2ProbePosition, Vector4.zero);
                    }
                }
                float probesIntensity = radiant.probesIntensity.value;
                cmd.SetGlobalVector(ShaderParams.ProbeData, new Vector4(probe1Weight * probesIntensity, probe2Weight * probesIntensity, probesIntensity, 0));

                return true;
            }

            static int PickNearProbes (NativeArray<VisibleReflectionProbe> visibleProbes, out ReflectionProbe probe1, out ReflectionProbe probe2) {
                probe1 = probe2 = null;

                int probesCount = visibleProbes.Length;
                if (probesCount == 0) return 0;

                float probe1Value = float.MaxValue;
                float probe2Value = float.MaxValue;

                for (int k = 0; k < probesCount; k++) {
                    ReflectionProbe probe = visibleProbes[k].reflectionProbe;
                    if (probe == null || probe.texture == null) continue;
                    float probeValue = ComputeProbeValue(camPos, probe);
                    if (probeValue < probe2Value) {
                        probe2 = probe;
                        probe2Value = probeValue;
                        if (probe2Value < probe1Value) {
                            // swap probe1 & probe2
                            float tempValue = probe1Value;
                            ReflectionProbe tempProbe = probe1;
                            probe1 = probe2;
                            probe1Value = probe2Value;
                            probe2 = tempProbe;
                            probe2Value = tempValue;
                        }
                    }
                }

                if (probe1 == null) return 0;
                if (probe2 == null) return 1;
                return 2;
            }

            static float ComputeProbeValue (Vector3 camPos, ReflectionProbe probe) {
                Vector3 probePos = probe.transform.position;
                float d = (probePos - camPos).sqrMagnitude * (probe.importance + 1) * 1000;
                if (!probe.bounds.Contains(camPos)) d += 100000;
                return d;
            }

            static void SetupVolumeBounds (CommandBuffer cmd) {
                if (!radiant.limitToVolumeBounds.value) {
                    cmd.SetGlobalVector(ShaderParams.BoundsXZ, unlimitedBounds);
                    return;
                }
                if (volumes == null) {
                    volumes = VolumeManager.instance.GetVolumes(-1);
                }
                int volumeCount = volumes.Length;
                for (int k = 0; k < volumeCount; k++) {
                    Volume volume = volumes[k];
                    if (volume == null) continue;
                    List<Collider> colliders = volume.colliders;
                    int colliderCount = colliders.Count;
                    for (int j = 0; j < colliderCount; j++) {
                        Collider collider = colliders[j];
                        if (collider == null) continue;
                        Bounds bounds = collider.bounds;
                        if (collider.bounds.Contains(camPos) && volume.sharedProfile.Has<RadiantGlobalIllumination>()) {
                            Vector4 effectBounds = new Vector4(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z);
                            cmd.SetGlobalVector(ShaderParams.BoundsXZ, effectBounds);
                            return;
                        }
                    }
                }
            }

            static bool SetupEmitters (Camera cam, List<RadiantVirtualEmitter> emitters) {
                // copy emitters data
                if (emittersBoxMax == null || emittersBoxMax.Length != MAX_EMITTERS || emittersShapeData == null || emittersShapeData.Length != MAX_EMITTERS || emittersLayerMasks == null || emittersLayerMasks.Length != MAX_EMITTERS) {
                    emittersBoxMax = new Vector4[MAX_EMITTERS];
                    emittersBoxMin = new Vector4[MAX_EMITTERS];
                    emittersShapeData = new Vector4[MAX_EMITTERS];
                    emittersRotation = new Vector4[MAX_EMITTERS];
                    emittersColors = new Vector4[MAX_EMITTERS];
                    emittersPositions = new Vector4[MAX_EMITTERS];
                    emittersLayerMasks = new Vector4[MAX_EMITTERS];
                }
                int emittersCount = 0;

                const int EMITTERS_BUDGET = 150; // max number of emitters to be processed per frame
                int emittersMax = Mathf.Min(EMITTERS_BUDGET, emitters.Count);

                GeometryUtility.CalculateFrustumPlanes(cam, cameraPlanes);

                for (int k = 0; k < emittersMax; k++) {
                    RadiantVirtualEmitter emitter = emitters[k];

                    // Cull emitters

                    // disabled emitter?
                    if (emitter == null || !emitter.isActiveAndEnabled) continue;

                    // emitter with no intensity or range?
                    if (emitter.intensity <= 0 || emitter.range <= 0) continue;

                    // emitter bounds out of camera frustum?
                    Bounds emitterBounds = emitter.GetBounds();
                    if (!GeometryUtility.TestPlanesAABB(cameraPlanes, emitterBounds)) continue;

                    // emitter with black color (nothing to inject)?
                    Vector4 colorAndRange = emitter.GetGIColorAndRange();
                    if (emitter.fadeDistance > 0) {
                        float fade = ComputeVolumeFade(emitterBounds, emitter.fadeDistance);
                        colorAndRange.x *= fade;
                        colorAndRange.y *= fade;
                        colorAndRange.z *= fade;
                    }
                    if (colorAndRange.x == 0 && colorAndRange.y == 0 && colorAndRange.z == 0) continue;

                    // add emitter
                    Vector3 emitterPosition = emitter.transform.position;
                    float emitterShape = emitter.shape == RadiantVirtualEmitter.EmitterShape.Box ? 1f : 0f;
                    emittersPositions[emittersCount] = emitterPosition;

                    emittersColors[emittersCount] = colorAndRange;

                    Vector3 boxMin = emitterBounds.min;
                    Vector3 boxMax = emitterBounds.max;
                    Vector3 shapeExtents = Vector3.zero;
                    Quaternion invRot = Quaternion.identity;
                    if (emitter.shape == RadiantVirtualEmitter.EmitterShape.Box) {
                        shapeExtents = emitter.shapeSize * 0.5f;
                        // Inverse rotation = conjugate for unit quaternions; used in shader to enter local box space.
                        Quaternion rot = emitter.transform.rotation;
                        invRot = new Quaternion(-rot.x, -rot.y, -rot.z, rot.w);

                        // Expand the cull AABB so it always encloses the rotated shape + range. Keeps the SDF cull
                        // correct without forcing the user to resize the AOI box manually after rotating the emitter.
                        Vector3 worldShapeExt = emitter.GetRotatedShapeWorldExtents();
                        float r = colorAndRange.w;
                        Vector3 needMin = emitterPosition - worldShapeExt - new Vector3(r, r, r);
                        Vector3 needMax = emitterPosition + worldShapeExt + new Vector3(r, r, r);
                        boxMin = Vector3.Min(boxMin, needMin);
                        boxMax = Vector3.Max(boxMax, needMax);
                    }

                    float lightRangeSqr = colorAndRange.w * colorAndRange.w;
                    // Commented out for future versions if needed
                    //float fadeStartDistanceSqr = 0.8f * 0.8f * lightRangeSqr;
                    //float fadeRangeSqr = (fadeStartDistanceSqr - lightRangeSqr);
                    //float oneOverFadeRangeSqr = 1.0f / fadeRangeSqr;
                    //float lightRangeSqrOverFadeRangeSqr = -lightRangeSqr / fadeRangeSqr;
                    float oneOverLightRangeSqr = 1.0f / Mathf.Max(0.0001f, lightRangeSqr);

                    float pointAttenX = oneOverLightRangeSqr;
                    //float pointAttenY = lightRangeSqrOverFadeRangeSqr;

                    emittersBoxMin[emittersCount] = new Vector4(boxMin.x, boxMin.y, boxMin.z, pointAttenX);
                    emittersBoxMax[emittersCount] = new Vector4(boxMax.x, boxMax.y, boxMax.z, 0f);
                    emittersShapeData[emittersCount] = new Vector4(shapeExtents.x, shapeExtents.y, shapeExtents.z, emitterShape);
                    emittersRotation[emittersCount] = new Vector4(invRot.x, invRot.y, invRot.z, invRot.w);
                    emittersLayerMasks[emittersCount] = PackRenderingLayerMask(emitter.renderingLayerMask);

                    emittersCount++;
                    if (emittersCount >= MAX_EMITTERS) break;
                }

                if (emittersCount == 0) return false;

                Shader.SetGlobalVectorArray(ShaderParams.EmittersPositions, emittersPositions);
                Shader.SetGlobalVectorArray(ShaderParams.EmittersBoxMin, emittersBoxMin);
                Shader.SetGlobalVectorArray(ShaderParams.EmittersBoxMax, emittersBoxMax);
                Shader.SetGlobalVectorArray(ShaderParams.EmittersShapeData, emittersShapeData);
                Shader.SetGlobalVectorArray(ShaderParams.EmittersRotation, emittersRotation);
                Shader.SetGlobalVectorArray(ShaderParams.EmittersColors, emittersColors);
                Shader.SetGlobalVectorArray(ShaderParams.EmittersLayerMasks, emittersLayerMasks);
                Shader.SetGlobalInt(ShaderParams.EmittersCount, emittersCount);

                return true;
            }

            static Vector4 PackRenderingLayerMask (int renderingLayerMask) {
                uint mask = unchecked((uint)renderingLayerMask);
                return new Vector4(mask & 0xFFFFu, (mask >> 16) & 0xFFFFu, 0f, 0f);
            }

            static void SortEmitters () {
                emitters.Sort((p1, p2) => emittersSortFunction(p1, p2));
            }

            static int EmittersDistanceComparer (RadiantVirtualEmitter p1, RadiantVirtualEmitter p2) {
                Vector3 p1Pos = p1.transform.position;
                Vector3 p2Pos = p2.transform.position;
                float d1 = (p1Pos - camPos).sqrMagnitude;
                float d2 = (p2Pos - camPos).sqrMagnitude;
                Bounds p1bounds = p1.GetBounds();
                Bounds p2bounds = p2.GetBounds();
                if (!p1bounds.Contains(camPos)) d1 += 100000;
                if (!p2bounds.Contains(camPos)) d2 += 100000;
                if (d1 < d2) return -1; else if (d1 > d2) return 1;
                return 0;
            }

            static float ComputeVolumeFade (Bounds emitterBounds, float fadeDistance) {
                Vector3 diff = emitterBounds.center - camPos;
                diff.x = diff.x < 0 ? -diff.x : diff.x;
                diff.y = diff.y < 0 ? -diff.y : diff.y;
                diff.z = diff.z < 0 ? -diff.z : diff.z;
                Vector3 extents = emitterBounds.extents;
                Vector3 gap = diff - extents;
                float maxDiff = gap.x > gap.y ? gap.x : gap.y;
                maxDiff = maxDiff > gap.z ? maxDiff : gap.z;
                fadeDistance += 0.0001f;
                float t = 1f - Mathf.Clamp01(maxDiff / fadeDistance);
                return t;
            }


            public void Cleanup () {
                CoreUtils.Destroy(mat);
                if (prevs != null) {
                    foreach (PerCameraData fad in prevs.Values) {
                        // Dispose double-buffered accumulation RTs
                        for (int i = 0; i < 2; i++) {
                            if (fad.rtAcum[i] != null) {
                                fad.rtAcum[i].Release();
                                DestroyImmediate(fad.rtAcum[i]);
                                fad.rtAcum[i] = null;
                            }
                            if (fad.rtRayAcum[i] != null) {
                                fad.rtRayAcum[i].Release();
                                DestroyImmediate(fad.rtRayAcum[i]);
                                fad.rtRayAcum[i] = null;
                            }
                            if (fad.rtRaySamples[i] != null) {
                                fad.rtRaySamples[i].Release();
                                DestroyImmediate(fad.rtRaySamples[i]);
                                fad.rtRaySamples[i] = null;
                            }
                        }
                        if (fad.rtBounce != null) {
                            fad.rtBounce.Release();
                            DestroyImmediate(fad.rtBounce);
                        }
                    }
                    prevs.Clear();
                }
                goldenRatioAcum = 0f;
                volumes = null;
                radiant = null;
                settings = null;
                cachedGradientProbeValid = false;
                cachedAmbientSettingsValid = false;
            }
        }


        partial class RadiantComparePass : ScriptableRenderPass {

            const string RGI_CBUF_NAME = "RadiantGICompare";
            static Material mat;
            static RadiantGlobalIllumination radiant;
            static RadiantRenderFeature settings;
            static UniversalRenderer renderer;

            class PassData {
                public CommandBuffer cmd;
#if UNITY_2022_2_OR_NEWER
                public RTHandle source, sourceDepth;
#else
                public RenderTargetIdentifier source, sourceDepth;
#endif
#if UNITY_2023_3_OR_NEWER
                public TextureHandle colorTexture, depthTexture, renderingLayersTexture;
#endif
                public RenderTextureDescriptor cameraTargetDesc;
            }

            readonly PassData passData = new PassData();

            public bool Setup (ScriptableRenderer renderer, RadiantRenderFeature settings) {

                radiant = VolumeManager.instance.stack.GetComponent<RadiantGlobalIllumination>();
                if (radiant == null || !radiant.IsActive() || radiant.debugView.value != DebugView.None) return false;

#if UNITY_EDITOR
                if (!Application.isPlaying && !radiant.showInEditMode.value) return false;
#endif

                if (!radiant.compareMode.value) return false;

                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing + 1;
                RadiantComparePass.settings = settings;
                RadiantComparePass.renderer = renderer as UniversalRenderer;
                if (mat == null) {
                    mat = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Kronnect/RadiantGI_URP"));
                }
                return true;
            }

#if UNITY_2023_3_OR_NEWER
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {

                using (var builder = renderGraph.AddUnsafePass<PassData>("Radiant GI Compare RG Pass", out var passData)) {

                    builder.AllowPassCulling(false);

                    UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                    passData.colorTexture = resourceData.activeColorTexture;
                    passData.depthTexture = resourceData.activeDepthTexture;
                    if (settings.useRenderingLayers) {
                        passData.renderingLayersTexture = GetRenderingLayersTexture(resourceData, renderer, settings.renderingPath);
                    }

                    RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                    desc.useMipMap = false;
                    desc.msaaSamples = 1;
                    desc.depthBufferBits = 0;
                    passData.cameraTargetDesc = desc;

                    builder.UseTexture(resourceData.activeColorTexture, AccessFlags.ReadWrite);
                    builder.UseTexture(resourceData.activeDepthTexture, AccessFlags.Read);
                    if (passData.renderingLayersTexture.IsValid()) {
                        builder.UseTexture(passData.renderingLayersTexture, AccessFlags.Read);
                    }

                    builder.SetRenderFunc((PassData passData, UnsafeGraphContext context) => {

                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        if (passData.renderingLayersTexture.IsValid()) {
                            cmd.SetGlobalTexture(ShaderParams.CameraRenderingLayersTexture, passData.renderingLayersTexture);
                        }
                        passData.cmd = cmd;
                        passData.source = passData.colorTexture;
                        passData.sourceDepth = passData.depthTexture;
                        ExecutePass(passData);
                    });
                }
            }


#endif

            static void ExecutePass (PassData passData) {

                if (!RadiantPass.computedGIRTValid) return;

                mat.DisableKeyword(ShaderParams.SKW_FORWARD_AND_DEFERRED);
                mat.DisableKeyword(ShaderParams.SKW_FORWARD);
                if (settings.renderingPath == RenderingPath.Both) {
                    mat.EnableKeyword(ShaderParams.SKW_FORWARD_AND_DEFERRED);
                }
                else if (settings.renderingPath == RenderingPath.Forward) {
                    mat.EnableKeyword(ShaderParams.SKW_FORWARD);
                }

                // _VIRTUAL_EMITTERS and _VIRTUAL_EMITTERS_LAYERS are mutually exclusive (one shared multi_compile slot).
                if (radiant.virtualEmitters.value && settings.useRenderingLayers) {
                    mat.DisableKeyword(ShaderParams.SKW_VIRTUAL_EMITTERS);
                    mat.EnableKeyword(ShaderParams.SKW_VIRTUAL_EMITTERS_LAYERS);
                }
                else if (radiant.virtualEmitters.value) {
                    mat.EnableKeyword(ShaderParams.SKW_VIRTUAL_EMITTERS);
                    mat.DisableKeyword(ShaderParams.SKW_VIRTUAL_EMITTERS_LAYERS);
                }
                else {
                    mat.DisableKeyword(ShaderParams.SKW_VIRTUAL_EMITTERS);
                    mat.DisableKeyword(ShaderParams.SKW_VIRTUAL_EMITTERS_LAYERS);
                }

                float nearFieldObscurance = radiant.nearFieldObscurance.value;
                if (nearFieldObscurance > 0) {
                    mat.EnableKeyword(ShaderParams.SKW_USES_NEAR_FIELD_OBSCURANCE);
                }
                else {
                    mat.DisableKeyword(ShaderParams.SKW_USES_NEAR_FIELD_OBSCURANCE);
                }

                mat.SetVector(ShaderParams.IndirectData, new Vector4(radiant.indirectIntensity.value, radiant.indirectMaxSourceBrightness.value, radiant.indirectDistanceAttenuation.value, radiant.rayReuse.value));

                float angle = radiant.compareLineAngle.value;
                mat.SetVector(ShaderParams.CompareParams, new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), radiant.compareSameSide.value ? radiant.comparePanning.value : -10, radiant.compareLineWidth.value));
                mat.SetInt(ShaderParams.StencilValue, radiant.stencilValue.value);
                mat.SetInt(ShaderParams.StencilCompareFunction, radiant.stencilCheck.value ? (int)radiant.stencilCompareFunction.value : (int)CompareFunction.Always);

                CommandBuffer cmd = passData.cmd;

                RenderTextureDescriptor desc = passData.cameraTargetDesc;
                if (desc.width < 1) desc.width = 1;
                if (desc.height < 1) desc.height = 1;
                cmd.GetTemporaryRT(ShaderParams.InputRT, desc, FilterMode.Point);
                cmd.SetGlobalTexture(ShaderParams.InputRT, ShaderParams.InputRT);
                cmd.GetTemporaryRT(ShaderParams.CompareTex, desc, FilterMode.Point);
                cmd.SetGlobalTexture(ShaderParams.CompareTex, ShaderParams.CompareTex);

                FullScreenBlit(cmd, passData.source, ShaderParams.InputRT, Pass.CopyExact); // include transparent objects in the original compare texture
                FullScreenBlit(cmd, RadiantPass.computedGIRT, ShaderParams.CompareTex, Pass.Compose, RadiantPass.probesProps); // add gi
                FullScreenBlit(cmd, ShaderParams.InputRT, passData.source, Pass.Compare);    // render the split

                cmd.ReleaseTemporaryRT(ShaderParams.InputRT);
                cmd.ReleaseTemporaryRT(ShaderParams.CompareTex);
            }

            static void FullScreenBlit (CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, Pass pass) {
                cmd.SetRenderTarget(destination, 0, CubemapFace.Unknown, -1);
                cmd.SetGlobalTexture(ShaderParams.MainTex, source);
                cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, mat, 0, (int)pass);
            }

            static void FullScreenBlit (CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, Pass pass, MaterialPropertyBlock props) {
                cmd.SetRenderTarget(destination, 0, CubemapFace.Unknown, -1);
                cmd.SetGlobalTexture(ShaderParams.MainTex, source);
                cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, mat, 0, (int)pass, props);
            }

            public void Cleanup () {
                CoreUtils.Destroy(mat);
            }
        }



        partial class RadiantOrganicLightPass : ScriptableRenderPass {

            enum Pass {
                OrganicLight = 0
            }

            const string m_strProfilerTag = "Radiant GI Organic Light";

            static Material mat;
            DeferredLights m_DeferredLights;

            Texture2D noiseTex;
            Vector3 offset;

            public bool Setup (RadiantGlobalIllumination radiant, ScriptableRenderer renderer, bool isSceneView) {

                if (radiant.organicLight.value <= 0) return false;

#if UNITY_EDITOR
                if (isSceneView && !radiant.showInSceneView.value) return false;
                if (!Application.isPlaying && !radiant.showInEditMode.value) return false;
#endif

                DeferredLights deferredLights = ((UniversalRenderer)renderer).deferredLights;
                if (deferredLights == null) return false;

                renderPassEvent = RenderPassEvent.AfterRenderingGbuffer;
                m_DeferredLights = deferredLights;

                if (mat == null) {
                    mat = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Kronnect/RadiantGIOrganicLight"));
                }

                if (noiseTex == null) {
                    noiseTex = Resources.Load<Texture2D>("RadiantGI/NoiseTex");
                }

                mat.SetTexture(ShaderParams.NoiseTex, noiseTex);
                mat.SetVector(ShaderParams.OrganicLightData, new Vector4(1.001f - radiant.organicLightSpread.value, radiant.organicLight.value, radiant.organicLightThreshold.value, radiant.organicLightNormalsInfluence.value));
                mat.SetColor(ShaderParams.OrganicLightTint, radiant.organicLightTintColor.value);
                offset += radiant.organicLightAnimationSpeed.value * Time.deltaTime;
                offset.x %= 10000f;
                offset.y %= 10000f;
                offset.z %= 10000f;
                mat.SetVector(ShaderParams.OrganicLightOffset, offset);

                if (radiant.organicLightDistanceScaling.value) {
                    mat.EnableKeyword(ShaderParams.SKW_DISTANCE_BLENDING);
                }
                else {
                    mat.DisableKeyword(ShaderParams.SKW_DISTANCE_BLENDING);
                }
                return true;
            }



#if !UNITY_6000_4_OR_NEWER
#if UNITY_2022_1_OR_NEWER
            RTHandle GetAlbedoFromGbuffer () {
                return m_DeferredLights.GbufferAttachments[m_DeferredLights.GBufferAlbedoIndex];
            }
#else
            RenderTargetIdentifier GetAlbedoFromGbuffer () {
                return m_DeferredLights.GbufferAttachmentIdentifiers[m_DeferredLights.GBufferAlbedoIndex];
            }
#endif
#endif

#if UNITY_2023_3_OR_NEWER

            class PassData {
                public TextureHandle depthTexture;
                public TextureHandle normalsTexture;
                public TextureHandle gBuffer0;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                
                if (!resourceData.gBuffer[0].IsValid()) return;

                using (var builder = renderGraph.AddUnsafePass<PassData>(m_strProfilerTag, out var passData)) {

                    passData.depthTexture = resourceData.activeDepthTexture;
                    passData.normalsTexture = resourceData.cameraNormalsTexture;
                    passData.gBuffer0 = resourceData.gBuffer[0];

                    builder.UseTexture(resourceData.gBuffer[0], AccessFlags.ReadWrite);
                    builder.UseTexture(resourceData.activeDepthTexture, AccessFlags.Read);
                    if (resourceData.cameraNormalsTexture.IsValid()) {
                        builder.UseTexture(resourceData.cameraNormalsTexture, AccessFlags.Read);
                    }

                    builder.SetRenderFunc((PassData passData, UnsafeGraphContext context) => {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        cmd.SetGlobalTexture(ShaderParams.CameraDepthTexture, passData.depthTexture);
                        if (passData.normalsTexture.IsValid()) {
                            cmd.SetGlobalTexture(ShaderParams.CameraNormalsTexture, passData.normalsTexture);
                        }
                        cmd.SetRenderTarget(passData.gBuffer0);
                        cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, mat, 0, (int)Pass.OrganicLight);
                    });
                }
            }

#endif

            public void Cleanup () {
                CoreUtils.Destroy(mat);
            }
        }

        partial class TransparentDepthRenderPass : ScriptableRenderPass {

            const string m_ProfilerTag = "Radiant GI Transparent Depth PrePass";
            const string m_DepthOnlyShader = "Hidden/Kronnect/RadiantGI/DepthOnly";

            public static int transparentLayerMask;

            static FilteringSettings filterSettings;
            static readonly List<ShaderTagId> shaderTagIdList = new List<ShaderTagId>();

#if UNITY_2022_2_OR_NEWER
            RTHandle m_Depth;
#else
            RenderTargetIdentifier m_Depth;
#endif
            static Material depthOnlyMaterial;

            const bool useOptimizedDepthOnlyShader = true;

            public TransparentDepthRenderPass () {
#if UNITY_2022_2_OR_NEWER
                RenderTargetIdentifier rti = new RenderTargetIdentifier(ShaderParams.TransparentDepthTexture, 0, CubemapFace.Unknown, -1);
                m_Depth = RTHandles.Alloc(rti, name: "_RadiantTransparentDepthTexture");
#else
                m_Depth = new RenderTargetIdentifier(ShaderParams.TransparentDepthTexture, 0, CubemapFace.Unknown, -1);
#endif
                shaderTagIdList.Clear();
                shaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
                shaderTagIdList.Add(new ShaderTagId("UniversalForward"));
                shaderTagIdList.Add(new ShaderTagId("LightweightForward"));
                filterSettings = new FilteringSettings(RenderQueueRange.transparent, 0);
            }

            public void Setup (int transparentLayerMask) {
                TransparentDepthRenderPass.transparentLayerMask = transparentLayerMask;
            }

#if UNITY_2023_3_OR_NEWER

            class PassData {
                public RendererListHandle rendererListHandle;
                public UniversalCameraData cameraData;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {

                using (var builder = renderGraph.AddUnsafePass<PassData>(m_ProfilerTag, out var passData)) {

                    builder.AllowPassCulling(false);

                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                    UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                    UniversalLightData lightData = frameData.Get<UniversalLightData>();
                    UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                    passData.cameraData = cameraData;

                    SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;
                    var drawingSettings = CreateDrawingSettings(shaderTagIdList, renderingData, cameraData, lightData, sortingCriteria);
                    drawingSettings.perObjectData = PerObjectData.None;
                    if (useOptimizedDepthOnlyShader) {
                        if (depthOnlyMaterial == null) {
                            Shader depthOnly = Shader.Find(m_DepthOnlyShader);
                            if (depthOnly != null) {
                                depthOnlyMaterial = new Material(depthOnly);
                            }
                        }
                        if (depthOnlyMaterial != null) {
                            drawingSettings.overrideMaterial = depthOnlyMaterial;
                        }
                    }
                    
                    if (transparentLayerMask != 0) {
                        if (transparentLayerMask != filterSettings.layerMask) {
                            filterSettings = new FilteringSettings(RenderQueueRange.transparent, transparentLayerMask);
                        }

                        RendererListParams listParams = new RendererListParams(renderingData.cullResults, drawingSettings, filterSettings);
                        passData.rendererListHandle = renderGraph.CreateRendererList(listParams);
                        builder.UseRendererList(passData.rendererListHandle);
                    }

                    builder.SetRenderFunc((PassData passData, UnsafeGraphContext context) => {

                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                        RenderTextureDescriptor depthDesc = passData.cameraData.cameraTargetDescriptor;
                        depthDesc.colorFormat = RenderTextureFormat.Depth;
                        depthDesc.depthBufferBits = 24;
                        depthDesc.msaaSamples = 1;

                        cmd.GetTemporaryRT(ShaderParams.TransparentDepthTexture, depthDesc, FilterMode.Point);
                        RenderTargetIdentifier rti = new RenderTargetIdentifier(ShaderParams.TransparentDepthTexture, 0, CubemapFace.Unknown, -1);
                        cmd.SetGlobalTexture(ShaderParams.TransparentDepthTexture, rti);
                        cmd.SetRenderTarget(rti);
                        cmd.ClearRenderTarget(true, true, Color.black);

                        if (transparentLayerMask != 0 && passData.rendererListHandle.IsValid()) {
                            context.cmd.DrawRendererList(passData.rendererListHandle);
                        }

                        if (depthOnlyMaterial != null) {
                            int transparentSupportCount = RadiantRenderFeature.transparentSupport.Count; 
                            for (int i = 0; i < transparentSupportCount; i++) {
                                Renderer renderer = RadiantRenderFeature.transparentSupport[i].theRenderer;
                                if (renderer != null) {
                                    cmd.DrawRenderer(renderer, depthOnlyMaterial);
                                }
                            }
                        }

                    });
                }
            }

#endif

            public void Cleanup () {
                CoreUtils.Destroy(depthOnlyMaterial);
#if UNITY_2022_2_OR_NEWER
                if (m_Depth != null) {
                    m_Depth.Release();
                }
#endif
            }
        }


        [Tooltip("Select the rendering mode according to the URP asset")]
        public RenderingPath renderingPath = RenderingPath.Deferred;

        [Tooltip("Allows Radiant to be executed even if camera has Post Processing option disabled")]
        public bool ignorePostProcessingOption = true;

        [Tooltip("Enable this option to skip rendering GI on overlay cameras")]
        public bool ignoreOverlayCameras = true;

        [Tooltip("Which cameras can use Radiant Global Illumination")]
        public LayerMask camerasLayerMask = -1;

        [Tooltip("Allocate URP rendering layers texture so virtual emitters can filter by rendering layer mask. Required if any Radiant Volume profile enables 'Virtual Emitters - Use Rendering Layers'.")]
        public bool useRenderingLayers = false;

        internal override bool RequireRenderingLayers (bool isDeferred, bool needsGBufferAccurateNormals, out RenderingLayerUtils.Event atEvent, out RenderingLayerUtils.MaskSize maskSize) {
            atEvent = isDeferred ? RenderingLayerUtils.Event.Opaque : RenderingLayerUtils.Event.DepthNormalPrePass;
            maskSize = RenderingLayerUtils.MaskSize.Bits8;
            return useRenderingLayers;
        }

        RadiantPass radiantPass;
        RadiantComparePass comparePass;
        RadiantOrganicLightPass organicLightPass;
        TransparentDepthRenderPass transparentDepthPass;

        void OnDisable () {
            if (radiantPass != null) {
                radiantPass.Cleanup();
            }
            if (comparePass != null) {
                comparePass.Cleanup();
            }
            if (organicLightPass != null) {
                organicLightPass.Cleanup();
            }
            if (transparentDepthPass != null) {
                transparentDepthPass.Cleanup();
            }
            installed = false;
        }

        public override void Create () {
            radiantPass = new RadiantPass();
            comparePass = new RadiantComparePass();
            organicLightPass = new RadiantOrganicLightPass();
            transparentDepthPass = new TransparentDepthRenderPass();

            emittersForceRefresh = true;
        }

        public static bool needRTRefresh;
        public static bool isRenderingInDeferred;
        public static bool installed;

        static int frameCount;

        static int GetRenderFrameId() {
            int frameId = Application.isPlaying ? Time.frameCount : frameCount++;
            if (frameId <= 0) {
                frameId = Time.frameCount;
            }
            return frameId;
        }

        public override void AddRenderPasses (ScriptableRenderer renderer, ref RenderingData renderingData) {

            installed = true;

            // Match rendering path to the active renderer (editor and build)
            UniversalRenderer universalRenderer = renderer as UniversalRenderer;
            if (universalRenderer == null) return;

            bool isForward = (int)universalRenderer.renderingModeActual == 0 || (int)universalRenderer.renderingModeActual == 2;
            if (isForward && renderingPath == RenderingPath.Deferred) {
                renderingPath = RenderingPath.Forward;
            } else if (!isForward && renderingPath == RenderingPath.Forward) {
                renderingPath = RenderingPath.Deferred;
            }
            isRenderingInDeferred = !isForward;

            if (!renderingData.cameraData.postProcessEnabled && !ignorePostProcessingOption) return;

            Camera cam = renderingData.cameraData.camera;
            if (cam.cameraType == CameraType.Reflection) return;

            bool isSceneView = cam.cameraType == CameraType.SceneView;
            if (cam.cameraType != CameraType.Game && !isSceneView) return;

#if UNITY_EDITOR
            if (isSceneView && !cam.TryGetComponent<UniversalAdditionalCameraData>(out _)) {
                cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
#endif

            CameraRenderType renderType = renderingData.cameraData.renderType;
            if (ignoreOverlayCameras && renderType == CameraRenderType.Overlay) return;

            if ((camerasLayerMask & (1 << cam.gameObject.layer)) == 0) return;

#if UNITY_EDITOR
            if (UnityEditor.ShaderUtil.anythingCompiling) {
                needRTRefresh = true;
            }
            if (needRTRefresh) {
                needRTRefresh = false;
                radiantPass.Cleanup();
                comparePass.Cleanup();
                organicLightPass.Cleanup();
            }
#endif
            RadiantGlobalIllumination radiant = VolumeManager.instance.stack.GetComponent<RadiantGlobalIllumination>();
            if (radiant == null) return;

            if (organicLightPass.Setup(radiant, renderer, isSceneView)) {
                renderer.EnqueuePass(organicLightPass);
            }

            if (radiantPass.Setup(radiant, renderer, this, isSceneView)) {
                if (radiant.transparencySupport.value) {
                    bool hasLayerMask = radiant.transparentLayerMask.value != 0;
                    bool hasScriptObjects = transparentSupport.Count > 0;
                    if (hasLayerMask || hasScriptObjects) {
                        transparentDepthPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox + 1;
                        transparentDepthPass.Setup(radiant.transparentLayerMask.value);
                        renderer.EnqueuePass(transparentDepthPass);
                    }
                }
                renderer.EnqueuePass(radiantPass);
                if (!isSceneView && comparePass.Setup(renderer, this)) {
                    renderer.EnqueuePass(comparePass);
                }
            }

        }

        public static void RegisterVirtualEmitter (RadiantVirtualEmitter emitter) {
            if (emitter == null) return;
            if (!emitters.Contains(emitter)) {
                emitters.Add(emitter);
                emittersForceRefresh = true;
            }
        }

        public static void UnregisterVirtualEmitter (RadiantVirtualEmitter emitter) {
            if (emitter == null) return;
            if (emitters.Contains(emitter)) {
                emitters.Remove(emitter);
                emittersForceRefresh = true;
            }
        }

        public static void RegisterTransparentSupport (RadiantTransparentSupport o) {
            if (o == null) return;
            if (!transparentSupport.Contains(o)) {
                transparentSupport.Add(o);
            }
        }

        public static void UnregisterTransparentSupport (RadiantTransparentSupport o) {
            if (o == null) return;
            if (transparentSupport.Contains(o)) {
                transparentSupport.Remove(o);
            }
        }

    }

}
