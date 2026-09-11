using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RadiantGI.Universal {

    [ExecuteInEditMode, VolumeComponentMenu("Kronnect/Radiant Global Illumination")]
    [HelpURL("https://kronnect.com/docs/radiant-gi-urp/")]
    public class RadiantGlobalIllumination : VolumeComponent, IPostProcessComponent {

        public enum DebugView {
            None,
            Albedo,
            Normals,
            Depth,
            MotionVectors,
            [InspectorName("Rendering Layer Mask")]
            RenderingLayerMask = 19,
            Raycast = 20,
            RaycastAccumulated = 21,
            Downscaled = 30,
            ReflectiveShadowMap = 40,
            TemporalAccumulationBuffer = 60,
            FinalGI = 70
        }

        public enum BlurQuality {
            Highest = 1,
            High = 2,
            Faster = 4
        }

        public enum DepthPrecision {
            [InspectorName("Half (16-bit)")]
            Half = 1,
            [InspectorName("Full (32-bit)")]
            Full = 2
        }

        [Tooltip("Intensity of the indirect lighting.")]
        public FloatParameter indirectIntensity = new FloatParameter(0);

        [Tooltip("Distance attenuation applied to indirect lighting. Reduces indirect intensity by square of distance.")]
        public ClampedFloatParameter indirectDistanceAttenuation = new ClampedFloatParameter(0, 0, 1);

        [Tooltip("Clamps maximum brightness of surfaces emitting indirect light.")]
        public FloatParameter indirectMaxSourceBrightness = new FloatParameter(15);

        [Tooltip("Determines how much influence has the surface normal map when receiving indirect lighting.")]
        public ClampedFloatParameter normalMapInfluence = new ClampedFloatParameter(0.2f, 0, 1);

        [Tooltip("Add one ray bounce.")]
        public BoolParameter rayBounce = new BoolParameter(false);

        [Tooltip("Intensity of the near field obscurance effect. Darkens surfaces occluded by other nearby surfaces.")]
        public FloatParameter nearFieldObscurance = new FloatParameter(0);

        [Tooltip("Spread or radius of the near field obscurance effect")]
        public ClampedFloatParameter nearFieldObscuranceSpread = new ClampedFloatParameter(0.2f, 0.01f, 1f);

        [Tooltip("Maximum distance of Near Field Obscurance effect")]
        public FloatParameter nearFieldObscuranceMaxCameraDistance = new FloatParameter(125f);

        [Tooltip("Distance threshold of the occluder")]
        public ClampedFloatParameter nearFieldObscuranceOccluderDistance = new ClampedFloatParameter(0.825f, 0, 1f);

        [Tooltip("Tint color of Near Field Obscurance effect")]
        [ColorUsage(showAlpha: false)]
        public ColorParameter nearFieldObscuranceTintColor = new ColorParameter(Color.black);

        [Tooltip("Enable user-defined light emitters in the scene.")]
        public BoolParameter virtualEmitters = new BoolParameter(false);

        [Tooltip("Intensity of organic light. This option injects artifical/procedural light variations into g-buffers to product a more natural and interesting lit environment. This added lighting is also used as source for indirect lighting.")]
        public ClampedFloatParameter organicLight = new ClampedFloatParameter(0, 0, 1);

        [Tooltip("Threshold of organic light noise calculation")]
        public ClampedFloatParameter organicLightThreshold = new ClampedFloatParameter(0.5f, 0, 1);

        [Tooltip("Organic light spread")]
        public ClampedFloatParameter organicLightSpread = new ClampedFloatParameter(0.98f, 0.9f, 1f);

        [Tooltip("Organic light normal influence preserves normal map effect on textures")]
        public ClampedFloatParameter organicLightNormalsInfluence = new ClampedFloatParameter(0.95f, 0f, 1f);

        [Tooltip("Organic light tint color")]
        public ColorParameter organicLightTintColor = new ColorParameter(Color.white);

        [Tooltip("Animation speed")]
        public Vector3Parameter organicLightAnimationSpeed = new Vector3Parameter(Vector3.zero);

        [Tooltip("Reduces organic light pattern repetition at the distance")]
        public BoolParameter organicLightDistanceScaling = new BoolParameter(false);

        [Tooltip("Controls the amount of Unity ambient light used in GI rays and final image. Lower values subtract more ambient light. 0 = full subtraction, 1 = no subtraction.")]
        public ClampedFloatParameter unityAmbientLighting = new ClampedFloatParameter(1f, 0, 1);

        [Tooltip("When enabled, ambient light is removed in a dedicated pre-pass before raytracing. More accurate (prevents Unity ambient from contaminating ray sources) but adds one extra full-screen pass. When disabled, the ambient is subtracted inline during the final compose pass (legacy behavior, lower cost). In many scenes the visual difference is negligible.")]
        public BoolParameter ambientStripPrePass = new BoolParameter(false);

        [Tooltip("Number of rays per pixel")]
        public ClampedIntParameter rayCount = new ClampedIntParameter(1, 1, 4);

        [Tooltip("Max ray length. Increasing this value may also require increasing the 'Max Samples' value to avoid losing quality.")]
        public FloatParameter rayMaxLength = new FloatParameter(8);

        [Tooltip("Maximum number of samples along the ray-march loop. Intended to avoid very expensive loops. Values of 24 or 32 usually give good results.")]
        public IntParameter rayMaxSamples = new IntParameter(32);

        [Tooltip("Adds a bit of randomization to the ray starting position to reduce banding.")]
        public FloatParameter rayJitter = new FloatParameter(0.3f);

        [Tooltip("The thickness or depth tolerance used to determine when the ray hits a surface. This value represents the minimum distance of the ray-march position to a nearby surface to consider a hit. Values of 0.3 to 1 are usually the best.")]
        public FloatParameter thickness = new FloatParameter(0.25f);

        [Tooltip("In case a ray miss a target, reuse rays from previous frames.")]
        public BoolParameter fallbackReuseRays = new BoolParameter(false);

        [Tooltip("If a ray misses a target, reuse result from history buffer. This value is the intensity of the previous color in case the ray misses the target. A value of 0 disables this feature. This option is very performant.")]
        public ClampedFloatParameter rayReuse = new ClampedFloatParameter(0, 0, 1);

        [Tooltip("In case a ray misses a target, use nearby reflection probes.")]
        public BoolParameter fallbackReflectionProbes = new BoolParameter(false);

        [Tooltip("Custom global probe intensity multiplier. Note that each probe has also an intensity property.")]
        public MinFloatParameter probesIntensity = new MinFloatParameter(0.2f, 0f);

        [Tooltip("In case a ray misses a target, use reflective shadow map data from the main directional light. This technique renders the scene from the directional light point of view. It's quite expensive if the directional light rotates continuously. You need to add the ReflectiveShadowMap script to the directional light to use this feature.")]
        public BoolParameter fallbackReflectiveShadowMap = new BoolParameter(false);

        [Tooltip("Intensity multiplier for the reflective shadow map fallback.")]
        public ClampedFloatParameter reflectiveShadowMapIntensity = new ClampedFloatParameter(0.8f, 0, 1);

        [Tooltip("Default color used when other fallbacks fail.")]
        [ColorUsage(showAlpha: false, hdr: true)]
        public ColorParameter fallbackDefaultAmbient = new ColorParameter(Color.black);

        [Tooltip("Multiplier for the spatial blur spread. Increase to reduce flickering and noise at lower resolutions. A value of 2 at 1080p matches the natural blur spread at 4K.")]
        public ClampedFloatParameter blurSpread = new ClampedFloatParameter(1, 1, 2);

        [Serializable] public sealed class BlurQualityParameter : VolumeParameter<BlurQuality> { }
        [Tooltip("Stride of the wide-kernel blur. Highest = denser sampling and best quality. Faster = fewer samples (recommended for VR/mobile).")]
        public BlurQualityParameter blurQuality = new BlurQualityParameter { value = BlurQuality.Highest };

        [Tooltip("Downscales the input image affecting all render passes, so the entire effect execution is faster (in exchange of quality of course).")]
        public ClampedFloatParameter downsampling = new ClampedFloatParameter(1, 1, 4);

        [Tooltip("Runs the spatial blur and NFO passes at full resolution instead of half resolution. Reduces blotchy artifacts, especially when using upscalers like DLSS or FSR.")]
        public BoolParameter highQualityFiltering = new BoolParameter(false);

        [Tooltip("By reducing the raytracer accuracy, performance can be improved. Technically this option effectively downscales the input depth buffer to improve GPU cache efficiency.")]
        public ClampedIntParameter raytracerAccuracy = new ClampedIntParameter(8, 1, 8);

        [Serializable] public sealed class DepthPrecisionParameter : VolumeParameter<DepthPrecision> { }
        [Tooltip("Storage precision of the internal downscaled depth buffer. 'Half' (16-bit) saves bandwidth. On graphics APIs without a reversed-Z buffer (e.g. OpenGL Core) the non-linear depth values cluster near the far range where 16-bit precision is poor, causing depth banding; switch to 'Full' (32-bit) to remove it.")]
        public DepthPrecisionParameter depthPrecision = new DepthPrecisionParameter { value = DepthPrecision.Half };

        [Tooltip("Enables the per-ray temporal filter that runs before the spatial blur. When disabled, the raycast result is forwarded directly to the spatial blur and temporal stability passes. Useful to avoid trailing artifacts at the cost of more noise during motion.")]
        public BoolParameter temporalFilter = new BoolParameter(true);

        [Tooltip("Maximum number of frames to accumulate in temporal history. Higher values provide more stability but may increase ghosting. Default = 8.")]
        public ClampedIntParameter temporalStabilization = new ClampedIntParameter(8, 1, 16);

        [Tooltip("How fast the spatio-temporal filter reacts to changes in the screen. A lower value will produce softer results but can introduce ghosting. Higher values reduce ghosting but also the smoothing while moving. Default value = 12.")]
        public FloatParameter temporalResponseSpeed = new FloatParameter(12);

        [Tooltip("Brightness threshold below which extra noise stabilization is applied")]
        public MinFloatParameter darkThreshold = new MinFloatParameter(0.2f, 0);

        [Tooltip("Higher values provide more stability in dark areas.")]
        public MinFloatParameter darkThresholdMultiplier = new MinFloatParameter(10f, 0f);

        [Tooltip("Renders the effect while not in play mode.")]
        public BoolParameter showInEditMode = new BoolParameter(true);

        [Tooltip("Renders the effect also in Scene View.")]
        public BoolParameter showInSceneView = new BoolParameter(false);

        [Tooltip("Determines the minimum brightness value of a pixel to be considered as indirect source for lighting. Every pixel should cast indirect light (even if it's very dim) although you may want to limit this effect to certain bright pixels only using this setting.")]
        public FloatParameter brightnessThreshold = new FloatParameter(0f);

        [Tooltip("Clamps the maximum brightness of the resulting GI contribution. This setting lets you avoid burning pixels with too much lighting.")]
        public FloatParameter brightnessMax = new FloatParameter(8f);

        [Tooltip("Brightness of the original image. Reduce to make GI more visible.")]
        public MinFloatParameter sourceBrightness = new MinFloatParameter(1f, 0);

        [Tooltip("Increases final GI contribution vs source color pixel. Increase this value to reduce the intensity of the source pixel color based on the received GI amount, making the applied GI more apparent.")]
        public FloatParameter giWeight = new FloatParameter(0f);

        [Tooltip("Controls amount of ambient lighting from APV and Sky.")]
        public ClampedFloatParameter fallbackAmbient = new ClampedFloatParameter(0f, 0, 1);

        [Tooltip("Attenuates GI brightness from nearby surfaces.")]
        public FloatParameter nearCameraAttenuation = new FloatParameter(0);

        [Tooltip("Adjusts the color saturation of the computed GI. A value of 0 makes the GI grayscale.")]
        public ClampedFloatParameter saturation = new ClampedFloatParameter(1, 0, 2);

        [Tooltip("Only applies GI within the boundaries of the current volume. Useful to restrict the GI to interior rooms for example, when there're windows and you don't want the GI to be computed outside. Use only if the volume is local.")]
        public BoolParameter limitToVolumeBounds = new BoolParameter(false);

        [Tooltip("Enabling this option activates the stencil mask which avoids GI from being applied to specific pixels on the screen. This can be used to avoid GI over certain objects like UI, FPS weapons or any other object that renders also using stencil.")]
        public BoolParameter stencilCheck = new BoolParameter(false);

        [Tooltip("The stencil reference value to compare against when stencil check is enabled.")]
        public IntParameter stencilValue = new IntParameter(1);

        [Serializable] public sealed class CompareFunctionParameter : VolumeParameter<CompareFunction> { }
        [Tooltip("The comparison function used for stencil testing when stencil check is enabled.")]
        public CompareFunctionParameter stencilCompareFunction = new CompareFunctionParameter { value = CompareFunction.NotEqual };

        [Tooltip("Integration with URP native screen space ambient occlusion (also with HBAO in Lit AO mode). Amount of ambient occlusion that influences indirect lighting created by Radiant.")]
        public ClampedFloatParameter aoInfluence = new ClampedFloatParameter(0f, 0, 1f);

        [Tooltip("Intensity of occlusion maps used to attenuate GI")]
        public ClampedFloatParameter occlusionIntensity = new ClampedFloatParameter(0.5f, 0f, 1f);

        [Serializable]
        public sealed class DebugViewParameter : VolumeParameter<DebugView> { }

        [Tooltip("Useful to inspect the different buffers used by the effect.")]
        public DebugViewParameter debugView = new DebugViewParameter { value = DebugView.None };

        [Tooltip("Depth values multiplier for the depth debug view")]
        public FloatParameter debugDepthMultiplier = new FloatParameter(10);

        [Tooltip("Motion vectors multiplier for the motion vectors debug view")]
        public FloatParameter debugMotionVectorMultiplier = new FloatParameter(1);
		
        [Tooltip("Lets you compare the impact of the effect and the current settings by showing a side-by-side or split screen view of your scene.")]
        public BoolParameter compareMode = new BoolParameter(false);

        [Tooltip("When enabled, shows the comparison on the same side instead of split screen.")]
        public BoolParameter compareSameSide = new BoolParameter(false);

        [Tooltip("Controls the portion of the image shown by both halves when using same side mode.")]
        public ClampedFloatParameter comparePanning = new ClampedFloatParameter(0.25f, 0, 0.5f);

        [Tooltip("Angle of the comparison line. Shown in degrees in the inspector; the scripting value is stored in radians.")]
        public ClampedFloatParameter compareLineAngle = new ClampedFloatParameter(1.4f, -Mathf.PI, Mathf.PI);

        [Tooltip("Width of the comparison line.")]
        public ClampedFloatParameter compareLineWidth = new ClampedFloatParameter(0.002f, 0.0001f, 0.05f);

        [Tooltip("Enables support for transparent objects which will also be included in GI calculations. Choose by layers or add a RadiantTransparentSupport script to the desired transparent objects.")]
        public BoolParameter transparencySupport = new BoolParameter(false);

        [Tooltip("Which layers are considered transparent.")]
        public LayerMaskParameter transparentLayerMask = new LayerMaskParameter(0);


        public bool IsActive() => indirectIntensity.value > 0 || compareMode.value;

        public bool IsTileCompatible() => true;

        void OnValidate() {
            indirectIntensity.value = Mathf.Max(0, indirectIntensity.value);
            indirectMaxSourceBrightness.value = Mathf.Max(0, indirectMaxSourceBrightness.value);
            temporalResponseSpeed.value = Mathf.Max(0, temporalResponseSpeed.value);
            rayMaxLength.value = Mathf.Max(0.1f, rayMaxLength.value);
            rayMaxSamples.value = Mathf.Max(2, rayMaxSamples.value);
            rayJitter.value = Mathf.Max(0.1f, rayJitter.value);
            thickness.value = Mathf.Max(0.1f, thickness.value);
            brightnessThreshold.value = Mathf.Max(0, brightnessThreshold.value);
            brightnessMax.value = Mathf.Max(0, brightnessMax.value);
            nearCameraAttenuation.value = Mathf.Max(0, nearCameraAttenuation.value);
            nearFieldObscurance.value = Mathf.Max(0, nearFieldObscurance.value);
            nearFieldObscuranceMaxCameraDistance.value = Mathf.Max(0, nearFieldObscuranceMaxCameraDistance.value);
            debugDepthMultiplier.value = Mathf.Max(0, debugDepthMultiplier.value);
            debugMotionVectorMultiplier.value = Mathf.Max(0, debugMotionVectorMultiplier.value);
            giWeight.value = Mathf.Max(0, giWeight.value);
            darkThreshold.value = Mathf.Max(0, darkThreshold.value);
            darkThresholdMultiplier.value = Mathf.Max(0, darkThresholdMultiplier.value);
            occlusionIntensity.value = Mathf.Clamp01(occlusionIntensity.value);
        }

        void Reset() {
            RadiantRenderFeature.needRTRefresh = true;
        }


    }
}
