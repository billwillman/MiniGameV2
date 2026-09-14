using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;
using DebugView = RadiantGI.Universal.RadiantGlobalIllumination.DebugView;
using System.Reflection;
using UnityEngine.Rendering;

namespace RadiantGI.Universal {

    public enum PerformancePreset {
        VeryFast,
        Fast,
        Default,
        HighQuality,
        Ultra
    }

#if UNITY_2022_2_OR_NEWER
    [CustomEditor(typeof(RadiantGlobalIllumination))]
#else
    [VolumeComponentEditor(typeof(RadiantGlobalIllumination))]
#endif
    public class RadiantGlobalIlluminationEditor : VolumeComponentEditor {

#pragma warning disable UDR0001
        static PerformancePreset selectedPreset = PerformancePreset.Default;
#pragma warning restore UDR0001

        SerializedDataParameter indirectIntensity, maxIndirectSourceBrightness, indirectDistanceAttenuation, normalMapInfluence;
        SerializedDataParameter nearFieldObscurance, nearFieldObscuranceSpread, nearFieldObscuranceOccluderDistance, nearFieldObscuranceMaxCameraDistance, nearFieldObscuranceTintColor;
        SerializedDataParameter virtualEmitters;
        SerializedDataParameter organicLight, organicLightThreshold, organicLightNormalsInfluence, organicLightSpread, organicLightTintColor, organicLightAnimationSpeed, organicLightDistanceScaling;
        SerializedDataParameter unityAmbientLighting, ambientStripPrePass;
        SerializedDataParameter brightnessThreshold, brightnessMax, sourceBrightness, giWeight, fallbackAmbient, nearCameraAttenuation, saturation, limitToVolumeBounds, aoInfluence, occlusionIntensity;
        SerializedDataParameter stencilCheck, stencilValue, stencilCompareFunction;
        SerializedDataParameter rayCount, rayMaxLength, rayMaxSamples, rayJitter, thickness, rayReuse, rayBounce;
        SerializedDataParameter fallbackReuseRays, fallbackReflectionProbes, probesIntensity, fallbackReflectiveShadowMap, reflectiveShadowMapIntensity, fallbackDefaultAmbient;
        SerializedDataParameter blurSpread, blurQuality, downsampling, highQualityFiltering, raytracerAccuracy, depthPrecision;
        SerializedDataParameter temporalFilter, temporalStability, temporalResponseSpeed, darkThreshold, darkThresholdMultiplier;
        SerializedDataParameter showInEditMode, showInSceneView, debugView, debugDepthMultiplier, debugMotionVectorMultiplier, compareMode, compareSameSide, comparePanning, compareLineAngle, compareLineWidth;
        SerializedDataParameter transparencySupport, transparentLayerMask;

#pragma warning disable UDR0001
        static GUIStyle sectionHeaderStyle;
#pragma warning restore UDR0001

        const string PrefKeyPrefix = "RadiantGI.RGIEditor.";
        const string PrefKeyGeneral = PrefKeyPrefix + "General";
        const string PrefKeyFallbacks = PrefKeyPrefix + "Fallbacks";
        const string PrefKeyRaymarch = PrefKeyPrefix + "Raymarch";
        const string PrefKeyTemporal = PrefKeyPrefix + "Temporal";
        const string PrefKeyArtistic = PrefKeyPrefix + "Artistic";
        const string PrefKeyDebug = PrefKeyPrefix + "Debug";

        bool showGeneral = true;
        bool showFallbacks = true;
        bool showRaymarch = true;
        bool showTemporal = true;
        bool showArtistic = true;
        bool showDebug = true;

#if !UNITY_2021_2_OR_NEWER
        public override bool hasAdvancedMode => false;
#endif

        bool isAmbientProbeValid;
        Light cachedDirectionalLight;

