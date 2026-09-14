using UnityEngine;

namespace FoW
{
    public abstract class FogOfWarShape
    {
        public Vector3 eyePosition;
        public Vector2 foward;
        public bool absoluteCenterPoint;
        public bool rotateToForward;
        public Vector2 centerPoint;
        public float[] lineOfSightRays;
        public float lineOfSightMinAngle;
        public float lineOfSightMaxAngle;
        public bool lineOfSightSeeOutsideRange;
        public float maxLineOfSightDistance;
        public bool[] visibleCells;
        public int visibleCellsWidth;
        public float radius;
        public FogOfWarValueType visibilityType;
        public float brightness;
        public byte maxBrightness;
        public Vector2 size;

        public virtual Vector2 CalculateRadius()
        {
            if (rotateToForward)
            {
                float r = size.magnitude * 0.5f;
                return new Vector2(r, r);
            }
            else
                return size * 0.5f;
        }

        public abstract byte GetVisibility(FogOfWarMap map, Vector2Int position);

        public bool LineOfSightCanSee(Vector2Int offset, float fogradius, bool lineofsightantialiasing)
        {
            return LineOfSightCanSeeRay(offset, fogradius, lineofsightantialiasing) && LineOfSightCanSeeCell(offset);
        }

        bool LineOfSightCanSeeRay(Vector2 offset, float fogradius, bool lineofsightantialiasing)
        {
            if (lineOfSightRays == null)
                return true;

            float angle = FogOfWarUtils.ClockwiseAngle(Vector2.up, offset);
            float lineofsightcoord = FogOfWarUtils.AngleInverseLerp(lineOfSightMinAngle, lineOfSightMaxAngle, angle);

            float idx = lineofsightcoord * (lineOfSightRays.Length - 1);
            if (idx < 0 || idx >= lineOfSightRays.Length || float.IsNaN(idx))
                return lineOfSightSeeOutsideRange;

            // sampling
            float value;
            if (lineofsightantialiasing)
            {
                int idxlow = Mathf.FloorToInt(idx);
                int idxhigh = (idxlow + 1) % lineOfSightRays.Length;
                value = Mathf.LerpUnclamped(lineOfSightRays[idxlow], lineOfSightRays[idxhigh], idx % 1);
            }
            else
                value = lineOfSightRays[Mathf.RoundToInt(idx) % lineOfSightRays.Length];

            float dist = value * fogradius;
            return offset.sqrMagnitude < dist * dist;
        }

        bool LineOfSightCanSeeCell(Vector2Int offset)
        {
            if (visibleCells == null)
                return true;

            // offset so it is relative to the center
            int halfwidth = visibleCellsWidth >> 1;

            offset.x += halfwidth;
            if (offset.x < 0 || offset.x >= visibleCellsWidth)
                return false;

            offset.y += halfwidth;
            if (offset.y < 0 || offset.y >= visibleCellsWidth)
                return false;

            return visibleCells[offset.y * visibleCellsWidth + offset.x];
        }
    }

    public class FogOfWarShapeCircle : FogOfWarShape
    {
        public float innerRadius;
        public float angle;

        public override Vector2 CalculateRadius()
        {
            return new Vector2(radius, radius);
        }

        public byte GetFalloff(float normdist)
        {
            if (normdist < innerRadius)
                return maxBrightness;
            float v = Mathf.InverseLerp(innerRadius, 1, normdist);
            v = 1 - (1 - v) * brightness;
            return (byte)(v * 255);
        }

