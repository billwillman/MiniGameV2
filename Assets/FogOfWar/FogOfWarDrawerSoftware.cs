using UnityEngine;

namespace FoW
{
    public class FogOfWarDrawerSoftware : FogOfWarDrawer
    {
        byte[] _fogValuesVisible = null;
        byte[] _fogValuesVisibleOnly = null;
        byte[] _fogValuesPartial = null;
        byte[] _fogValuesLastVisible = null;
        protected bool _isMultithreaded { get; private set; }
        float _fadeAccumulation = 0;
        byte[] _outputColors;
        Texture2D _outputTexture;
        const int _outputTextureChannels = 3;

        protected override void OnInitialise()
        {
            if (_fogValuesVisible == null || _fogValuesVisible.Length != _map.pixelCount)
            {
                OnDestroy();
                _outputTexture = new Texture2D(_map.resolution.x, _map.resolution.y, TextureFormat.RGB24, false);

                _fogValuesVisible = new byte[_map.pixelCount];
                _fogValuesVisibleOnly = new byte[_map.pixelCount];
                _fogValuesPartial = new byte[_map.pixelCount];
                _fogValuesLastVisible = null;
                _outputColors = new byte[_map.pixelCount * _outputTextureChannels];
            }

            for (int i = 0; i < _fogValuesVisible.Length; ++i)
                _fogValuesVisible[i] = 255;
            for (int i = 0; i < _fogValuesVisibleOnly.Length; ++i)
                _fogValuesVisibleOnly[i] = 255;
            for (int i = 0; i < _fogValuesPartial.Length; ++i)
                _fogValuesPartial[i] = 255;
        }

        public override void OnDestroy()
        {
            if (_outputTexture != null)
            {
                Object.Destroy(_outputTexture);
                _outputTexture = null;
            }
        }

        public override void StartFrame()
        {
            for (int i = 0; i < _fogValuesVisible.Length; ++i)
                _fogValuesVisible[i] = 255;
            for (int i = 0; i < _fogValuesVisibleOnly.Length; ++i)
                _fogValuesVisibleOnly[i] = 255;
        }

        public override void Combine(bool dofade, float fadeamount)
        {
            if (!dofade)
                return;
             
            if (_fogValuesLastVisible == null)
            {
                _fogValuesLastVisible = new byte[_fogValuesVisible.Length];
                for (int i = 0; i < _fogValuesVisible.Length; ++i)
                    _fogValuesLastVisible[i] = 255;
            }

            _fadeAccumulation += fadeamount * 255f;
            byte change = (byte)(_fadeAccumulation % 255);
            if (change == 0)
            {
                System.Array.Copy(_fogValuesLastVisible, _fogValuesVisible, _fogValuesVisible.Length);
                return;
            }

            _fadeAccumulation -= change;

            for (int i = 0; i < _fogValuesVisible.Length; ++i)
            {
                if (_fogValuesLastVisible[i] == _fogValuesVisible[i])
                    continue;

                _fogValuesVisible[i] = (byte)Mathf.MoveTowards(_fogValuesLastVisible[i], _fogValuesVisible[i], change);
                _fogValuesLastVisible[i] = _fogValuesVisible[i];
            }
        }

        public override Texture EndFrame()
        {
            // combine
            for (int i = 0; i < _map.pixelCount; ++i)
            {
                _outputColors[i * _outputTextureChannels + 0] = _fogValuesVisible[i];
                _outputColors[i * _outputTextureChannels + 1] = _fogValuesPartial[i];
                _outputColors[i * _outputTextureChannels + 2] = _fogValuesVisibleOnly[i];
            }

            _outputTexture.LoadRawTextureData(_outputColors);
            _outputTexture.Apply(false, false);

            // merge visible into partial
            for (int i = 0; i < _fogValuesVisible.Length; ++i)
                _fogValuesPartial[i] = _fogValuesVisible[i] < _fogValuesPartial[i] ? _fogValuesVisible[i] : _fogValuesPartial[i];

            return _outputTexture;
        }

        public override void GetValues(Color32[] outvalues)
        {
            for (int i = 0; i < outvalues.Length; ++i)
            {
                outvalues[i].r = _outputColors[i * _outputTextureChannels + 0];
                outvalues[i].g = _outputColors[i * _outputTextureChannels + 1];
                outvalues[i].b = _outputColors[i * _outputTextureChannels + 2];
            }
        }

        public override void SetValues(FogOfWarValueType type, byte[] values)
        {
            if (type == FogOfWarValueType.Visible)
                System.Array.Copy(values, _fogValuesVisible, _fogValuesVisible.Length);
            else if (type == FogOfWarValueType.Partial)
                System.Array.Copy(values, _fogValuesPartial, _fogValuesPartial.Length);
            else if (type == FogOfWarValueType.VisibleOnly)
                System.Array.Copy(values, _fogValuesVisibleOnly, _fogValuesVisibleOnly.Length);
        }

