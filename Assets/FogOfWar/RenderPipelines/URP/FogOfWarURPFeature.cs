using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FoW
{
    public class FogOfWarURPFeature : ScriptableRendererFeature
    {
        FogOfWarURPPass _fowPass;

        public override void Create()
        {
            _fowPass = new FogOfWarURPPass(RenderPassEvent.BeforeRenderingPostProcessing);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType != CameraType.Game)
                return;

            _fowPass.Setup(renderer);
            renderer.EnqueuePass(_fowPass);
        }
    }
}
