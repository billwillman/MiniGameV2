using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_2023_3_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace RadiantGI.Universal {

    public partial class RadiantShadowMapRenderFeature : ScriptableRendererFeature {

        static Mesh _fullscreenMesh;
        static Mesh fullscreenMesh {
            get {
                if (_fullscreenMesh != null) return _fullscreenMesh;
                Mesh m = new Mesh();
                _fullscreenMesh = m;
                _fullscreenMesh.SetVertices(new List<Vector3> {
                    new Vector3(-1f, -1f, 0f),
                    new Vector3(-1f,  1f, 0f),
                    new Vector3( 1f, -1f, 0f),
                    new Vector3( 1f,  1f, 0f)
                });
                _fullscreenMesh.SetUVs(0, new List<Vector2> {
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f)
                });
                _fullscreenMesh.SetIndices(new int[6] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
                _fullscreenMesh.UploadMeshData(true);
                return _fullscreenMesh;
            }
        }

        partial class RadiantRSMPass : ScriptableRenderPass {
            const string ProfilerTag = "Radiant RSM Capture";
            static Material captureMat;

            class PassData {
                public CommandBuffer cmd;
                public Camera cam;
#if UNITY_2023_3_OR_NEWER
                public TextureHandle depth;
#endif
            }

            static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");

            public RadiantRSMPass() {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            static void ExecutePass(PassData pd) {
                Camera cam = pd.cam;
                if (!RadiantShadowMap.installed) return;
                if (RadiantShadowMap.captureCameraRef == null || cam != RadiantShadowMap.captureCameraRef) return;
                if (RadiantShadowMap.worldPosRef == null || RadiantShadowMap.normalsRef == null) return;
                if (captureMat == null) {
                    if (captureMat == null) {
                        captureMat = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/Kronnect/RadiantGICapture"));
                    }
                }

                CommandBuffer cmd = pd.cmd;

#if UNITY_2023_3_OR_NEWER
                if (pd.depth.IsValid()) {
                    cmd.SetGlobalTexture(CameraDepthTextureId, pd.depth);
                }
#endif

                // Pass 0: World positions
                cmd.SetRenderTarget(RadiantShadowMap.worldPosRef);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, captureMat, 0, 0);
                cmd.SetGlobalTexture(RadiantShadowMap.ShaderParams.RadiantShadowMapWorldPos, RadiantShadowMap.worldPosRef);

                // Pass 1: Normals
                cmd.SetRenderTarget(RadiantShadowMap.normalsRef);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.DrawMesh(fullscreenMesh, Matrix4x4.identity, captureMat, 0, 1);
                cmd.SetGlobalTexture(RadiantShadowMap.ShaderParams.RadiantShadowMapNormals, RadiantShadowMap.normalsRef);

                cmd.SetGlobalTexture(RadiantShadowMap.ShaderParams.RadiantShadowMapColors, RadiantShadowMap.colorsRef);
            }

#if UNITY_2023_3_OR_NEWER
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
                using (var builder = renderGraph.AddUnsafePass<PassData>(ProfilerTag, out var passData)) {
                    builder.AllowPassCulling(false);

                    UniversalCameraData camData = frameData.Get<UniversalCameraData>();
                    UniversalResourceData res = frameData.Get<UniversalResourceData>();
                    passData.cam = camData.camera;
                    passData.depth = res.activeDepthTexture;

                    builder.UseTexture(res.activeDepthTexture);

                    builder.SetRenderFunc((PassData pd, UnsafeGraphContext ctx) => {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                        pd.cmd = cmd;
                        ExecutePass(pd);
                    });
                }
            }
#endif

            public static void Cleanup() {
                if (captureMat != null) {
                    CoreUtils.Destroy(captureMat);
                    captureMat = null;
                }
            }
        }

        RadiantRSMPass rsmPass;

        public override void Create() {
            rsmPass = new RadiantRSMPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            Camera cam = renderingData.cameraData.camera;
            if (RadiantShadowMap.installed && RadiantShadowMap.captureCameraRef != null && cam == RadiantShadowMap.captureCameraRef) {
                renderer.EnqueuePass(rsmPass);
            }
        }

        void OnDisable() {
            RadiantRSMPass.Cleanup();
        }
    }
}


