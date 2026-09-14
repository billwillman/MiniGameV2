using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FoW
{
    public static class FogOfWarURPEditor
    {
        static string[] _depthBufferRenderFeatures = new string[]
        {
            "ScreenSpaceAmbientOcclusion",
            "DecalRendererFeature"
        };

        static string[] _searchInFolders = new string[] { "Assets" };

        public static void TryFixInstallIssues()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", _searchInFolders))
            {
                UniversalRenderPipelineAsset pipelineasset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));

                if (!pipelineasset.supportsCameraDepthTexture)
                    pipelineasset.supportsCameraDepthTexture = true;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableRendererData", _searchInFolders))
            {
                ScriptableRendererData renderdata = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(AssetDatabase.GUIDToAssetPath(guid));

                bool found = false;
                foreach (ScriptableRendererFeature feature in renderdata.rendererFeatures)
                {
                    if (feature is FogOfWarURPFeature)
                    {
                        if (!feature.isActive)
                        {
                            feature.SetActive(true);
                            EditorUtility.SetDirty(renderdata);
                        }
                        found = true;
                        continue;
                    }

                    int index = System.Array.IndexOf(_depthBufferRenderFeatures, feature.GetType().Name);
                    if (index != -1)
                    {
                        Debug.LogError("FoW: FogOfWarURP currently does not work with the " + _depthBufferRenderFeatures[index] + " render feature. " + _depthBufferRenderFeatures[index] + " will be disabled to fix this issue. Hopefully this will be fixed soon.");
                        feature.SetActive(false);
                        EditorUtility.SetDirty(renderdata);
                    }
                }

                if (!found)
                {
                    renderdata.rendererFeatures.Add(new FogOfWarURPFeature() { name = nameof(FogOfWarURPFeature)});
                    EditorUtility.SetDirty(renderdata);
                }

                if (renderdata is UniversalRendererData urprenderdata)
                {
                    if (urprenderdata.copyDepthMode != CopyDepthMode.AfterOpaques)
                    {
                        urprenderdata.copyDepthMode = CopyDepthMode.AfterOpaques;
                        EditorUtility.SetDirty(renderdata);
                    }
#if UNITY_2021_2_OR_NEWER
                    if (urprenderdata.intermediateTextureMode != IntermediateTextureMode.Always)
                    {
                        urprenderdata.intermediateTextureMode = IntermediateTextureMode.Always;
                        EditorUtility.SetDirty(renderdata);
                    }
#endif
                }
                else if (renderdata is UnityEngine.Rendering.Universal.Renderer2DData renderdata2d)
                    FogOfWarError.Warning(renderdata, "Perspective cameras or orthographic camera not aligned with Z-axis are not supported in the URP 2D renderer.");
            }

            FogOfWarSetup.ForceAddMainShadersInBuild();
            FogOfWarSetup.ForceAddShaderBuild("FogOfWarURP");
        }

        public static void GetInstallErrors()
        {
            string[] guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", _searchInFolders);
            if (guids.Length == 0)
                FogOfWarError.Error(null, "There is no UniversalRenderPipelineAsset in the project!");

            foreach (string guid in guids)
            {
                UniversalRenderPipelineAsset pipelineasset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));

                if (!pipelineasset.supportsCameraDepthTexture)
                    FogOfWarError.Error(pipelineasset, "Depth Texture must be turned on.");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableRendererData", _searchInFolders))
            {
                ScriptableRendererData renderdata = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(AssetDatabase.GUIDToAssetPath(guid));

                ScriptableRendererFeature fowfeature = renderdata.rendererFeatures.Find(f => f is FogOfWarURPFeature);
                if (fowfeature == null)
                {
                    FogOfWarError.Error(renderdata, "No FogOfWarURPFeature was found on UniversalRendererData.");
                    continue;
                }
                if (!fowfeature.isActive)
                    FogOfWarError.Error(renderdata, "The FogOfWarURPFeature is disabled.");

                foreach (ScriptableRendererFeature feature in renderdata.rendererFeatures)
                {
                    int index = System.Array.IndexOf(_depthBufferRenderFeatures, feature.GetType().Name);
                    if (index != -1)
                    {
                        if (feature.isActive)
                            FogOfWarError.Error(renderdata, _depthBufferRenderFeatures[index] + " render feature must be disabled as it causes issues with the depth buffer. Hopefully this will be fixed in a future release.");
                        else
                            FogOfWarError.Warning(renderdata, _depthBufferRenderFeatures[index] + " render feature has been disabled as it causes issues with the depth buffer. Hopefully this will be fixed in a future release.");
                    }
                }

                if (renderdata is UniversalRendererData urprenderdata)
                {
                    if (urprenderdata.copyDepthMode != CopyDepthMode.AfterOpaques)
                        FogOfWarError.Error(renderdata, "Copy Depth Mode must be set to AfterOpaques!");
#if UNITY_2021_2_OR_NEWER
                    if (urprenderdata.intermediateTextureMode != IntermediateTextureMode.Always)
                        FogOfWarError.Error(renderdata, "IntermediateTextureMode must be set to Always for Unity 2021.2+!");
#endif
                }
                else if (renderdata is UnityEngine.Rendering.Universal.Renderer2DData renderdata2d)
                    FogOfWarError.Warning(renderdata, "Perspective cameras or orthographic camera not aligned with Z-axis are not supported in the URP 2D renderer.");
            }

            FogOfWarSetup.CheckMainShadersIncludedInBuild();
            FogOfWarSetup.CheckShaderIsIncludedInBuild("FogOfWarURP");
        }

        static FogOfWarURP GetFogOfWarURPFromVolume(Volume volume)
        {
            return volume.sharedProfile != null && volume.sharedProfile.TryGet(out FogOfWarURP renderer) ? renderer : null;
        }

        public static void GetSceneErrors()
        {
            Volume[] volumes = FogOfWarUtils.FindObjectsOfType<Volume>();
            if (volumes.Length == 0)
                FogOfWarError.Error(null, "There are no Volume components in the scene.");

            bool found = false;
            FogOfWarTeam[] teams = FogOfWarUtils.FindObjectsOfType<FogOfWarTeam>();
            foreach (Volume volume in volumes)
            {
                FogOfWarURP renderer = GetFogOfWarURPFromVolume(volume);
                if (renderer == null)
                    continue;

                found = true;

                if (!System.Array.Exists(teams, t => t.team == renderer.team.value))
                    FogOfWarError.Error(null, "There are no FogOfWarTeams in the scene with team index " + renderer.team.ToString() + "!");

                if (!renderer.fogColor.overrideState || renderer.fogColor.value.a < 0.001f)
                    FogOfWarError.Error(renderer, "The fog color is set to be transparent and won't be visible!");
            }

            if (!found)
                FogOfWarError.Error(null, "There are no Volume components in the scene with FogOfWarURP on them.");

            found = false;
            Camera[] cameras = FogOfWarUtils.FindObjectsOfType<Camera>();
            foreach (Camera camera in cameras)
            {
                UniversalAdditionalCameraData cameradata = camera.GetComponent<UniversalAdditionalCameraData>();
                if (cameradata == null)
                    continue;

                if (!cameradata.renderPostProcessing)
                    continue;

                foreach (Volume volume in volumes)
                {
                    FogOfWarURP renderer = GetFogOfWarURPFromVolume(volume);
                    if (renderer == null)
                        continue;

                    if ((cameradata.volumeLayerMask & (1 << volume.gameObject.layer)) != 0)
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
                FogOfWarError.Error(null, "There are no cameras in the scene that have post processing enabled and have a Culling Mask including a FogOfWarURP Volume.");
        }
    }
}
