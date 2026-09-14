**************************************
*    RADIANT GLOBAL ILLUMINATION     *
*        Created by Kronnect         *   
*            README FILE             *
**************************************


Notice about Universal Rendering Pipeline
-----------------------------------------
This package is designed for URP. It requires Unity 2022.3 or later
Video instructions: https://youtu.be/sjcUA_nx5s4

To install the plugin correctly:

1) Make sure you have Universal Rendering Pipeline asset installed (from Package Manager).
2) Go to Project Settings / Graphics.
3) Double click the Universal Rendering Pipeline asset.
4) Double click the Universal Renderer asset.
5) Click "+" to add the Radiant Renderer Feature to the list of the Renderer Features.
6) If you're using forward rendering path, select the checkbox "Forward Rendering" in the Radiant Render Feature.

Note: URP assets can be assigned to Settings / Graphics and also Settings / Quality. Check both sections!

Make sure the Radiant Renderer Feature is listed in the Renderer Features of the  Forward Renderer in the pipeline asset.



Quick help: what's this and how to use this asset?
--------------------------------------------------

Radiant Global Illumination brings realtime screen space global illumination to URP.
It's a fast way to provide more natural look to the scenes.
Global Illumination means that each pixel acts as a tiny light so it bounces on the pixel surface and illuminate other surfaces.

Follow the instructions above to configure the effect in your project.


Help & Support Forum
--------------------

Check the Documentation folder for detailed instructions.

Have any question or issue?

* Support-Web: https://kronnect.com/docs/radiant-gi-urp/
* Support-Discord: https://discord.gg/EH2GMaM
* Email: contact@kronnect.com
* Twitter: @Kronnect

If you like Radiant GI, please rate it on the Asset Store. It encourages us to keep improving it! Thanks!
https://assetstore.unity.com/packages/package/225934#reviews



Future updates
--------------

All our assets follow an incremental development process by which a few beta releases are published on our support forum (kronnect.com).
We encourage you to signup and engage our forum. The forum is the primary support and feature discussions medium.

Of course, all updates of Radiant GI will be eventually available on the Asset Store.



More Cool Assets!
-----------------
Check out our other assets here:
https://assetstore.unity.com/publishers/15018



Version history
---------------

Version 32.3
- Compare mode improvements: side by side now supports custom angle
- [Fix] Shader compilation error on Unity 6.7 (URP 17.7)

Version 32.2.2
- [Fix] Fixed regression on Unity 6.3

Version 32.2.1
- [Fix] Added compatibility with Unity 6.5 (URP)

Version 32.2
- Added "Depth Precision" option (Half/Full) for the internal depth buffer. Removes depth banding on graphics APIs without a reversed-Z buffer (e.g. OpenGL); quality presets pick the right value automatically.
- [Fix] OpenGL: global illumination could disappear due to an empty multi-render-target depth binding.
- [Fix] Rendering path now matches the active renderer in builds too (e.g. Deferred falling back to Forward on OpenGL), fixing the effect breaking in builds while working in the editor.
- [Fix] Removed ghosting in the Scene View when moving the camera outside Play mode.

Version 32.1
- Added "Temporal Filter" toggle. Disabling it (paired with a higher Blur Spread) reduces ghosting in dark areas at the cost of more noise during motion.
- Temporal Stability minimum lowered from 6 to 1 for faster response.
- [Fix] Raymarch could occasionally report hits past the maximum distance after the 32.0 thin-geometry improvements.

Version 32.0
- Virtual emitters: new Box shape with volumetric falloff and transform rotation support
- Virtual emitters: added support for rendering layers
- Added "Blur Quality" setting (Highest / High / Faster)
- Improved raycast accuracy on thin geometry (walls, railings, foliage) which now captures 10-20% more light (you may need to reduce radiant indirect intensity after this update).
- [Fix] VR Single Pass Instanced fixes

Version 31.4
- Improved GI accuracy: ambient light is now stripped from ray sources via a pre-pass, preventing indirect light from bouncing Unity ambient through the scene
- Added "High Quality Filtering" option: runs spatial blur and NFO at full resolution, improving quality when using upscalers (DLSS, FSR, etc.)
- New "Ambient Strip Pre-Pass" toggle (under "Unity Ambient Intensity"): activates a dedicated pre-pass that removes ambient light from ray sources before raytracing. Disabled by default to preserve performance
- "Unity Ambient Intensity" slider now controls how much ambient is removed before ray tracing (1 = none, 0 = full removal)

Version 31.3.1
- [Fix] Fixed ghosting/smearing caused by render pass double execution in certain Unity Editor layouts

Version 31.2
- Added Blur Spread option

Version 31.0
- Added support for Unity 6.4

Version 30.0
- Added support for Adaptative Probe Volumes
- Reflection Probes no longer need a Reflection Probe script attached
- Improved ray convergence and stability, with additional options for dark areas
- New fallback: default ambient (useful to inject default lighting without using Unity environment ligthting)
- Transparency support option (under Raymarch settings)
- Occlusion maps support