        public override void OnEnable () {
            base.OnEnable();

            var o = new PropertyFetcher<RadiantGlobalIllumination>(serializedObject);
            indirectIntensity = Unpack(o.Find(x => x.indirectIntensity));
            maxIndirectSourceBrightness = Unpack(o.Find(x => x.indirectMaxSourceBrightness));
            indirectDistanceAttenuation = Unpack(o.Find(x => x.indirectDistanceAttenuation));
            normalMapInfluence = Unpack(o.Find(x => x.normalMapInfluence));
            nearFieldObscurance = Unpack(o.Find(x => x.nearFieldObscurance));
            nearFieldObscuranceSpread = Unpack(o.Find(x => x.nearFieldObscuranceSpread));
            nearFieldObscuranceOccluderDistance = Unpack(o.Find(x => x.nearFieldObscuranceOccluderDistance));
            nearFieldObscuranceMaxCameraDistance = Unpack(o.Find(x => x.nearFieldObscuranceMaxCameraDistance));
            nearFieldObscuranceTintColor = Unpack(o.Find(x => x.nearFieldObscuranceTintColor));
            virtualEmitters = Unpack(o.Find(x => x.virtualEmitters));
            organicLight = Unpack(o.Find(x => x.organicLight));
            organicLightThreshold = Unpack(o.Find(x => x.organicLightThreshold));
            organicLightSpread = Unpack(o.Find(x => x.organicLightSpread));
            organicLightNormalsInfluence = Unpack(o.Find(x => x.organicLightNormalsInfluence));
            organicLightTintColor = Unpack(o.Find(x => x.organicLightTintColor));
            organicLightAnimationSpeed = Unpack(o.Find(x => x.organicLightAnimationSpeed));
            organicLightDistanceScaling = Unpack(o.Find(x => x.organicLightDistanceScaling));
            unityAmbientLighting = Unpack(o.Find(x => x.unityAmbientLighting));
            ambientStripPrePass = Unpack(o.Find(x => x.ambientStripPrePass));
            brightnessThreshold = Unpack(o.Find(x => x.brightnessThreshold));
            brightnessMax = Unpack(o.Find(x => x.brightnessMax));
            sourceBrightness = Unpack(o.Find(x => x.sourceBrightness));
            giWeight = Unpack(o.Find(x => x.giWeight));
            fallbackAmbient = Unpack(o.Find(x => x.fallbackAmbient));
            nearCameraAttenuation = Unpack(o.Find(x => x.nearCameraAttenuation));
            saturation = Unpack(o.Find(x => x.saturation));
            limitToVolumeBounds = Unpack(o.Find(x => x.limitToVolumeBounds));
            stencilCheck = Unpack(o.Find(x => x.stencilCheck));
            stencilValue = Unpack(o.Find(x => x.stencilValue));
            stencilCompareFunction = Unpack(o.Find(x => x.stencilCompareFunction));
            aoInfluence = Unpack(o.Find(x => x.aoInfluence));
            occlusionIntensity = Unpack(o.Find(x => x.occlusionIntensity));
            rayCount = Unpack(o.Find(x => x.rayCount));
            rayMaxLength = Unpack(o.Find(x => x.rayMaxLength));
            rayMaxSamples = Unpack(o.Find(x => x.rayMaxSamples));
            rayJitter = Unpack(o.Find(x => x.rayJitter));
            thickness = Unpack(o.Find(x => x.thickness));
            rayReuse = Unpack(o.Find(x => x.rayReuse));
            rayBounce = Unpack(o.Find(x => x.rayBounce));
            fallbackReuseRays = Unpack(o.Find(x => x.fallbackReuseRays));
            fallbackReflectionProbes = Unpack(o.Find(x => x.fallbackReflectionProbes));
            probesIntensity = Unpack(o.Find(x => x.probesIntensity));
            fallbackReflectiveShadowMap = Unpack(o.Find(x => x.fallbackReflectiveShadowMap));
            reflectiveShadowMapIntensity = Unpack(o.Find(x => x.reflectiveShadowMapIntensity));
            fallbackDefaultAmbient = Unpack(o.Find(x => x.fallbackDefaultAmbient));
            blurSpread = Unpack(o.Find(x => x.blurSpread));
            blurQuality = Unpack(o.Find(x => x.blurQuality));
            downsampling = Unpack(o.Find(x => x.downsampling));
            highQualityFiltering = Unpack(o.Find(x => x.highQualityFiltering));
            raytracerAccuracy = Unpack(o.Find(x => x.raytracerAccuracy));
            depthPrecision = Unpack(o.Find(x => x.depthPrecision));
            temporalFilter = Unpack(o.Find(x => x.temporalFilter));
            temporalStability = Unpack(o.Find(x => x.temporalStabilization));
            temporalResponseSpeed = Unpack(o.Find(x => x.temporalResponseSpeed));
            darkThreshold = Unpack(o.Find(x => x.darkThreshold));
            darkThresholdMultiplier = Unpack(o.Find(x => x.darkThresholdMultiplier));
            showInEditMode = Unpack(o.Find(x => x.showInEditMode));
            showInSceneView = Unpack(o.Find(x => x.showInSceneView));
            debugView = Unpack(o.Find(x => x.debugView));
            debugDepthMultiplier = Unpack(o.Find(x => x.debugDepthMultiplier));
            debugMotionVectorMultiplier = Unpack(o.Find(x => x.debugMotionVectorMultiplier));
            compareMode = Unpack(o.Find(x => x.compareMode));
            compareSameSide = Unpack(o.Find(x => x.compareSameSide));
            comparePanning = Unpack(o.Find(x => x.comparePanning));
            compareLineAngle = Unpack(o.Find(x => x.compareLineAngle));
            compareLineWidth = Unpack(o.Find(x => x.compareLineWidth));
            transparencySupport = Unpack(o.Find(x => x.transparencySupport));
            transparentLayerMask = Unpack(o.Find(x => x.transparentLayerMask));

            isAmbientProbeValid = RadiantRenderFeature.IsAmbientProbeValid(RenderSettings.ambientProbe);
            cachedDirectionalLight = GetDirectionalLight();

            showGeneral = EditorPrefs.GetBool(PrefKeyGeneral, true);
            showFallbacks = EditorPrefs.GetBool(PrefKeyFallbacks, true);
            showRaymarch = EditorPrefs.GetBool(PrefKeyRaymarch, true);
            showTemporal = EditorPrefs.GetBool(PrefKeyTemporal, true);
            showArtistic = EditorPrefs.GetBool(PrefKeyArtistic, true);
            showDebug = EditorPrefs.GetBool(PrefKeyDebug, true);
        }