        void Unfog(byte[] targetTexture, int x, int y, byte v)
        {
            int index = y * _map.resolution.x + x;
            if (targetTexture[index] > v)
                targetTexture[index] = v;
        }

        public override void Draw(FogOfWarShape shape, bool ismultithreaded)
        {
            _isMultithreaded = ismultithreaded;

            if (shape is FogOfWarShapeCircle)
                DrawCircle((FogOfWarShapeCircle)shape);
            else if (shape is FogOfWarShapeBox)
            {
                FogOfWarShapeBox box = (FogOfWarShapeBox)shape;
                if (box.rotateToForward)
                    DrawRotatedBox(box);
                else
                    DrawAxisAlignedBox(box);
            }
            else if (shape is FogOfWarShapeMesh)
                DrawMesh((FogOfWarShapeMesh)shape);
        }

        void DrawCircle(FogOfWarShapeCircle shape)
        {
            byte[] targetTexture = GetTarget(shape.visibilityType);
            int fogradius = Mathf.RoundToInt(shape.radius * _map.pixelSize);
            int fogradiussqr = fogradius * fogradius;
            FogOfWarDrawInfo info = new FogOfWarDrawInfo(_map, shape);
            float lineofsightradius = shape.maxLineOfSightDistance * _map.pixelSize;

            // view angle stuff
            float dotangle = 1 - shape.angle / 90;
            bool usedotangle = dotangle > -0.99f;

            for (int y = info.yMin; y <= info.yMax; ++y)
            {
                for (int x = info.xMin; x <= info.xMax; ++x)
                {
                    // is pixel within circle radius
                    Vector2 centeroffset = new Vector2(x, y) - info.fogCenterPos;
                    if (shape.visibleCells == null && centeroffset.sqrMagnitude >= fogradiussqr)
                        continue;

                    // if in the center, just unfog, otherwise centeroffset.normalized will be pointing in a weird direction
                    if (info.fogEyePos.x == x && info.fogEyePos.y == y)
                    {
                        Unfog(targetTexture, x, y, 0);
                        continue;
                    }

                    // check if in view angle
                    if (usedotangle && Vector2.Dot(centeroffset.normalized, info.fogForward) <= dotangle)
                        continue;

                    // can see pixel
                    Vector2Int offset = new Vector2Int(x, y) - info.fogEyePos;
                    if (!shape.LineOfSightCanSee(offset, lineofsightradius, _map.lineOfSightAntiAliasing))
                        continue;

                    Unfog(targetTexture, x, y, shape.GetFalloff(centeroffset.magnitude / lineofsightradius));
                }
            }
        }

        void DrawAxisAlignedBox(FogOfWarShapeBox shape)
        {
            // convert size to fog space
            FogOfWarDrawInfo info = new FogOfWarDrawInfo(_map, shape);
            float lineofsightradius = shape.maxLineOfSightDistance * _map.pixelSize + 0.01f;

            byte[] targetTexture = GetTarget(shape.visibilityType);
            byte brightness = shape.maxBrightness;
            bool drawtexture = shape.hasTexture && !_isMultithreaded;
            for (int y = info.yMin; y <= info.yMax; ++y)
            {
                for (int x = info.xMin; x <= info.xMax; ++x)
                {
                    // can see pixel
                    Vector2Int offset = new Vector2Int(x, y) - info.fogEyePos;
                    if (!shape.LineOfSightCanSee(offset, lineofsightradius, _map.lineOfSightAntiAliasing))
                        continue;

                    // unfog
                    if (drawtexture)
                    {
                        float u = Mathf.InverseLerp(info.xMin, info.xMax, x);
                        float v = Mathf.InverseLerp(info.yMin, info.yMax, y);
                        Unfog(targetTexture, x, y, shape.SampleTexture(u, v));
                    }
                    else
                        Unfog(targetTexture, x, y, brightness);
                }
            }
        }

