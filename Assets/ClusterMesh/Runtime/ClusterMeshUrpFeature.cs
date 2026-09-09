using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ClusterMesh
{
    public sealed class ClusterMeshUrpFeature : ScriptableRendererFeature
    {
        ClusterMeshUrpPass _depthPass;
        ClusterMeshUrpPass _colorPass;

        public override void Create()
        {
            _depthPass = new ClusterMeshUrpPass(RenderPassEvent.BeforeRenderingPrePasses, ClusterMeshUrpPhase.Depth);
            _colorPass = new ClusterMeshUrpPass(RenderPassEvent.BeforeRenderingOpaques, ClusterMeshUrpPhase.Color);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (!ClusterMeshUrpBridge.ShouldSubmitUrp(camera))
                return;

            ClusterMeshSceneBatcher.PrepareAndSubmitUrpShadows(camera);
            renderer.EnqueuePass(_depthPass);
            renderer.EnqueuePass(_colorPass);
        }
    }

    enum ClusterMeshUrpPhase
    {
        Depth,
        Color
    }

    sealed class ClusterMeshUrpPass : ScriptableRenderPass
    {
        readonly ClusterMeshUrpPhase _phase;

        public ClusterMeshUrpPass(RenderPassEvent evt, ClusterMeshUrpPhase phase)
        {
            renderPassEvent = evt;
            _phase = phase;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            CommandBuffer cmd = CommandBufferPool.Get(_phase == ClusterMeshUrpPhase.Depth
                ? "ClusterMesh Depth"
                : "ClusterMesh Color");
            if (_phase == ClusterMeshUrpPhase.Depth)
                ClusterMeshSceneBatcher.SubmitUrpDepth(camera, cmd);
            else
                ClusterMeshSceneBatcher.SubmitUrpColor(camera, cmd);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
