using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ClusterMesh
{
    public sealed class ClusterMeshUrpFeature : ScriptableRendererFeature
    {
        ClusterMeshUrpPass _depthPass;
        ClusterMeshUrpPass _colorPass;
        ClusterMeshUrpPass _gbufferPass;
        ClusterMeshUrpPass _motionPass;

        public override void Create()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            _depthPass = new ClusterMeshUrpPass(RenderPassEvent.BeforeRenderingPrePasses, ClusterMeshUrpPhase.Depth);
            _colorPass = new ClusterMeshUrpPass(RenderPassEvent.BeforeRenderingOpaques, ClusterMeshUrpPhase.Color);
            _gbufferPass = new ClusterMeshUrpPass(
                (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingGbuffer + 1),
                ClusterMeshUrpPhase.GBuffer);
            _motionPass = new ClusterMeshUrpPass(
                (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingPostProcessing - 1),
                ClusterMeshUrpPhase.Motion);
        }

        protected override void Dispose(bool disposing)
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            base.Dispose(disposing);
        }

        static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            ClusterMeshUrpBridge.SubmitUrpShadowsBeforeCull(camera);
        }

        public override void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData)
        {
            ClusterMeshUrpBridge.SubmitUrpShadowsBeforeCull(cameraData.camera);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (!ClusterMeshUrpBridge.ShouldSubmitUrp(camera))
                return;

            ClusterMeshUrpBridge.SubmitUrpShadowsBeforeCull(camera);
            // One shading raster only. A DepthOnly pass plus Forward/GBuffer writes the
            // same clusters twice (often onto the camera target). Scene View is a single
            // Graphics.Draw; the extra prepass showed up as stacked / broken silhouettes
            // in Game View. Color and GBuffer already ZWrite.
            if (ClusterMeshUrpBridge.IsDeferred(renderer))
            {
                _gbufferPass.Setup(renderer);
                renderer.EnqueuePass(_gbufferPass);
            }
            else
            {
                renderer.EnqueuePass(_colorPass);
            }

            if (ClusterMeshSceneBatcher.HasMotionVectors(camera) ||
                ClusterSkinnedMeshSceneBatcher.HasMotionVectors(camera))
            {
                _motionPass.Setup(renderer);
                renderer.EnqueuePass(_motionPass);
            }
        }
    }

    enum ClusterMeshUrpPhase
    {
        Depth,
        Color,
        GBuffer,
        Motion
    }

    sealed class ClusterMeshUrpPass : ScriptableRenderPass
    {
        readonly ClusterMeshUrpPhase _phase;
        ScriptableRenderer _renderer;
        bool _deferredTargetsReady;
        bool _motionTargetsReady;
        bool _loggedDeferredTargetFailure;
        static readonly int MotionVectorParamsId = Shader.PropertyToID("unity_MotionVectorsParams");

        public ClusterMeshUrpPass(RenderPassEvent evt, ClusterMeshUrpPhase phase)
        {
            renderPassEvent = evt;
            _phase = phase;
            if (_phase == ClusterMeshUrpPhase.Motion)
                ConfigureInput(ScriptableRenderPassInput.Motion);
        }

        public void Setup(ScriptableRenderer renderer)
        {
            _renderer = renderer;
            _deferredTargetsReady = false;
            _motionTargetsReady = false;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            // URP calls every OnCameraSetup first, then every Configure. GBufferPass
            // reallocates _GBuffer0 in Configure. Binding here keeps color/depth sizes aligned.
            if (_phase == ClusterMeshUrpPhase.GBuffer)
            {
                bool found = ClusterMeshUrpBridge.TryGetDeferredTargets(
                    _renderer, out RTHandle[] colors, out RTHandle depth, out _);
                _deferredTargetsReady = found &&
                    ClusterMeshUrpBridge.AreCompatibleDeferredTargets(colors, depth);
                if (_deferredTargetsReady)
                {
                    ConfigureTarget(colors, depth);
                    ConfigureClear(ClearFlag.None, Color.black);
                    _loggedDeferredTargetFailure = false;
                }
                else if (!_loggedDeferredTargetFailure)
                {
                    Debug.LogError(
                        "ClusterMesh: URP Deferred GBuffer binding failed; ClusterMesh GBuffer submission was skipped. " +
                        (found
                            ? "GBuffer color and depth attachments have different dimensions."
                            : ClusterMeshUrpBridge.LastDeferredBindingError));
                    _loggedDeferredTargetFailure = true;
                }
            }
            else if (_phase == ClusterMeshUrpPhase.Motion)
            {
                _motionTargetsReady = ClusterMeshUrpBridge.TryGetMotionVectorTargets(
                    _renderer, out RTHandle color, out RTHandle depth) &&
                    ClusterMeshUrpBridge.AreCompatibleDeferredTargets(new[] { color }, depth);
                if (_motionTargetsReady)
                {
                    ConfigureTarget(color, depth);
                    ConfigureClear(ClearFlag.None, Color.black);
                }
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            string passName = _phase == ClusterMeshUrpPhase.Depth
                ? "ClusterMesh Depth"
                : (_phase == ClusterMeshUrpPhase.GBuffer ? "ClusterMesh URP GBuffer"
                    : (_phase == ClusterMeshUrpPhase.Motion ? "ClusterMesh Motion Vectors" : "ClusterMesh Color"));
            CommandBuffer cmd = CommandBufferPool.Get(passName);
            ClusterMeshMaterialUtil.BeginEditorSyncCompilation(cmd);
            if (_phase == ClusterMeshUrpPhase.Depth)
            {
                ClusterMeshSceneBatcher.SubmitUrpDepth(camera, cmd);
                ClusterSkinnedMeshSceneBatcher.SubmitUrpDepth(camera, cmd);
            }
            else if (_phase == ClusterMeshUrpPhase.Color)
            {
                ClusterMeshSceneBatcher.SubmitUrpColor(camera, cmd);
                ClusterSkinnedMeshSceneBatcher.SubmitUrpColor(camera, cmd);
            }
            else if (_deferredTargetsReady)
            {
                ClusterMeshSceneBatcher.SubmitUrpGBuffer(camera, cmd);
                ClusterSkinnedMeshSceneBatcher.SubmitUrpGBuffer(camera, cmd);
            }
            else if (_phase == ClusterMeshUrpPhase.Motion && _motionTargetsReady)
            {
                // Indirect draws do not receive Unity's per-Renderer motion constants.
                // Keep the official URP convention enabled for our explicit history data.
                cmd.SetGlobalVector(MotionVectorParamsId, new Vector4(0f, 1f, 0f, 0f));
                ClusterMeshSceneBatcher.SubmitUrpMotionVectors(camera, cmd);
                ClusterSkinnedMeshSceneBatcher.SubmitUrpMotionVectors(camera, cmd);
            }
            ClusterMeshMaterialUtil.EndEditorSyncCompilation(cmd);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