        public override void OnInspectorGUI () {

            var pipe = GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (pipe == null) {
                EditorGUILayout.HelpBox("Universal Rendering Pipeline asset is not set in Project Settings / Graphics !", MessageType.Error);
                return;
            }

            // Check if RadiantRenderFeature is added to renderer
            if (!RadiantRenderFeature.installed) {
                EditorGUILayout.HelpBox("Radiant Render Feature is not currently running. Please add it to the Renderer Features list in your URP Renderer Data asset.", MessageType.Warning);
                if (GUILayout.Button("Show URP Renderer Asset")) {
                    ShowURPRendererAsset(pipe);
                }
                EditorGUILayout.Separator();
            }
            else {
                string pathName = RadiantRenderFeature.isRenderingInDeferred ? "Deferred" : "Forward";
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Radiant using " + pathName + " rendering path.", EditorStyles.miniLabel);
                if (GUILayout.Button("Show URP Renderer Asset", EditorStyles.miniButton)) {
                    ShowURPRendererAsset(pipe);
                }
                EditorGUILayout.EndHorizontal();

                Camera mainCam = Camera.main;
                bool cameraHDR = mainCam != null && mainCam.allowHDR;
                bool pipeHDR = pipe.supportsHDR;
                bool hdrEnabled = cameraHDR && pipeHDR;
                EditorGUILayout.BeginHorizontal();
                string hdrStatus;
                if (hdrEnabled) {
                    hdrStatus = "HDR: On";
                }
                else if (!pipeHDR) {
                    hdrStatus = "HDR: Off (URP Asset)";
                }
                else {
                    hdrStatus = "HDR: Off (Camera)";
                }
                EditorGUILayout.LabelField(hdrStatus, EditorStyles.miniLabel);
                if (!hdrEnabled && pipeHDR && mainCam != null) {
                    if (GUILayout.Button("Show Camera Settings", EditorStyles.miniButton)) {
                        Selection.activeGameObject = mainCam.gameObject;
                        EditorGUIUtility.PingObject(mainCam.gameObject);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            // Check depth texture mode
            FieldInfo renderers = pipe.GetType().GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (renderers == null) return;
            foreach (var renderer in (object[])renderers.GetValue(pipe)) {
                if (renderer == null) continue;
                FieldInfo depthTextureModeField = renderer.GetType().GetField("m_CopyDepthMode", BindingFlags.NonPublic | BindingFlags.Instance);
                if (depthTextureModeField != null) {
                    int depthTextureMode = (int)depthTextureModeField.GetValue(renderer);
                    if (depthTextureMode == 1) { // transparent copy depth mode
                        EditorGUILayout.HelpBox("Depth Texture Mode in URP asset must be set to 'After Opaques' or 'Force Prepass'.", MessageType.Warning);
                        if (GUILayout.Button("Show Pipeline Asset")) {
                            Selection.activeObject = (Object)renderer;
                            GUIUtility.ExitGUI();
                        }
                        EditorGUILayout.Separator();
                    }
                }
            }

            serializedObject.Update();

            showGeneral = DrawSectionHeader("General", PrefKeyGeneral, showGeneral);
            if (showGeneral) {
                PropertyField(indirectIntensity, new GUIContent("Indirect Light Intensity"));
                EditorGUI.indentLevel++;
                PropertyField(indirectDistanceAttenuation, new GUIContent("Distance Attenuation"));
                PropertyField(rayBounce, new GUIContent("One Extra Bounce", "Performs raycast over the result of the previous bounce (frame). Warning: this can cause lag when camera moves fast."));
                PropertyField(sourceBrightness, new GUIContent("Source Brightness Multiplier"));
                if (sourceBrightness.overrideState.boolValue && sourceBrightness.value.floatValue > 2f) {
                    Light sun = cachedDirectionalLight;
                    if (sun != null && sun.intensity < 2f) {
                        EditorGUILayout.HelpBox("Instead of increasing Source Brightness Multiplier, consider raising the light intensity in the scene.", MessageType.Info);
                    }
                }
                PropertyField(maxIndirectSourceBrightness, new GUIContent("Max Source Brightness"));
                PropertyField(normalMapInfluence);
                EditorGUI.indentLevel--;
                PropertyField(nearFieldObscurance);
                if (nearFieldObscurance.overrideState.boolValue && nearFieldObscurance.value.floatValue > 0f) {
                    EditorGUI.indentLevel++;
                    PropertyField(nearFieldObscuranceSpread, new GUIContent("Spread"));
                    PropertyField(nearFieldObscuranceOccluderDistance, new GUIContent("Occluder Distance"));
                    PropertyField(nearFieldObscuranceMaxCameraDistance, new GUIContent("Max Camera Distance"));
                    PropertyField(nearFieldObscuranceTintColor, new GUIContent("Tint color"));
                    EditorGUI.indentLevel--;
                }
                PropertyField(virtualEmitters);
                PropertyField(organicLight);
                if (organicLight.overrideState.boolValue && organicLight.value.floatValue > 0f) {
                    EditorGUI.indentLevel++;
                    if (!RadiantRenderFeature.isRenderingInDeferred) {
                        EditorGUILayout.HelpBox("Organic Light requires deferred rendering path.", MessageType.Warning);
                    }
                    PropertyField(organicLightSpread, new GUIContent("Spread"));
                    PropertyField(organicLightThreshold, new GUIContent("Threshold"));
                    PropertyField(organicLightNormalsInfluence, new GUIContent("Normals Influence"));
                    PropertyField(organicLightTintColor, new GUIContent("Tint Color"));
                    PropertyField(organicLightAnimationSpeed, new GUIContent("Animation Speed"));
                    PropertyField(organicLightDistanceScaling, new GUIContent("Distance Scaling"));
                    EditorGUI.indentLevel--;
                }
                PropertyField(unityAmbientLighting, new GUIContent("Unity Ambient Intensity", "Controls the amount of Unity ambient light. Lower values subtract more ambient light per-pixel. 0 = full subtraction (disable Unity ambient), 1 = no subtraction (keep all Unity ambient). Default 1 = no subtraction."));
                if (unityAmbientLighting.value.floatValue < 1f) {
                    EditorGUI.indentLevel++;
                    PropertyField(ambientStripPrePass, new GUIContent("Ambient Strip Pre-Pass", "When enabled, ambient light is removed in a dedicated pre-pass before raytracing. More accurate (prevents Unity ambient from contaminating ray sources) but adds one extra full-screen pass. When disabled, the ambient is subtracted inline during the final compose pass (legacy behavior, lower cost). In many scenes the visual difference is negligible."));
                    EditorGUI.indentLevel--;
                    if (!isAmbientProbeValid) {
                        WarnAmbientProbeEmpty();
                    }
                }
            }

            showFallbacks = DrawSectionHeader("Fallbacks", PrefKeyFallbacks, showFallbacks);
            if (showFallbacks) {
                PropertyField(fallbackReflectionProbes, new GUIContent("Reflection Probes", "Uses nearby reflection probes when rays miss"));
                if (fallbackReflectionProbes.value.boolValue) {
                    EditorGUI.indentLevel++;
                    PropertyField(probesIntensity, new GUIContent("Intensity"));
                    EditorGUI.indentLevel--;
                }
                PropertyField(fallbackAmbient, new GUIContent("APV/Sky"));
                if (!isAmbientProbeValid && fallbackAmbient.value.floatValue > 0f) {
                    WarnAmbientProbeEmpty();
                }

                PropertyField(fallbackReflectiveShadowMap, new GUIContent("Reflective Shadow Map", "Separate system - works independently"));
                if (fallbackReflectiveShadowMap.value.boolValue) {
                    EditorGUI.indentLevel++;
                    if (!RadiantShadowMap.installed) {
                        EditorGUILayout.HelpBox("Add Radiant Shadow Map script to the main directional light.", MessageType.Warning);
                    }
                    PropertyField(reflectiveShadowMapIntensity, new GUIContent("Intensity"));
                    EditorGUI.indentLevel--;
                }

                PropertyField(fallbackReuseRays, new GUIContent("Reuse Rays", "Temporal fallback - works alongside other fallbacks"));
                if (fallbackReuseRays.value.boolValue) {
                    EditorGUI.indentLevel++;
                    PropertyField(rayReuse, new GUIContent("Intensity"));
                    EditorGUI.indentLevel--;
                }

                PropertyField(fallbackDefaultAmbient, new GUIContent("Minimum Ambient", "Default color used when other fallbacks fail."));
            }

            showRaymarch = DrawSectionHeader("Raymarch & Performance", PrefKeyRaymarch, showRaymarch);
            if (showRaymarch) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Sample Presets:");
                selectedPreset = (PerformancePreset)EditorGUILayout.EnumPopup(selectedPreset);
                if (GUILayout.Button("Apply", GUILayout.Width(50))) {
                    ApplyPerformancePreset(selectedPreset);
                }
                if (GUILayout.Button("?", GUILayout.Width(20))) {
                    EditorUtility.DisplayDialog("Sample Presets", "These presets are orientative and aimed for full-HD resolution (1920x1080). You may need to adjust the settings manually, for example, you can keep a max distance higher while keeping ray count to 1. You can also reduce raymarch accuracy in 2K/4K to improve performance.", "OK");
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(4);

                PropertyField(raytracerAccuracy, new GUIContent("Raymarch Accuracy", "This option effectively downscales the input depth buffer to improve GPU cache efficiency improving performance significantly."));
                PropertyField(depthPrecision, new GUIContent("Depth Precision", "Storage precision of the internal downscaled depth buffer. Half (16-bit) saves bandwidth but bands on APIs without reversed-Z (e.g. OpenGL Core). Switch to Full (32-bit) to remove the banding."));
                PropertyField(downsampling);
                PropertyField(highQualityFiltering, new GUIContent("High Quality Filtering", "Runs spatial blur and NFO at full resolution. Recommended when using DLSS, FSR or other upscalers."));
                PropertyField(rayCount);
                PropertyField(rayMaxLength, new GUIContent("Max Distance"));
                PropertyField(rayMaxSamples, new GUIContent("Max Samples"));
                PropertyField(rayJitter, new GUIContent("Jittering"));
                PropertyField(thickness);

                PropertyField(blurQuality, new GUIContent("Blur Quality", "Stride of the wide-kernel blur sample loop. Highest = densest sampling and best quality (~63 taps). High = balanced (~32 taps). Faster = fewer samples (~16 taps), recommended for VR/mobile."));
                PropertyField(blurSpread, new GUIContent("Blur Spread", "Multiplier for the spatial blur spread. Increase to reduce flickering at lower resolutions. A value of 2 at 1080p matches the natural blur spread at 4K."));

                PropertyField(transparencySupport);
                if (transparencySupport.value.boolValue) {
                    EditorGUI.indentLevel++;
                    PropertyField(transparentLayerMask, new GUIContent("Layer Mask"));
                    EditorGUILayout.HelpBox("RadiantTransparentSupport script can also be added to specific transparent objects in addition to use (or not) a layer mask.", MessageType.Info);
                    EditorGUILayout.HelpBox("GI for transparent objects is rendered before the transparent pass. Transparent shaders can read from _RadiantGITexture to add the GI on top of transparent surfaces.", MessageType.Info);
                    EditorGUI.indentLevel--;
                }
            }

            showTemporal = DrawSectionHeader("Temporal Filter", PrefKeyTemporal, showTemporal);
            if (showTemporal) {
                PropertyField(temporalFilter, new GUIContent("Temporal Filter", "Enables the per-ray temporal filter that runs before the spatial blur. When disabled, the raycast result feeds the spatial blur and temporal stability passes directly. Disabling reduces trailing artifacts at the cost of more noise during motion."));
                bool temporalFilterEnabled = temporalFilter.value.boolValue;
                if (temporalFilterEnabled) {
                    PropertyField(temporalStability, new GUIContent("Temporal Stability", "Maximum number of frames to accumulate in temporal history. Higher values provide more stability but may increase ghosting."));
                }
                PropertyField(temporalResponseSpeed, new GUIContent("Motion Response", "How quickly the temporal blend reacts to motion. Higher values reduce ghosting but increase noise during movement."));
                if (temporalFilterEnabled) {
                    PropertyField(darkThreshold, new GUIContent("Dark Threshold", "Brightness threshold below which dark threshold is applied. When colors are very dark (brightness is low), this threshold activates to improve stability in dark areas."));
                    PropertyField(darkThresholdMultiplier, new GUIContent("Dark Stability Multiplier", "Multiplier for dark threshold stability. Higher values provide more stability in dark areas but may increase ghosting."));
                } else {
                    EditorGUILayout.HelpBox("Temporal Filter is disabled. Increase Blur Spread to reduce noise.", MessageType.Info);
                }
            }

            showArtistic = DrawSectionHeader("Artistic Controls", PrefKeyArtistic, showArtistic);
            if (showArtistic) {
                PropertyField(aoInfluence, new GUIContent("AO Influence"));
                PropertyField(occlusionIntensity, new GUIContent("Occlusion Intensity", "Intensity of occlusion maps used to attenuate GI"));
                PropertyField(brightnessThreshold, new GUIContent("Source Brightness Threshold"));
                PropertyField(brightnessMax, new GUIContent("GI Maximum Brightness"));
                PropertyField(giWeight, new GUIContent("GI Weight"));
                PropertyField(saturation, new GUIContent("GI Saturation"));
                PropertyField(nearCameraAttenuation);
                PropertyField(limitToVolumeBounds);
                PropertyField(stencilCheck);
                if (stencilCheck.value.boolValue) {
                    PropertyField(stencilValue);
                    PropertyField(stencilCompareFunction);
                }
            }

            showDebug = DrawSectionHeader("Debug", PrefKeyDebug, showDebug);
            if (showDebug) {
                PropertyField(showInEditMode);
                PropertyField(showInSceneView);
                PropertyField(debugView);
                int debugViewInt = debugView.value.intValue;
                if (debugViewInt == (int)DebugView.ReflectiveShadowMap && !fallbackReflectiveShadowMap.value.boolValue) {
                    EditorGUILayout.HelpBox("Reflective Shadow Map fallback option is not enabled. No debug output available.", MessageType.Warning);
                }
                else if (debugView.value.intValue == (int)DebugView.Depth) {
                    PropertyField(debugDepthMultiplier);
                }
                else if (debugView.value.intValue == (int)DebugView.MotionVectors) {
                    PropertyField(debugMotionVectorMultiplier);
                }
                else if (debugView.value.intValue == (int)DebugView.RenderingLayerMask) {
                    EditorGUILayout.HelpBox("Requires the Radiant Render Feature and the active URP Pipeline Asset to have Use Rendering Layers enabled.", MessageType.Info);
                }
                PropertyField(compareMode);
                if (compareMode.value.boolValue) {
                    EditorGUI.indentLevel++;
                    PropertyField(compareSameSide, new GUIContent("Same Side"));
                    DrawAngleInDegrees(compareLineAngle);
                    if (compareSameSide.value.boolValue) {
                        PropertyField(comparePanning, new GUIContent("Panning"));
                    }
                    else {
                        PropertyField(compareLineWidth, new GUIContent("Line Width"));
                    }
                    EditorGUI.indentLevel--;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        void DrawAngleInDegrees (SerializedDataParameter parameter) {
            GUIContent label = new GUIContent("Angle (°)", "Angle of the comparison line in degrees.");
            using (var scope = new OverridablePropertyScope(parameter, label, this)) {
                if (!scope.displayed) return;

                EditorGUI.showMixedValue = parameter.value.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                float degrees = parameter.value.floatValue * Mathf.Rad2Deg;
                degrees = EditorGUILayout.Slider(scope.label, degrees, -180f, 180f);
                if (EditorGUI.EndChangeCheck()) {
                    parameter.value.floatValue = degrees * Mathf.Deg2Rad;
                }
                EditorGUI.showMixedValue = false;
            }
        }

        void ShowURPRendererAsset (UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset pipe) {
            FieldInfo renderersField = pipe.GetType().GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (renderersField == null) return;

            object[] rendererDataList = renderersField.GetValue(pipe) as object[];
            if (rendererDataList == null || rendererDataList.Length == 0) return;

            FieldInfo defaultIndexField = pipe.GetType().GetField("m_DefaultRendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            int defaultIndex = 0;
            if (defaultIndexField != null) {
                defaultIndex = (int)defaultIndexField.GetValue(pipe);
            }
            if (defaultIndex < 0 || defaultIndex >= rendererDataList.Length) {
                defaultIndex = 0;
            }

            Object rendererData = rendererDataList[defaultIndex] as Object;
            if (rendererData != null) {
                Selection.activeObject = rendererData;
                EditorGUIUtility.PingObject(rendererData);
            }
        }

        void ApplyPerformancePreset(PerformancePreset preset) {
            switch (preset) {
                case PerformancePreset.VeryFast:
                    SetOverride(raytracerAccuracy, 6);
                    SetOverride(downsampling, 2f);
                    SetOverride(rayCount, 1);
                    SetOverride(rayJitter, 1f);
                    SetOverride(rayMaxSamples, 12);
                    SetOverride(rayMaxLength, 6f);
                    SetOverride(thickness, 0.4f);
                    SetOverride(temporalStability, 16);
                    SetOverride(blurQuality, RadiantGlobalIllumination.BlurQuality.Faster);
                    if (!SystemInfo.usesReversedZBuffer) SetOverride(depthPrecision, RadiantGlobalIllumination.DepthPrecision.Half);
                    break;
                case PerformancePreset.Fast:
                    SetOverride(raytracerAccuracy, 7);
                    SetOverride(downsampling, 1f);
                    SetOverride(rayCount, 1);
                    SetOverride(rayJitter, 0.7f);
                    SetOverride(rayMaxSamples, 16);
                    SetOverride(rayMaxLength, 8f);
                    SetOverride(thickness, 0.35f);
                    SetOverride(temporalStability, 14);
                    SetOverride(blurQuality, RadiantGlobalIllumination.BlurQuality.Faster);
                    if (!SystemInfo.usesReversedZBuffer) SetOverride(depthPrecision, RadiantGlobalIllumination.DepthPrecision.Half);
                    break;
                case PerformancePreset.Default:
                    SetOverride(raytracerAccuracy, 8);
                    SetOverride(downsampling, 1f);
                    SetOverride(rayCount, 1);
                    SetOverride(rayJitter, 0.3f);
                    SetOverride(rayMaxSamples, 32);
                    SetOverride(rayMaxLength, 12f);
                    SetOverride(thickness, 0.25f);
                    SetOverride(temporalStability, 12);
                    SetOverride(blurQuality, RadiantGlobalIllumination.BlurQuality.High);
                    if (!SystemInfo.usesReversedZBuffer) SetOverride(depthPrecision, RadiantGlobalIllumination.DepthPrecision.Half);
                    break;
                case PerformancePreset.HighQuality:
                    SetOverride(raytracerAccuracy, 7);
                    SetOverride(downsampling, 1f);
                    SetOverride(rayCount, 2);
                    SetOverride(rayJitter, 0.2f);
                    SetOverride(rayMaxSamples, 34);
                    SetOverride(rayMaxLength, 14f);
                    SetOverride(thickness, 0.2f);
                    SetOverride(temporalStability, 12);
                    SetOverride(blurQuality, RadiantGlobalIllumination.BlurQuality.High);
                    if (!SystemInfo.usesReversedZBuffer) SetOverride(depthPrecision, RadiantGlobalIllumination.DepthPrecision.Full);
                    break;
                case PerformancePreset.Ultra:
                    SetOverride(raytracerAccuracy, 8);
                    SetOverride(downsampling, 1f);
                    SetOverride(rayCount, 3);
                    SetOverride(rayJitter, 0.2f);
                    SetOverride(rayMaxSamples, 36);
                    SetOverride(rayMaxLength, 18f);
                    SetOverride(thickness, 0.15f);
                    SetOverride(temporalStability, 12);
                    SetOverride(blurQuality, RadiantGlobalIllumination.BlurQuality.Highest);
                    if (!SystemInfo.usesReversedZBuffer) SetOverride(depthPrecision, RadiantGlobalIllumination.DepthPrecision.Full);
                    break;
            }
        }

        void SetOverride(SerializedDataParameter param, int value) {
            param.value.intValue = value;
            param.overrideState.boolValue = true;
        }

        void SetOverride(SerializedDataParameter param, float value) {
            param.value.floatValue = value;
            param.overrideState.boolValue = true;
        }

        void SetOverride(SerializedDataParameter param, System.Enum value) {
            param.value.intValue = System.Convert.ToInt32(value);
            param.overrideState.boolValue = true;
        }

        bool DrawSectionHeader (string title, string prefKey, bool expanded) {
            if (sectionHeaderStyle == null) {
                sectionHeaderStyle = new GUIStyle("ShurikenModuleTitle");
                sectionHeaderStyle.fontStyle = FontStyle.Bold;
                sectionHeaderStyle.fixedHeight = 22f;
                sectionHeaderStyle.contentOffset = new Vector2(20f, -2f);
            }

            Rect rect = GUILayoutUtility.GetRect(16f, 22f, sectionHeaderStyle);
            GUI.Box(rect, title, sectionHeaderStyle);

            Rect toggleRect = new Rect(rect.x + 6f, rect.y + 2f, 13f, 13f);
            if (Event.current.type == EventType.Repaint) {
                EditorStyles.foldout.Draw(toggleRect, false, false, expanded, false);
            }

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition)) {
                expanded = !expanded;
                EditorPrefs.SetBool(prefKey, expanded);
                Event.current.Use();
            }

            return expanded;
        }

        void WarnAmbientProbeEmpty () {
            EditorGUILayout.HelpBox("Ambient Probe is empty. Click Update to regenerate it from current environment lighting.", MessageType.Info);
            if (GUILayout.Button("Update Ambient Probe")) {
                DynamicGI.UpdateEnvironment();
                isAmbientProbeValid = RadiantRenderFeature.IsAmbientProbeValid(RenderSettings.ambientProbe);
            }
        }

        Light GetDirectionalLight () {
            Light sun = RenderSettings.sun;
            if (sun != null && sun.type == LightType.Directional) {
                return sun;
            }
            Light[] lights = Misc.FindObjectsOfType<Light>();
            for (int i = 0; i < lights.Length; i++) {
                Light light = lights[i];
                if (light != null && light.type == LightType.Directional) {
                    return light;
                }
            }
            return null;
        }
    }
}