Version 21.0
- Added support for material occlusion maps in deferred (use AO Influence slider)
- [Fix] Fixes related to the New Input System in demo scenes

Version 20.4
- [Fix] Fixes for Radiant Shadow Map in Unity 6

Version 20.3
- Improved effect on specular surfaces
- Added Unity Ambient Intensity slider to Artistic Choices section (URP only)

Version 20.2.2
- [Fix] Fixes and optimizations for URP Render Graph

Version 20.2.1
- [Fix] Fixed a shadow potential artifact in forward rendering path

Version 20.2
- Virtual emitters: easily change radius in SceneView through a Sphere handle
- Virtual emitters: added fade distance option

Version 20.1.2
- [Fix] Fixed rendering sorting issue with Volumetric Fog & Mist asset

Version 20.1.1
- [Fix] Fixed NFO artifacts at far distances

Version 20.1
- Added "Ignore Overlay Cameras" and "Cameras Layer Mask" options to Radiant Render Feature

Version 20.0.2
- [Fix] Fixed an issue on Uniy 2022 or later when Post Processing is enabled on camera with temporal reprojection

Version 20.0.1
- Added inspector warning if Depth Texture Mode is set to After Transparents (should be After Opaques or Depth PrePass)

Version 20.0
- Added support for Render Graph (Unity 2023.3)

Version 8.2.1
- [Fix] Fixes an issue that delayed virtual emitters activation

Version 8.2
- Source Brightness (reduces the brigthness of the original image before adding GI)
- GI Weight (this will reduce the pixel color to allow the added GI to be more prominent in the LDR color range)

Version 8.1
- Temporal Filter chroma threshold max range increased to 2
- Internal raycast improvements
- [Fix] Fixed reflective shadow map lighting changes linked to directional light position
- [Fix] Fixed pixelization issue

Version 8.0.1
- [Fix] Fixed banding artifacts on WebGL

Version 8.0
- Organic Light: new feature that injects light variation in the scene creating more interesting and complex lighting

Version 7.1.1
- [Fix] Fixed integration with Unity SSAO

Version 7.1
- Added "Capture Size" parameter to Radiant Shadow Map script. Increase this value to cover a wider area when using third person view cameras for example.

Version 7.0
- Max Brightness option produces now more natural results

Version 6.9
- Added specular contribution option. Decrease to avoid overexposition of GI over shiny materials.
- [Fix] Fixed rendering issue in deferred when rendering layers option is enabled

Version 6.8
- Added NFO Tint Color option

Version 6.7.1
- [Fix] Fixed NFO bug when accurate g-buffer normals option is used

Version 6.7
- Virtual emitters: culling optimization
- Near Field Obscurance effect improvements
- Other Editor improvements

Version 6.6
- Virtual emitters: added Scene View bounds modifier tool

Version 6.5
- Virtual emitters: added range and material options
- Default maximum virtual emitters increased to 32
- Performance optimizations

Version 6.4.2
- Improved speed response of reflection probe changes
- Fixes

Version 6.4
- Improved near field obscurance option

Version 6.3
- Added support for orthographic camera

Version 6.2
- Added "Near Camera Attenuation" option under Artistic Controls
- Added material index field to virtual emitter
- Filters NaN pixels

Version 6.1
- Some shader optimizations

Version 6.0
- Added "Near Field Obscurance" option
- [Fix] Fixed virtual emitters not visible in compare mode

Version 5.2
- Improved accuracy of distance attenuation term
- Integration with URP native SSAO: parameter AO Influence added under Artistic Control section

Version 5.1
- Added support for both deferred and forward path materials. Enable 'Both' in the Radiant GI render feature if needed.
- Added stencil check option under Artistic Controls section
- Improved GI reconstruction speed on new screen pixels

Version 5.0
- Added support for Unity 2022
- Added "Probes Intensity" option to reflection probe fallback
- Added "Add Material Emission" option to virtual emitters
- Added "Show In Scene View" option

Version 4.0
- Added virtual emitters
- Added reflective shadow map fallback option and intensity property
- Added support for reflection probe blending distance
- Added optimization for single probe
- Updated documentation

Version 3.1
- Added "Limit To Volume Bounds" option
- [Fix] Fixes transparent objects not showing up correctly when Compare Mode is enabled

Version 3.0
- Added "Fallback Mode" with reflection probes option
- [Fix] Fixed color bleeding when using very high indirect intensity values

Version 2.4
- Added new Artistic Controls section with new options: Brightness Threshold, Maximum Brightness and Saturation.
- Radiant GI now ignores overlay cameras
- [Fix] Fixed issue when enabling "Accurate G-Buffer normals"

Version 2.3
- Added "Raytracer Accuracy" option
- Some performance improvements

Version 2.2
- Added "Chroma Threshold". A new option that stabilizes noise based on chroma variance

Version 2.1
- Additional levels for downsampling are now available for better mobile support
- Minor changes to smoothing levels
- [Fix] Fixed depth rejection bug when camera moves

Version 2.0
- Added Normals Influence option to enhance/preserve normal map based details

Version 1.0
- Initial launch