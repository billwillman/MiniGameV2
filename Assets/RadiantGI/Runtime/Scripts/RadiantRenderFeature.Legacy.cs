using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if !UNITY_6000_4_OR_NEWER
namespace RadiantGI.Universal {

    public partial class RadiantRenderFeature {

        partial class RadiantPass {

            public override void Configure (CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) {
                ConfigureInput(GetRequiredInputs());
            }

            public override void Execute (ScriptableRenderContext context, ref RenderingData renderingData) {

                sourceDesc = renderingData.cameraData.cameraTargetDescriptor;
                sourceDesc.colorFormat = RenderTextureFormat.ARGBHalf;
                sourceDesc.useMipMap = false;
                sourceDesc.msaaSamples = 1;
                sourceDesc.depthBufferBits = 0;
                cameraTargetDesc = sourceDesc;

                float downsampling = radiant.downsampling.value;
                sourceDesc.width = (int)(sourceDesc.width / downsampling);
                sourceDesc.height = (int)(sourceDesc.height / downsampling);

                Camera cam = renderingData.cameraData.camera;

                CommandBuffer cmd = CommandBufferPool.Get(RGI_CBUF_NAME);
                cmd.Clear();

#if UNITY_2022_2_OR_NEWER
                passData.source = renderingData.cameraData.renderer.cameraColorTargetHandle;
#else
                passData.source = renderingData.cameraData.renderer.cameraColorTarget;
#endif

                passData.cmd = cmd;
                passData.cam = cam;
                passData.reflectionProbes = renderingData.cullResults.visibleReflectionProbes;
                passData.usesProbeAtlas = false; // only on Unity 6.1+ with render graph

                RenderGI(passData);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        partial class RadiantComparePass {

            public override void Execute (ScriptableRenderContext context, ref RenderingData renderingData) {

                CommandBuffer cmd = CommandBufferPool.Get(RGI_CBUF_NAME);
                cmd.Clear();

#if UNITY_2022_2_OR_NEWER
                passData.source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                passData.sourceDepth = renderingData.cameraData.renderer.cameraDepthTargetHandle;
#else
                passData.source = renderingData.cameraData.renderer.cameraColorTarget;
                passData.sourceDepth = renderingData.cameraData.renderer.cameraDepthTarget;
#endif

                RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.useMipMap = false;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;
                passData.cameraTargetDesc = desc;

                passData.cmd = cmd;

                ExecutePass(passData);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        partial class RadiantOrganicLightPass {

            public override void Configure (CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) {
#if UNITY_2022_1_OR_NEWER
                RTHandle m_GbufferAttachmentsHandle = GetAlbedoFromGbuffer();
                ConfigureTarget(m_GbufferAttachmentsHandle, m_DeferredLights.DepthAttachmentHandle);
#else
                RenderTargetIdentifier m_GbufferAttachmentsIdentifier = GetAlbedoFromGbuffer();
                m_GbufferAttachmentsIdentifier = new RenderTargetIdentifier(m_GbufferAttachmentsIdentifier, 0, CubemapFace.Unknown, -1);
                ConfigureTarget(m_GbufferAttachmentsIdentifier, m_DeferredLights.DepthAttachmentIdentifier);
#endif
            }

            public override void Execute (ScriptableRenderContext context, ref RenderingData renderingData) {

                CommandBuffer cmd = CommandBufferPool.Get(m_strProfilerTag);
                cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, mat, 0, (int)Pass.OrganicLight);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        partial class TransparentDepthRenderPass {

            public override void Configure (CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) {
                if (transparentLayerMask != 0 && transparentLayerMask != filterSettings.layerMask) {
                    filterSettings = new FilteringSettings(RenderQueueRange.transparent, transparentLayerMask);
                }
                RenderTextureDescriptor depthDesc = cameraTextureDescriptor;
                depthDesc.colorFormat = RenderTextureFormat.Depth;
                depthDesc.depthBufferBits = 24;
                depthDesc.msaaSamples = 1;

                cmd.GetTemporaryRT(ShaderParams.TransparentDepthTexture, depthDesc, FilterMode.Point);
#if UNITY_2022_2_OR_NEWER
                cmd.SetGlobalTexture(ShaderParams.TransparentDepthTexture, m_Depth);
                ConfigureTarget(m_Depth);
#else
                cmd.SetGlobalTexture(ShaderParams.TransparentDepthTexture, m_Depth);
                ConfigureTarget(m_Depth);
#endif
                ConfigureClear(ClearFlag.Depth, Color.black);
            }

            public override void Execute (ScriptableRenderContext context, ref RenderingData renderingData) {
                CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                if (useOptimizedDepthOnlyShader) {
                    if (depthOnlyMaterial == null) {
                        Shader depthOnly = Shader.Find(m_DepthOnlyShader);
                        if (depthOnly != null) {
                            depthOnlyMaterial = new Material(depthOnly);
                        }
                    }
                }

                if (transparentLayerMask != 0) {
                    SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;
                    var drawSettings = CreateDrawingSettings(shaderTagIdList, ref renderingData, sortingCriteria);
                    drawSettings.perObjectData = PerObjectData.None;

                    if (depthOnlyMaterial != null) {
                        drawSettings.overrideMaterial = depthOnlyMaterial;
                    }
                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings);
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

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
#endif
