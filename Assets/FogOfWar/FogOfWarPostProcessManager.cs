using UnityEngine;

namespace FoW
{
    public enum FogOfWarStyle
    {
        /// <summary>
        /// A point filter is applied to the texture. This will give it a pixelated appearance.
        /// </summary>
        Point = 0,

        /// <summary>
        /// A bilinear filter is applied to the texture. This will give it smooth transitions.
        /// </summary>
        Linear = 1,

        /// <summary>
        /// An SDF filter will be applied, with a cutoff at 0.5. This will have hard edges, but with smooth curves. 
        /// </summary>
        SDF = 2
    }

    public class FoWIDs
    {
        public int mainTex;
        public int skyboxTex;
        public int clearFogTex;
        public int fogColorTex;
        public int fogColorTexScale;
        public int inverseView;
        public int inverseProj;
        public int mainFogColor;
        public int partialFogAmount;
        public int mapOffset;
        public int mapSize;
        public int fogTextureSize;
        public int fogTex;
        public int fogPartialTex;
        public int fogHeightRange;
        public int outsideFogStrength;
        public int cameraWorldPosition;
        public int stereoSeparation;

        static FoWIDs _instance = null;
        public static FoWIDs instance
        {
            get
            {
                if (_instance == null)
                    _instance = new FoWIDs();
                return _instance;
            }
        }

        internal FoWIDs()
        {
            mainTex = Shader.PropertyToID("_MainTex");
            skyboxTex = Shader.PropertyToID("_SkyboxTex");
            clearFogTex = Shader.PropertyToID("_ClearFogTex");
            fogColorTex = Shader.PropertyToID("_FogColorTex");
            fogColorTexScale = Shader.PropertyToID("_FogColorTexScale");
            inverseView = Shader.PropertyToID("_FoWInverseView");
            inverseProj = Shader.PropertyToID("_FoWInverseProj");
            mainFogColor = Shader.PropertyToID("_MainFogColor");
            partialFogAmount = Shader.PropertyToID("_PartialFogAmount");
            mapOffset = Shader.PropertyToID("_MapOffset");
            mapSize = Shader.PropertyToID("_MapSize");
            fogTextureSize = Shader.PropertyToID("_FogTextureSize");
            fogTex = Shader.PropertyToID("_FogTex");
            fogHeightRange = Shader.PropertyToID("_FogHeightRange");
            outsideFogStrength = Shader.PropertyToID("_OutsideFogStrength");
            cameraWorldPosition = Shader.PropertyToID("_CameraWorldPosition");
            stereoSeparation = Shader.PropertyToID("_StereoSeparation");
        }
    }

    public abstract class FogOfWarPostProcessManager
    {
        public int team { get; set; }
        public Camera camera { get; set; }
        public bool fogFarPlane { get; set; }
        public float outsideFogStrength { get; set; }
        public FogOfWarStyle style { get; set; }
        public Color fogColor { get; set; }
        public float partialFogAmount { get; set; }
        public Texture fogColorTexture { get; set; }
        public float fogHeightMin { get; set; }
        public float fogHeightMax { get; set; }
        public bool fogTextureScreenSpace { get; set; }
        public float fogColorTextureScale { get; set; }
        public float fogColorTextureHeight { get; set; }

        protected abstract void SetTexture(int id, Texture value);
        protected abstract void SetVector(int id, Vector4 value);
        protected abstract void SetColor(int id, Color value);
        protected abstract void SetFloat(int id, float value);
        protected abstract void SetMatrix(int id, Matrix4x4 value);
        protected abstract void SetKeyword(string keyword, bool enabled);
        protected abstract void BlitToScreen();

        public void Render()
        {
            FogOfWarTeam fow = FogOfWarTeam.GetTeam(team);
            if (fow == null)
            {
                Debug.LogWarning("No FogOfWar team found: " + team.ToString());
                return;
            }

            if (fow.fogTexture == null)
                return;

#if UNITY_2019_3_OR_NEWER
            FogOfWarTextureSource clearfog = null;
            camera.TryGetComponent(out clearfog);
#else
            FogOfWarClearFog clearfog = camera.GetComponent<FogOfWarClearFog>();
#endif
            if (clearfog != null && clearfog.targetCamera?.targetTexture != null)
            {
                fogColorTexture = clearfog.targetCamera.targetTexture;
                fogTextureScreenSpace = true;
            }

            if ((camera.depthTextureMode & DepthTextureMode.Depth) == 0)
                camera.depthTextureMode |= DepthTextureMode.Depth;

            FoWIDs ids = FoWIDs.instance;

            FilterMode filtermode = style == FogOfWarStyle.Point ? FilterMode.Point : FilterMode.Bilinear;
            fow.fogTexture.filterMode = filtermode;
            SetTexture(ids.fogTex, fow.fogTexture);
            SetVector(ids.fogTextureSize, fow.mapResolution.ToFloat());
            SetFloat(ids.mapSize, fow.mapSize);
            SetVector(ids.mapOffset, fow.mapOffset);
            SetColor(ids.mainFogColor, fogColor);
            SetFloat(ids.partialFogAmount, partialFogAmount);
            SetMatrix(ids.inverseView, camera.cameraToWorldMatrix);
            SetMatrix(ids.inverseProj, camera.projectionMatrix.inverse);
            SetFloat(ids.outsideFogStrength, outsideFogStrength);
            SetVector(ids.fogHeightRange, new Vector4(fogHeightMin, fogHeightMax, 0, 0));
            SetVector(ids.cameraWorldPosition, camera.transform.position);
            SetFloat(ids.stereoSeparation, camera.stereoSeparation);

            // orthographic is treated very differently in the shader, so we have to make sure it executes the right code
            SetKeyword("CAMERA_PERSPECTIVE", !camera.orthographic);
            SetKeyword("CAMERA_ORTHOGRAPHIC", camera.orthographic);

            // which plane will the fog be rendered to?
            SetKeyword("PLANE_XY", fow.plane == FogOfWarPlane.XY);
            SetKeyword("PLANE_YZ", fow.plane == FogOfWarPlane.YZ);
            SetKeyword("PLANE_XZ", fow.plane == FogOfWarPlane.XZ);

            // which plane will the fog be rendered to?
            SetKeyword("STYLE_DEFAULT", style != FogOfWarStyle.SDF);
            SetKeyword("STYLE_SDF", style == FogOfWarStyle.SDF);

            SetKeyword("FOG_COLORED", fogColorTexture == null);
            SetKeyword("FOG_TEXTURED_WORLD", fogColorTexture != null && !fogTextureScreenSpace);
            SetKeyword("FOG_TEXTURED_SCREEN", fogColorTexture != null && fogTextureScreenSpace);
            if (fogColorTexture != null)
            {
                SetTexture(ids.fogColorTex, fogColorTexture);
                SetVector(ids.fogColorTexScale, new Vector2(fogColorTextureScale, fogColorTextureHeight));
            }

            SetKeyword("FOGFARPLANE", fogFarPlane);

            BlitToScreen();
        }
    }
}