        void DrawRotatedBox(FogOfWarShapeBox shape)
        {
            // convert size to fog space
            FogOfWarDrawInfo info = new FogOfWarDrawInfo(_map, shape);
            float lineofsightradius = shape.maxLineOfSightDistance * _map.pixelSize;

            // rotation stuff
            Vector2 sizemul = shape.size * 0.5f * _map.pixelSize;
            Vector2 invfogsize = new Vector2(1.0f / (shape.size.x * _map.pixelSize), 1.0f / (shape.size.y * _map.pixelSize));
            float sin = Mathf.Sin(info.forwardAngle);
            float cos = Mathf.Cos(info.forwardAngle);

            byte[] targetTexture = GetTarget(shape.visibilityType);
            byte brightness = shape.maxBrightness;
            bool drawtexture = shape.hasTexture && !_isMultithreaded;
            for (int y = info.yMin; y < info.yMax; ++y)
            {
                float yy = y - info.fogCenterPos.y;

                for (int x = info.xMin; x < info.xMax; ++x)
                {
                    float xx = x - info.fogCenterPos.x;

                    // get rotated uvs
                    float u = xx * cos - yy * sin;
                    if (u < -sizemul.x || u >= sizemul.x)
                        continue;
                    float v = yy * cos + xx * sin;
                    if (v < -sizemul.y || v >= sizemul.y)
                        continue;

                    // can see pixel
                    Vector2Int offset = new Vector2Int(x, y) - info.fogEyePos;
                    if (!shape.LineOfSightCanSee(offset, lineofsightradius, _map.lineOfSightAntiAliasing))
                        continue;

                    // unfog
                    if (drawtexture)
                        Unfog(targetTexture, x, y, shape.SampleTexture(0.5f + u * invfogsize.x, 0.5f + v * invfogsize.y));
                    else
                        Unfog(targetTexture, x, y, brightness);
                }
            }
        }

        void DrawMesh(FogOfWarShapeMesh shape)
        {
            if (shape.mesh == null)
                return;

            FogOfWarDrawInfo info = new FogOfWarDrawInfo(_map, shape);
            float lineofsightradius = shape.maxLineOfSightDistance * _map.pixelSize;
            for (int i = 0; i < shape.indices.Length; i += 3)
            {
                Vector2Int v0 = shape.GetMeshVertex(_map, i + 0, in info);
                Vector2Int v1 = shape.GetMeshVertex(_map, i + 1, in info);
                Vector2Int v2 = shape.GetMeshVertex(_map, i + 2, in info);
                DrawTriangle(shape, in info, lineofsightradius, v0, v1, v2);
            }
        }

        void DrawTriangle(FogOfWarShapeMesh shape, in FogOfWarDrawInfo info, float lineofsightradius, Vector2Int v1, Vector2Int v2, Vector2Int v3)
        {
            int minX = Mathf.Min(v1.x, Mathf.Min(v2.x, v3.x));
            int maxX = Mathf.Max(v1.x, Mathf.Max(v2.x, v3.x));
            int minY = Mathf.Min(v1.y, Mathf.Min(v2.y, v3.y));
            int maxY = Mathf.Max(v1.y, Mathf.Max(v2.y, v3.y));

            // constrain to map
            minX = Mathf.Max(minX, 0);
            maxX = Mathf.Min(maxX, _map.resolution.x - 1);
            minY = Mathf.Max(minY, 0);
            maxY = Mathf.Min(maxY, _map.resolution.y - 1);

            // spanning vectors of edge (v1,v2) and (v1,v3)
            Vector2Int vs1 = new Vector2Int(v2.x - v1.x, v2.y - v1.y);
            Vector2Int vs2 = new Vector2Int(v3.x - v1.x, v3.y - v1.y);

            byte[] targetTexture = GetTarget(shape.visibilityType);
            byte brightness = shape.maxBrightness;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector2Int q = new Vector2Int(x - v1.x, y - v1.y);

                    float s = (float)FogOfWarUtils.CrossProduct(q, vs2) / FogOfWarUtils.CrossProduct(vs1, vs2);
                    float t = (float)FogOfWarUtils.CrossProduct(vs1, q) / FogOfWarUtils.CrossProduct(vs1, vs2);

                    // is within triangle
                    if (s < 0 || t < 0 || s + t > 1)
                        continue;

                    // can see pixel
                    Vector2Int offset = new Vector2Int(x, y) - info.fogEyePos;
                    if (!shape.LineOfSightCanSee(offset, lineofsightradius, _map.lineOfSightAntiAliasing))
                        continue;

                    Unfog(targetTexture, x, y, brightness);
                }
            }
        }

        byte[] GetTarget(FogOfWarValueType type)
        {
            if (type == FogOfWarValueType.Visible)
                return _fogValuesVisible;
            else if (type == FogOfWarValueType.Partial)
                return _fogValuesPartial;
            else if (type == FogOfWarValueType.VisibleOnly)
                return _fogValuesVisibleOnly;
            throw new System.ArgumentException("Invalid FogOfWarValueType.");
        }

        public override void SetFog(FogOfWarValueType type, RectInt rect, byte value)
        {
            byte[] target = GetTarget(type);
            for (int y = rect.yMin; y < rect.yMax; ++y)
            {
                for (int x = rect.xMin; x < rect.xMax; ++x)
                    target[y * _map.resolution.x + x] = value;
            }
        }

        public override void SetAll(FogOfWarValueType type, byte value)
        {
            byte[] target = GetTarget(type);
            for (int i = 0; i < target.Length; ++i)
                target[i] = value;
        }
    }
}
