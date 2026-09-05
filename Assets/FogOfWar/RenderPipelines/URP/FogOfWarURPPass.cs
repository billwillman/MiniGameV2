using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace FoW
{
    class FogOfWarURPManager : FogOfWarPostProcessManager
    {
        Material _material;

        // Render Graph
#if UNITY_6000_0_OR_NEWER
        FogOfWarURPPass _pass;
        RenderGraph _renderGraph;
        ContextContainer _frameData;
        public RasterCommandBuffer cmd { get; set; }
        public RTHandle sourceTexture { get; set; }
#endif

        // Legacy
        CommandBuffer _cmd;
        ScriptableRenderContext _context;
        CameraData _cameraData;
        public RenderTargetIdentifier sourceTarget { get; set; }

        bool _isLegacy
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                return _cmd != null;
#else
                return true;
#endif
            }
        }

        public bool isActive => _material != null;

        public FogOfWarURPManager()
        {
            if (_material == null)
                _material = new Material(FogOfWarUtils.FindShader("Hidden/FogOfWarURP"));
        }

        public void OnDestroy()
        {
            if (Application.isPlaying)
                Object.Destroy(_material);
            else
                Object.DestroyImmediate(_material);
            _material = null;
        }

        protected override void SetTexture(int id, Texture value) { _material.SetTexture(id, value); }
        protected override void SetVector(int id, Vector4 value) { _material.SetVector(id, value); }
        protected override void SetColor(int id, Color value) { _material.SetColor(id, value); }
        protected override void SetFloat(int id, float value) { _material.SetFloat(id, value); }
        protected override void SetMatrix(int id, Matrix4x4 value) { _material.SetMatrix(id, value); }
        protected override void SetKeyword(string keyword, bool enabled)
        {
            if (enabled)
                _material.EnableKeyword(keyword);
            else
                _material.DisableKeyword(keyword);
        }

        // Render Graph
#if UNITY_6000_0_OR_NEWER
        public void Setup(FogOfWarURPPass pass, RenderGraph renderGraph, ContextContainer frameData)
        {
            _pass = pass;
            _renderGraph = renderGraph;
            _frameData = frameData;
        }
#endif

        // Legacy
        public void Setup(CommandBuffer cmd, ref ScriptableRenderContext context, ref CameraData cameradata)
        {
            _cmd = cmd;
            _context = context;
            _cameraData = cameradata;
            _material.mainTexture = _cameraData.targetTexture;
        }

        protected override void BlitToScreen()
        {
#if UNITY_6000_0_OR_NEWER
            if (!_isLegacy)
            {
                if (sourceTexture != null)
                    _material.SetTexture("_MainTex", sourceTexture);

                cmd.DrawProcedural(Matrix4x4.identity, _material, 0, MeshTopology.Triangles, 3, 1);
                return;
            }
#endif

            int destination = Shader.PropertyToID("FogOfWarURP");

            _cmd.SetGlobalTexture("_MainTex", sourceTarget);
            _cmd.GetTemporaryRT(destination, _cameraData.camera.scaledPixelWidth, _cameraData.camera.scaledPixelHeight, 0, FilterMode.Point, _cameraData.isHdrEnabled ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            _cmd.Blit(sourceTarget, destination);
            _cmd.Blit(destination, sourceTarget, _material);

            _context.ExecuteCommandBuffer(_cmd);
            CommandBufferPool.Release(_cmd);
        }
    }

    public class FogOfWarURPPass : ScriptableRenderPass
    {
        FogOfWarURP _fowURP;
        FogOfWarURPManager _postProcess = null;
        ScriptableRenderer _renderer;

#if UNITY_EDITOR && UNITY_2021_2_OR_NEWER
        bool _hasForcedImmediateTexture = false;
#endif

        public FogOfWarURPPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
            _postProcess = new FogOfWarURPManager();
        }

        public void Setup(ScriptableRenderer renderer)
        {
            // Ungodly fixes to unity shenenigans
#if UNITY_EDITOR && UNITY_2021_2_OR_NEWER
            if (!_hasForcedImmediateTexture)
            {
                foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:UniversalRendererData"))
                {
                    UniversalRendererData renderdata = UnityEditor.AssetDatabase.LoadAssetAtPath<UniversalRendererData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));

                    if (renderdata.intermediateTextureMode != IntermediateTextureMode.Always)
                    {
                        Debug.LogWarning("FoW: Forcing UniversalRendererData.intermediateTextureMode to Always.", renderdata);
                        renderdata.intermediateTextureMode = IntermediateTextureMode.Always;
                        UnityEditor.EditorUtility.SetDirty(renderdata);
                    }

                    foreach (ScriptableRendererFeature feature in renderdata.rendererFeatures)
                    {
                        if (feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                        {
                            Debug.LogWarning("FoW: FogOfWarURP currently does not work with the SSAO render feature. SSAO will be disabled to fix this issue. Hopefully this will be fixed soon.");
                            feature.SetActive(false);
                            UnityEditor.EditorUtility.SetDirty(renderdata);
                        }
                    }
                }

                _hasForcedImmediateTexture = true;
            }