        public override byte GetVisibility(FogOfWarMap map, Vector2Int position)
        {
            int fogradius = Mathf.RoundToInt(radius * map.pixelSize);
            int fogradiussqr = fogradius * fogradius;
            FogOfWarDrawInfo info = new FogOfWarDrawInfo(map, this);
            if (position.x < info.xMin || position.x > info.xMax || position.y < info.yMin || position.y > info.yMax)
                return 255;

            float lineofsightradius = maxLineOfSightDistance * map.pixelSize;

            // view angle stuff
            float dotangle = 1 - angle / 90;
            bool usedotangle = dotangle > -0.99f;

            // is pixel within circle radius
            Vector2 centeroffset = position - info.fogCenterPos;
            if (visibleCells == null && centeroffset.sqrMagnitude >= fogradiussqr)
                return 255;

            // check if in view angle
            if (usedotangle && Vector2.Dot(centeroffset.normalized, info.fogForward) <= dotangle)
                return 255;

            // can see pixel
            Vector2Int offset = position - info.fogEyePos;
            if (!LineOfSightCanSee(offset, lineofsightradius, map.lineOfSightAntiAliasing))
                return 255;

            return GetFalloff(centeroffset.magnitude / lineofsightradius);
        }
    }

    public class FogOfWarShapeBox : FogOfWarShape
    {
        public Texture2D texture;
        public bool pointSample;
        public bool hasTexture = false; // this is required for multithreading because == will use unity stuff!

        public override byte GetVisibility(FogOfWarMap map, Vector2Int position)
        {
            if (rotateToForward)
                return GetVisibilityRotated(map, position);
            else
                return GetVisibilityAxisAligned(map, position);
        }

        byte GetVisibilityRotated(FogOfWarMap _map, Vector2Int position)
        {
            // convert size to fog space
            FogOfWarDrawInfo info = new FogOfWarDrawInfo(_map, this);
            float lineofsightradius = maxLineOfSightDistance * _map.pixelSize;

            // rotation stuff
            Vector2 sizemul = size * 0.5f * _map.pixelSize;
            Vector2 invfogsize = new Vector2(1.0f / (size.x * _map.pixelSize), 1.0f / (size.y * _map.pixelSize));
            float sin = Mathf.Sin(info.forwardAngle);
            float cos = Mathf.Cos(info.forwardAngle);

            byte brightness = maxBrightness;

            float yy = position.y - info.fogCenterPos.y;
            float xx = position.x - info.fogCenterPos.x;

            // get rotated uvs
            float u = xx * cos - yy * sin;
            if (u < -sizemul.x || u >= sizemul.x)
                return 255;
            float v = yy * cos + xx * sin;
            if (v < -sizemul.y || v >= sizemul.y)
                return 255;

            // can see pixel
            Vector2Int offset = position - info.fogEyePos;
            if (!LineOfSightCanSee(offset, lineofsightradius, _map.lineOfSightAntiAliasing))
                return 255;

            // unfog
            if (hasTexture)
                return SampleTexture(0.5f + u * invfogsize.x, 0.5f + v * invfogsize.y);
            else
                return brightness;
        }

        byte GetVisibilityAxisAligned(FogOfWarMap map, Vector2Int position)
        {
            // convert size to fog space
            FogOfWarDrawInfo info = new FogOfWarDrawInfo(map, this);
            if (position.x < info.xMin || position.x > info.xMax || position.y < info.yMin || position.y > info.yMax)
                return 255;

            float lineofsightradius = maxLineOfSightDistance * map.pixelSize;

            // rotation stuff
            Vector2 sizemul = size * 0.5f * map.pixelSize;
            Vector2 invfogsize = new Vector2(1.0f / (size.x * map.pixelSize), 1.0f / (size.y * map.pixelSize));
            float sin = Mathf.Sin(info.forwardAngle);
            float cos = Mathf.Cos(info.forwardAngle);

            byte brightness = maxBrightness;

            float yy = position.y - info.fogCenterPos.y;
            float xx = position.x - info.fogCenterPos.x;

            // get rotated uvs
            float u = xx * cos - yy * sin;
            if (u < -sizemul.x || u >= sizemul.x)
                return 255;
            float v = yy * cos + xx * sin;
            if (v < -sizemul.y || v >= sizemul.y)
                return 255;

            // can see pixel
            Vector2Int offset = position - info.fogEyePos;
            if (!LineOfSightCanSee(offset, lineofsightradius, map.lineOfSightAntiAliasing))
                return 255;

            // unfog
            if (hasTexture)
                return SampleTexture(0.5f + u * invfogsize.x, 0.5f + v * invfogsize.y);
            else
                return brightness;
        }

