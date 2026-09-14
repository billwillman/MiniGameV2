using UnityEngine;

namespace FoW
{
    [System.Serializable]
    public abstract class FogOfWarDrawer
    {
        protected FogOfWarMap _map;

        public void Initialise(FogOfWarMap map)
        {
            _map = map;
            OnInitialise();
        }

        protected abstract void OnInitialise();
        public abstract void OnDestroy();
        public abstract void StartFrame();
        public abstract void Combine(bool dofade, float fadeamount);
        public abstract Texture EndFrame();
        public abstract void GetValues(Color32[] outvalues);
        public abstract void SetValues(FogOfWarValueType type, byte[] values);
        public abstract void Draw(FogOfWarShape shape, bool ismultithreaded);
        public abstract void SetFog(FogOfWarValueType type, RectInt rect, byte value);
        public abstract void SetAll(FogOfWarValueType type, byte value);
    }

    public struct FogOfWarDrawInfo
    {
        public Vector2 fogCenterPos;
        public Vector2Int fogEyePos;
        public Vector2 fogForward;
        public float forwardAngle;
        float _sin;
        float _cos;
        public int xMin;
        public int xMax;
        public int yMin;
        public int yMax;

        public FogOfWarDrawInfo(FogOfWarMap map, FogOfWarShape shape)
        {
            // convert size to fog space
            fogForward = shape.foward;
            if (shape.rotateToForward)
            {
                forwardAngle = FogOfWarUtils.ClockwiseAngle(Vector2.up, fogForward) * Mathf.Deg2Rad;
                _sin = Mathf.Sin(-forwardAngle);
                _cos = Mathf.Cos(-forwardAngle);
            }
            else
            {
                forwardAngle = 0;
                _sin = 0;
                _cos = 1;
            }

            Vector2 relativeoffset;
            if (shape.absoluteCenterPoint)
            {
                relativeoffset = shape.centerPoint;
            }
            else
            {
                float angle = shape.rotateToForward ? -forwardAngle : FogOfWarUtils.ClockwiseAngle(Vector2.up, fogForward) * -Mathf.Deg2Rad;
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);
                relativeoffset = new Vector2(shape.centerPoint.x * cos - shape.centerPoint.y * sin, shape.centerPoint.x * sin + shape.centerPoint.y * cos);
            }

            // Subtract 0.5 here because the eye/center needs to be in the center of the fog pixel
            fogCenterPos = FogOfWarConversion.WorldToFog(FogOfWarConversion.WorldToFogPlane(shape.eyePosition, map.plane) + relativeoffset, map.offset, map.resolution, map.size) - new Vector2(0.5f, 0.5f);
            fogEyePos = (FogOfWarConversion.WorldToFog(shape.eyePosition, map.plane, map.offset, map.resolution, map.size) - new Vector2(0.5f, 0.5f)).ToInt();

            // find ranges
            Vector2 radius = shape.CalculateRadius() * map.pixelSize;
            if (shape.visibleCells == null)
            {
                xMin = Mathf.Max(0, Mathf.RoundToInt(fogCenterPos.x - radius.x));
                xMax = Mathf.Min(map.resolution.x - 1, Mathf.RoundToInt(fogCenterPos.x + radius.x));
                yMin = Mathf.Max(0, Mathf.RoundToInt(fogCenterPos.y - radius.y));
                yMax = Mathf.Min(map.resolution.y - 1, Mathf.RoundToInt(fogCenterPos.y + radius.y));
            }
            else
            {
                Vector2Int pos = fogCenterPos.ToInt();
                Vector2Int rad = radius.ToInt();
                xMin = Mathf.Max(0, pos.x - rad.x);
                xMax = Mathf.Min(map.resolution.x - 1, pos.x + rad.x);
                yMin = Mathf.Max(0, pos.y - rad.y);
                yMax = Mathf.Min(map.resolution.y - 1, pos.y + rad.y);
            }
        }

        public Vector2 Transform(FogOfWarShape shape, Vector2 pos)
        {
            if (shape.rotateToForward)
                pos = new Vector2(pos.x * _cos - pos.y * _sin, pos.x * _sin + pos.y * _cos);
            pos += fogCenterPos;
            return pos;
        }
    }
}
