using UnityEngine;
using UnityEngine.UI;

namespace FoW
{
    [RequireComponent(typeof(RawImage))]
    public class FogOfWarMinimap : MonoBehaviour
    {
        [Tooltip("The team index that should be displayed. This should be the same index specified on the corresponding FogOfWarTeam component.")]
        public int team = 0;
        public Color fogColor = Color.black;
        public Color partialColor = Color.gray;
        public Color visibleColor = Color.white;
        public Color32 unitColor = Color.blue;
        public Color32 enemyColor = Color.red;
        public Color32 cameraColor = Color.green;
        [Range(0.0f, 1.0f), Tooltip("The fog threshold that will trigger the object to show/hide. A lower value will be more visible in higher fog values.")]
        public float opponentMinFogStrength = 0.5f;
        [Tooltip("The camera that will have it's position drawn on the minimap.")]
        public new Camera camera;
        [Tooltip("The pixel size of all icons.")]
        public int iconSize = 2;
        [Tooltip("The AspectRatioFitter used to ensure the aspect ratio matches the map size of the FogOfWarTeam.")]
        public AspectRatioFitter aspectRatioFitter;

        Texture2D _texture;
        byte[] _fogVisibleValues;
        byte[] _fogPartialValues;
        Color32[] _pixels;

        void OnDestroy()
        {
            Destroy(_texture);
        }

        void LateUpdate()
        {
            FogOfWarTeam fow = FogOfWarTeam.GetTeam(team);
            if (fow == null)
                return;

            // setup texture
            if (_texture == null || _texture.width != fow.mapResolution.x || _texture.height != fow.mapResolution.y)
            {
                if (_texture != null)
                    Destroy(_texture);

                _texture = new Texture2D(fow.mapResolution.x, fow.mapResolution.y, TextureFormat.ARGB32, false, false);
                _texture.name = "FogOfWarMinimap";
                _fogVisibleValues = new byte[fow.mapPixelCount];
                _fogPartialValues = new byte[fow.mapPixelCount];
                _pixels = new Color32[fow.mapPixelCount];

                GetComponent<RawImage>().texture = _texture;
                if (aspectRatioFitter != null)
                    aspectRatioFitter.aspectRatio = (float)fow.mapResolution.x / fow.mapResolution.y;
            }

            // fog
            fow.GetFogValues(FogOfWarValueType.Visible, _fogVisibleValues);
            fow.GetFogValues(FogOfWarValueType.Partial, _fogPartialValues);
            for (int i = 0; i < _fogVisibleValues.Length; ++i)
                _pixels[i] = Color.LerpUnclamped(Color.LerpUnclamped(visibleColor, partialColor, _fogVisibleValues[i] / 255f), fogColor, _fogPartialValues[i] / 255f);

            // units
            byte opponentminvisibility = (byte)(opponentMinFogStrength * 255);
            for (int i = 0; i < FogOfWarUnit.registeredUnits.Count; ++i)
            {
                FogOfWarUnit unit = FogOfWarUnit.registeredUnits[i];
                if (unit.team == team)
                    DrawIconOnMap(fow, unit.transform.position, unitColor);
                else
                    DrawIconOnMap(fow, unit.transform.position, enemyColor, opponentminvisibility);
            }

            // camera
            if (camera != null)
                DrawIconOnMap(fow, camera.transform.position, cameraColor);

            // apply to texture
            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);
        }

        void DrawIconOnMap(FogOfWarTeam fow, Vector3 worldpos, Color color, byte maxfogamount = 255)
        {
            Vector2Int fogpos = fow.WorldPositionToFogPosition(worldpos);
            if (fogpos.x < 0 || fogpos.x >= fow.mapResolution.x ||
                fogpos.y < 0 || fogpos.y >= fow.mapResolution.y)
                return;

            if (maxfogamount < 255 && _fogVisibleValues[fow.mapResolution.y * fogpos.y + fogpos.x] > maxfogamount)
                return;

            int offset = (iconSize / 2) - 1;
            int xmin = fogpos.x - offset;
            int xmax = fogpos.x + offset;
            int ymin = fogpos.y - offset;
            int ymax = fogpos.y + offset;
            for (int y = ymin; y <= ymax; ++y)
            {
                if (y < 0 || y >= fow.mapResolution.y)
                    continue;

                for (int x = xmin; x <= xmax; ++x)
                {
                    if (x < 0 || x >= fow.mapResolution.x)
                        continue;
                    
                    _pixels[fow.mapResolution.y * y + x] = color;
                }
            }
        }
    }
}