        public byte SampleTexture(float u, float v)
        {
            // GetPixel() and GetPixelBilinear() are not supported on other threads!
            float value;
            if (pointSample)
                value = 1 - texture.GetPixel(Mathf.FloorToInt(u * texture.width), Mathf.FloorToInt(v * texture.height)).a;
            else
                value = 1 - texture.GetPixelBilinear(u, v).a;
            value = 1 - (1 - value) * brightness;
            return (byte)(value * 255);
        }
    }

    public class FogOfWarShapeMesh : FogOfWarShape
    {
        public Mesh mesh;
        public Vector3[] vertices;
        public int[] indices;

        public override byte GetVisibility(FogOfWarMap map, Vector2Int position)
        {
            if (mesh == null)
                return 255;

            FogOfWarDrawInfo info = new FogOfWarDrawInfo(map, this);
            //if (position.x < info.xMin || position.x > info.xMax || position.y < info.yMin || position.y > info.yMax)
            //    return 255;

            float lineofsightradius = maxLineOfSightDistance * map.pixelSize;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector2Int v0 = GetMeshVertex(map, i + 0, in info);
                Vector2Int v1 = GetMeshVertex(map, i + 1, in info);
                Vector2Int v2 = GetMeshVertex(map, i + 2, in info);
                if (IsInsideTriangle(map, position, in info, lineofsightradius, v0, v1, v2))
                    return maxBrightness;
            }

            return 255;
        }

        public Vector2Int GetMeshVertex(FogOfWarMap map, int index, in FogOfWarDrawInfo info)
        {
            Vector3 worldpos = vertices[indices[index]];
            Vector2 pos = FogOfWarConversion.WorldSizeToFogSize(FogOfWarConversion.WorldToFogPlane(worldpos, map.plane), map.resolution, map.size);
            pos.Scale(size);
            pos = info.Transform(this, pos);
            return pos.RoundToInt();
        }

        bool IsInsideTriangle(FogOfWarMap map, Vector2Int position, in FogOfWarDrawInfo info, float lineofsightradius, Vector2Int v1, Vector2Int v2, Vector2Int v3)
        {
            int minX = Mathf.Min(v1.x, Mathf.Min(v2.x, v3.x));
            int maxX = Mathf.Max(v1.x, Mathf.Max(v2.x, v3.x));
            int minY = Mathf.Min(v1.y, Mathf.Min(v2.y, v3.y));
            int maxY = Mathf.Max(v1.y, Mathf.Max(v2.y, v3.y));

            // constrain to map
            minX = Mathf.Max(minX, 0);
            maxX = Mathf.Min(maxX, map.resolution.x - 1);
            minY = Mathf.Max(minY, 0);
            maxY = Mathf.Min(maxY, map.resolution.y - 1);

            if (position.x < minX || position.x > maxX || position.y < minY || position.y > maxY)
                return false;

            // spanning vectors of edge (v1,v2) and (v1,v3)
            Vector2Int vs1 = new Vector2Int(v2.x - v1.x, v2.y - v1.y);
            Vector2Int vs2 = new Vector2Int(v3.x - v1.x, v3.y - v1.y);

            Vector2Int q = new Vector2Int(position.x - v1.x, position.y - v1.y);

            float s = (float)FogOfWarUtils.CrossProduct(q, vs2) / FogOfWarUtils.CrossProduct(vs1, vs2);
            float t = (float)FogOfWarUtils.CrossProduct(vs1, q) / FogOfWarUtils.CrossProduct(vs1, vs2);

            // is within triangle
            if (s < 0 || t < 0 || s + t > 1)
                return false;

            // can see pixel
            Vector2Int offset = position - info.fogEyePos;
            if (!LineOfSightCanSee(offset, lineofsightradius, map.lineOfSightAntiAliasing))
                return false;

            return true;
        }
    }
}
