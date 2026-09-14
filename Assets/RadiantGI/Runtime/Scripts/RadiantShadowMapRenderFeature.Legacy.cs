using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if !UNITY_6000_4_OR_NEWER
namespace RadiantGI.Universal {

    public partial class RadiantShadowMapRenderFeature {

        partial class RadiantRSMPass {

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
                CommandBuffer cmd = CommandBufferPool.Get(ProfilerTag);

                PassData pd = new PassData();
                pd.cam = renderingData.cameraData.camera;
                pd.cmd = cmd;
                ExecutePass(pd);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
#endif