#endif

            _renderer = renderer;
        }

        void SetupPostProcess(Camera camera)
        {
            _postProcess.team = _fowURP.team.value;
            _postProcess.camera = camera;
            _postProcess.style = _fowURP.style.value;
            _postProcess.fogFarPlane = _fowURP.fogFarPlane.value;
            _postProcess.outsideFogStrength = _fowURP.outsideFogStrength.value;
            _postProcess.fogHeightMin = _fowURP.minFogHeight.value;
            _postProcess.fogHeightMax = _fowURP.maxFogHeight.value;
            _postProcess.fogColor = _fowURP.fogColor.value;
            _postProcess.partialFogAmount = _fowURP.partialFogAmount.value;
            _postProcess.fogColorTexture = _fowURP.fogColorTexture.value;
            _postProcess.fogColorTextureScale = _fowURP.fogColorTextureScale.value;
            _postProcess.fogColorTextureHeight = _fowURP.fogColorTextureHeight.value;
        }


#if UNITY_6000_0_OR_NEWER
        class MainPassData
        {
            public TextureHandle inputTexture;
            public FogOfWarURPManager postProcess;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (!cameraData.postProcessEnabled)
                return;

            UniversalResourceData resourcesData = frameData.Get<UniversalResourceData>();

            VolumeStack stack = VolumeManager.instance.stack;
            _fowURP = stack.GetComponent<FogOfWarURP>();
            if (_fowURP == null || !_fowURP.IsActive())
                return;

            _postProcess.Setup(this, renderGraph, frameData);
            SetupPostProcess(cameraData.camera);

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(passName, out MainPassData passData, profilingSampler))
            {
                passData.inputTexture = resourcesData.cameraColor;
                passData.postProcess = _postProcess;

                TextureDesc destinationDesc = renderGraph.GetTextureDesc(resourcesData.cameraColor);
                destinationDesc.name = "FogOfWarURP";
                destinationDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                builder.UseTexture(passData.inputTexture, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                builder.SetRenderFunc((MainPassData data, RasterGraphContext context) => ExecuteMainPass(data, context));

                resourcesData.cameraColor = destination;
            }
        }

        static void ExecuteMainPass(MainPassData data, RasterGraphContext context)
        {
            data.postProcess.cmd = context.cmd;
            data.postProcess.sourceTexture = data.inputTexture.IsValid() ? data.inputTexture : null;
            data.postProcess.Render();
        }
#endif

#pragma warning disable CS0618
#pragma warning disable CS0672
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            base.Configure(cmd, cameraTextureDescriptor);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!renderingData.cameraData.postProcessEnabled)
                return;

            VolumeStack stack = VolumeManager.instance.stack;
            _fowURP = stack.GetComponent<FogOfWarURP>();
            if (_fowURP == null || !_fowURP.IsActive())
                return;

#if UNITY_2022_1_OR_NEWER
            _postProcess.sourceTarget = _renderer.cameraColorTargetHandle;
#else
            _postProcess.sourceTarget = _renderer.cameraColorTarget;
#endif

            CommandBuffer cmd = CommandBufferPool.Get("FogOfWarURP");

            _postProcess.Setup(cmd, ref context, ref renderingData.cameraData);
            SetupPostProcess(renderingData.cameraData.camera);

            _postProcess.Render();
        }
#pragma warning restore CS0672
#pragma warning restore CS0618
    }
}
