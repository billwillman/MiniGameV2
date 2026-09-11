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

        public override void Create()
        {
            _depthPass = new ClusterMeshUrpPass(RenderPassEvent.BeforeRenderingPrePasses, ClusterMeshUrpPhase.Depth);
            _colorPass = new ClusterMeshUrpPass(RenderPassEvent.BeforeRenderingOpaques, ClusterMeshUrpPhase.Color);
            _gbufferPass = new ClusterMeshUrpPass(
                (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingGbuffer + 1),
                ClusterMeshUrpPhase.GBuffer);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (!ClusterMeshUrpBridge.ShouldSubmitUrp(camera))
                return;

            ClusterMeshSceneBatcher.PrepareAndSubmitUrpShadows(camera);
            ClusterSkinnedMeshSceneBatcher.PrepareAndSubmitUrpShadows(camera);
            renderer.EnqueuePass(_depthPass);
            if (ClusterMeshUrpBridge.IsDeferred(renderer))
            {
                _gbufferPass.Setup(renderer);
                renderer.EnqueuePass(_gbufferPass);
            }
            else
            {
                renderer.EnqueuePass(_colorPass);
            }
        }
    }

    enum ClusterMeshUrpPhase
    {
        Depth,
        Color,
        GBuffer
    }

    sealed class ClusterMeshUrpPass : ScriptableRenderPass
    {
        readonly ClusterMeshUrpPhase _phase;
        ScriptableRenderer _renderer;
        bool _deferredTargetsReady;

        public ClusterMeshUrpPass(RenderPassEvent evt, ClusterMeshUrpPhase phase)
        {
            renderPassEvent = evt;
            _phase = phase;
        }

        public void Setup(ScriptableRenderer renderer)
        {
            _renderer = renderer;
            _deferredTargetsReady = false;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (_phase != ClusterMeshUrpPhase.GBuffer)
                return;
            _deferredTargetsReady = ClusterMeshUrpBridge.TryGetDeferredTargets(
                    _renderer, out RTHandle[] colors, out RTHandle depth, out UnityEngine.Experimental.Rendering.GraphicsFormat[] formats);
            if (_deferredTargetsReady)
            {
                ConfigureTarget(colors, depth, formats);
                ConfigureClear(ClearFlag.None, Color.black);
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            string passName = _phase == ClusterMeshUrpPhase.Depth
                ? "ClusterMesh Depth"
                : (_phase == ClusterMeshUrpPhase.GBuffer ? "ClusterMesh URP GBuffer" : "ClusterMesh Color");
            CommandBuffer cmd = CommandBufferPool.Get(passName);
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
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
