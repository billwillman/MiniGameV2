using UnityEngine;
using System.Collections.Generic;
#if UNITY_2018_1_OR_NEWER
using Unity.Collections;
#endif

namespace FoW
{
    public enum FogOfWarShapeType
    {
        Circle,
        Box,
        Mesh
    }

    public enum FogOfWarLineOfSightCellTest
    {
        Center,
        NearestPoint
    }

#if UNITY_2018_1_OR_NEWER
    class FogOfWarUnitRaycasts
    {
        public NativeArray<RaycastCommand> raycasts;
        public NativeArray<RaycastHit> hits;
    }
#endif

    [AddComponentMenu("FogOfWar/FogOfWarUnit")]
    public class FogOfWarUnit : MonoBehaviour
    {
        [Tooltip("The team index that this unit belongs to. This should be the same index specified on the corresponding FogOfWarTeam component.")]
        public int team = 0;
        [Tooltip("The type of visibility for this unit. Visible will permanently clear the fog so the unit is visible. Partial will clear the fog but not make the unit visible. VisibleOnly will clear the fog so the unit is visible, but only temporarily.")]
        public FogOfWarValueType visibilityType = FogOfWarValueType.Visible;
        [Range(0, 1), Tooltip("How much this unit will affect the fog. 1 = Fully affect the fog, 0 = No affect to the fog.")]
        public float brightness = 1;
        [Tooltip("The offset of the unit's center point in world coordinates. The center point is used to cast rays and move the shape around.")]
        public Vector3 offset = Vector3.zero;

        [Header("Shape")]
        [Tooltip("The type of shape used to affect the fog for this unit.")]
        public FogOfWarShapeType shapeType = FogOfWarShapeType.Circle;
        [Tooltip("If true, the unit's position will be snapped to the center of the fog pixel.")]
        public bool snapToPixelCenter = false;
        [EnableIf(nameof(shapeType), EnableIfComparison.Equal, FogOfWarShapeType.Box, FogOfWarShapeType.Mesh), Tooltip("If true, the shape will be rotated to the forward direction of the unit. If false, it will always be aligned with the world-forward.")]
        public bool rotateToForward = false;
        [Tooltip("If true, the center point's offset will be in world-space. If false, it will be relative to the forward direction of the unit.")]
        public bool absoluteCenterPoint = false;
        [Tooltip("The center point of the unit's shape.")]
        public Vector2 centerPoint = Vector2.zero;
        [EnableIf(nameof(shapeType), EnableIfComparison.Equal, FogOfWarShapeType.Box,  FogOfWarShapeType.Mesh), Tooltip("The dimensions of the Box or Mesh shape.")]
        public Vector2 boxSize = new Vector2(5, 5);
        [EnableIf(nameof(shapeType), EnableIfComparison.Equal, FogOfWarShapeType.Circle), Tooltip("The radius of the circle shape.")]
        public float circleRadius = 5;
        [EnableIf(nameof(shapeType), EnableIfComparison.Equal, FogOfWarShapeType.Circle), Tooltip("For circle only. The percentage of the radius where the gradient starts."), Range(0.0f, 1.0f)]
        public float innerRadius = 1;
        [EnableIf(nameof(shapeType), EnableIfComparison.Equal, FogOfWarShapeType.Circle), Tooltip("For circle only. Creates a semi-circle covering this angle. 0 = Complete circle, 90 = half circle."), Range(0.0f, 180.0f)]
        public float angle = 180;
        [EnableIf(nameof(shapeType), EnableIfComparison.Equal, FogOfWarShapeType.Box), Tooltip("For box only. The texture applied over the top of the texture.")]
        public Texture2D texture;
        [EnableIf(nameof(shapeType), EnableIfComparison.Equal, FogOfWarShapeType.Mesh), Tooltip("For mesh only. The mesh to be used. This mesh should be on the XZ axis.")]
        public Mesh mesh = null;
        Mesh _lastMesh = null;
        Vector3[] _meshVertices = null;
        int[] _meshIndices = null;
        Vector2 _meshSize;

        [Header("Line of Sight")]
        [Tooltip("The layer mask used to detect objects blocking the line of sight. If set to None, line of sight will not be used.\nLine of sight will have it's 'eye' come out of the unit's origin + the offset value.")]
        public LayerMask lineOfSightMask = 0;
        [EnableIf(nameof(lineOfSightMask), EnableIfComparison.NotEqual, 0), Tooltip("How many rays will be cast to detect collision. This will only be used if cellBased is set to false.")]
        public int lineOfSightRaycastCount = 100;
        [EnableIf(nameof(lineOfSightMask), EnableIfComparison.NotEqual, 0), Tooltip("The amount of distance that the line of sight ray can penetrate into a wall. This will only be used if cellBased is set to false.")]
        public float lineOfSightPenetration = 0;
        [EnableIf(nameof(lineOfSightMask), EnableIfComparison.NotEqual, 0), Range(0, 360), Tooltip("The max angle to spread the line of sight rays in a circular direction. This will only be used if cellBased is set to false.")]
        public float lineOfSightRaycastAngle = 360;
        [EnableIf(nameof(lineOfSightMask), EnableIfComparison.NotEqual, 0), Tooltip("The angle to rotate the line of sight rays, in a circular direction. This will only be used if cellBased is set to false.")]
        public float lineOfSightRaycastAngleOffset = 0;
        [EnableIf(nameof(lineOfSightMask), EnableIfComparison.NotEqual, 0), Tooltip("When lineOfSightRayAngle is <360, will that area be visible? This will only be used if cellBased is set to false.")]
        public bool lineOfSightSeeOutsideRange = false;
#if UNITY_2018_1_OR_NEWER
        [EnableIf(nameof(lineOfSightMask), EnableIfComparison.NotEqual, 0), Tooltip("Perform the line of sight raycasts on another thread? This will only be used if cellBased is set to false.")]
        public bool multithreading = false;
        FogOfWarUnitRaycasts _multithreadRaycasts = null;
#endif
        [EnableIf(nameof(lineOfSightMask), EnableIfComparison.NotEqual, 0), Tooltip("If false, line of sight rays will be shot out in a circular direction.\nIf true, rays will shoot to the surrounding fog cells around the unit.")]
        public bool cellBased = false;
        [EnableIf(nameof(cellBased), EnableIfComparison.Equal, true), Tooltip("The point in a fog cell that will be raycasted to. This will only be used if cellBased is set to true.")]
        public FogOfWarLineOfSightCellTest cellTestPoint = FogOfWarLineOfSightCellTest.NearestPoint;
        float[] _distances = null;
        bool[] _visibleCells = null;

        Transform _transform;
        FogOfWarShape _cachedShape = null;

        static List<FogOfWarUnit> _registeredUnits = new List<FogOfWarUnit>();
        public static List<FogOfWarUnit> registeredUnits { get { return _registeredUnits; } }

        public static bool showShapeGizmos = true;
        public static bool showRaycastGizmos = true;

        void Awake()
        {
            _transform = transform;
        }

        void OnEnable()
        {
            registeredUnits.Add(this);
        }

        void CleanupMultithreadRaycasts()
        {
#if UNITY_2018_1_OR_NEWER
            if (_multithreadRaycasts == null)
                return;

            _multithreadRaycasts.raycasts.Dispose();
            _multithreadRaycasts.hits.Dispose();
            _multithreadRaycasts = null;
#endif
        }

        void OnDisable()
        {
            registeredUnits.Remove(this);
            CleanupMultithreadRaycasts();
        }

        void CalculateRaycastAngles(out float angle, out float raycastoffset, FogOfWarPlane plane)
        {
            angle = lineOfSightRaycastAngle / _distances.Length;
            raycastoffset = lineOfSightRaycastAngleOffset - lineOfSightRaycastAngle * 0.5f;
            if (rotateToForward)
                raycastoffset += FogOfWarUtils.ClockwiseAngle(Vector2.up, FogOfWarConversion.TransformFogPlaneForward(_transform, plane));
        }

        bool CalculateLineOfSight2D(Vector2 eye, float radius)
        {
            bool hashit = false;
            float angle;
            float raycastoffset;
            CalculateRaycastAngles(out angle, out raycastoffset, FogOfWarPlane.XY);
            RaycastHit2D hit;

            for (int i = 0; i <_distances.Length; ++i)
            {
                Vector2 dir = Quaternion.AngleAxis(raycastoffset + angle * i, Vector3.back) * Vector2.up;
                hit = Physics2D.Raycast(eye, dir, radius, lineOfSightMask);
                if (hit.collider != null)
                {
                    _distances[i] = (hit.distance + lineOfSightPenetration) / radius;
                    if (_distances[i] < 1)
                        hashit = true;
                    else
                        _distances[i] = 1;
                }
                else
                    _distances[i] = 1;
            }

            return hashit;
        }

        bool CalculateLineOfSight3D(Vector3 eye, float radius, float penetration, LayerMask layermask, Vector3 up, Vector3 forward, FogOfWarPlane plane)
        {
            bool hashit = false;
            float angle;
            float raycastoffset;
            CalculateRaycastAngles(out angle, out raycastoffset, plane);

#if UNITY_2018_1_OR_NEWER
            if (multithreading)
            {
                // make sure native arrays are ready
                if (_multithreadRaycasts == null)
                {
                    _multithreadRaycasts = new FogOfWarUnitRaycasts()
                    {
                        raycasts = new NativeArray<RaycastCommand>(lineOfSightRaycastCount, Allocator.Persistent),
                        hits = new NativeArray<RaycastHit>(lineOfSightRaycastCount, Allocator.Persistent)
                    };
                }
                else if (_multithreadRaycasts.raycasts.Length != lineOfSightRaycastCount)
                {
                    _multithreadRaycasts.raycasts = new NativeArray<RaycastCommand>(lineOfSightRaycastCount, Allocator.Persistent);
                    _multithreadRaycasts.hits = new NativeArray<RaycastHit>(lineOfSightRaycastCount, Allocator.Persistent);
                }

                // prepare raycasts
                for (int i = 0; i < _distances.Length; ++i)
                {
                    Vector3 dir = Quaternion.AngleAxis(raycastoffset + angle * i, up) * forward;
#if UNITY_2022_2_OR_NEWER
                    _multithreadRaycasts.raycasts[i] = new RaycastCommand(eye, dir, new QueryParameters(layermask))
                    {
                        distance = radius
                    };
#else
                    _multithreadRaycasts.raycasts[i] = new RaycastCommand(eye, dir, radius, layermask);
#endif
                }

                // perform raycasts
                RaycastCommand.ScheduleBatch(_multithreadRaycasts.raycasts, _multithreadRaycasts.hits, 1).Complete();

                // copy results
                for (int i = 0; i < _distances.Length; ++i)
                {
                    if (_multithreadRaycasts.hits[i].collider != null)
                    {
                        _distances[i] = (_multithreadRaycasts.hits[i].distance + penetration) / radius;
                        if (_distances[i] < 1)
                            hashit = true;
                        else
                            _distances[i] = 1;
                    }
                    else
                        _distances[i] = 1;
                }
            }
            else
#endif
            {
                CleanupMultithreadRaycasts();
                for (int i = 0; i < _distances.Length; ++i)
                {
                    Vector3 dir = Quaternion.AngleAxis(raycastoffset + angle * i, up) * forward;
                    RaycastHit hit;
                    if (Physics.Raycast(eye, dir, out hit, radius, layermask))
                    {
                        _distances[i] = (hit.distance + penetration) / radius;
                        if (_distances[i] < 1)
                            hashit = true;
                        else
                            _distances[i] = 1;
                    }
                    else
                        _distances[i] = 1;
                }
            }

            return hashit || lineOfSightRaycastAngle < 359f;
        }

        float[] CalculateLineOfSightRays(FogOfWarTeam fow, Vector3 eyepos, FogOfWarPlane plane, float distance)
        {
            if (fow.physics == FogOfWarPhysics.None || lineOfSightMask == 0)
                return null;

            lineOfSightRaycastCount = Mathf.Max(1, lineOfSightRaycastCount);
            if (_distances == null || _distances.Length != lineOfSightRaycastCount)
                _distances = new float[lineOfSightRaycastCount];

            if (fow.physics == FogOfWarPhysics.Physics2D)
            {
                if (CalculateLineOfSight2D(eyepos, distance))
                    return _distances;
            }
            else if (fow.physics == FogOfWarPhysics.Physics3D)
            {
                if (plane == FogOfWarPlane.XZ)
                {
                    if (CalculateLineOfSight3D(eyepos, distance, lineOfSightPenetration, lineOfSightMask, Vector3.up, Vector3.forward, plane))
                        return _distances;
                }
                else if (plane == FogOfWarPlane.XY)
                {
                    if (CalculateLineOfSight3D(eyepos, distance, lineOfSightPenetration, lineOfSightMask, Vector3.back, Vector3.up, plane))
                        return _distances;
                }
            }

            return null;
        }
        
        bool[] CalculateLineOfSightCells(FogOfWarTeam fow, Vector3 eyepos, float distance, out int visiblecellswidth)
        {
            if (fow.physics == FogOfWarPhysics.None || lineOfSightMask == 0)
            {
                visiblecellswidth = 0;
                return null;
            }

            int rad = Mathf.RoundToInt(distance * fow.mapResolution.x / fow.mapSize);
            visiblecellswidth = rad + rad + 1;
            if (_visibleCells == null || _visibleCells.Length != visiblecellswidth * visiblecellswidth)
                _visibleCells = new bool[visiblecellswidth * visiblecellswidth];

            Vector3 playerworldpos = FogOfWarConversion.WorldToFogPlane3(eyepos, fow.plane);
            for (int y = -rad; y <= rad; ++y)
            {
                for (int x = -rad; x <= rad; ++x)
                {
                    Vector3 worldoffset = GetCellOffset(x, y, fow);
                    int idx = (y + rad) * visiblecellswidth + x + rad;

                    // if it is out of range
                    if (worldoffset.magnitude > distance)
                    {
                        _visibleCells[idx] = false;
                        continue;
                    }

                    if (fow.physics == FogOfWarPhysics.Physics2D)
                        _visibleCells[idx] = Physics2D.Raycast(playerworldpos, worldoffset.normalized, Mathf.Max(worldoffset.magnitude - lineOfSightPenetration, 0.00001f), lineOfSightMask).collider == null;
                    else if (fow.physics == FogOfWarPhysics.Physics3D)
                        _visibleCells[idx] = !Physics.Raycast(eyepos, worldoffset, Mathf.Max(worldoffset.magnitude - lineOfSightPenetration, 0.00001f), lineOfSightMask);
                }
            }

            return _visibleCells;
        }

        Vector3 GetCellOffset(int x, int y, FogOfWarTeam fow)
        {
            Vector2Int offset = new Vector2Int(x, y);

            Vector2 fogoffset = offset;
            if (cellTestPoint == FogOfWarLineOfSightCellTest.NearestPoint)
            {
                // find the nearest point in the cell to the player and raycast to that point
                const float fudge = 0.1f; // bring it a little away from the collider a bit so the raycast won't hit it
                fogoffset.x -= FogOfWarUtils.Sign(offset.x) * 0.125f * (fow.mapSize + fudge) / fow.mapResolution.x;
                fogoffset.y -= FogOfWarUtils.Sign(offset.y) * 0.125f * (fow.mapSize + fudge) / fow.mapResolution.y;
            }
            Vector2 worldoffset = FogOfWarConversion.FogSizeToWorldSize(fogoffset, fow.mapResolution, fow.mapSize);
            return FogOfWarConversion.FogPlaneToWorld(worldoffset.x, worldoffset.y, 0, fow.plane);
        }

        void FillShape(FogOfWarTeam fow, FogOfWarShape shape, bool rotateToForward)
        {
            Vector3 worldpos = _transform.TransformPoint(offset);

            if (snapToPixelCenter)
            {
                // snap to nearest fog pixel
                Vector3 fogworldpos = FogOfWarConversion.WorldToFogPlane3(worldpos, fow.plane);
                Vector2 fogworldpos2 = FogOfWarConversion.SnapWorldPositionToNearestFogPixel(fow, fogworldpos);
                shape.eyePosition = FogOfWarConversion.FogPlaneToWorld(fogworldpos2.x, fogworldpos2.y, fogworldpos.z, fow.plane);
            }
            else
                shape.eyePosition = worldpos + FogOfWarConversion.FogPlaneToWorld(new Vector3(-0.5f / fow.mapResolution.x, -0.5f / fow.mapResolution.y, 0), fow.plane);
            shape.visibilityType = visibilityType;
            shape.brightness = brightness;
            shape.maxBrightness = (byte)((1 - brightness) * 255);
            shape.foward = FogOfWarConversion.TransformFogPlaneForward(_transform, fow.plane);
            shape.absoluteCenterPoint = absoluteCenterPoint;
            shape.rotateToForward = rotateToForward;
            shape.centerPoint = centerPoint;
            shape.radius = circleRadius;
            shape.size = boxSize;
            shape.lineOfSightMinAngle = lineOfSightRaycastAngleOffset - lineOfSightRaycastAngle * 0.5f;
            if (rotateToForward)
                shape.lineOfSightMinAngle += FogOfWarUtils.ClockwiseAngle(Vector2.up, FogOfWarConversion.TransformFogPlaneForward(_transform, fow.plane));
            shape.lineOfSightMaxAngle = shape.lineOfSightMinAngle + lineOfSightRaycastAngle;
            shape.lineOfSightSeeOutsideRange = lineOfSightSeeOutsideRange;
        }

        T GetShapeFromCache<T>() where T : FogOfWarShape, new()
        {
            if (_cachedShape == null || !(_cachedShape is T))
                _cachedShape = new T();
            return (T)_cachedShape;
        }

        FogOfWarShape CreateShape(FogOfWarTeam fow)
        {
            if (shapeType == FogOfWarShapeType.Circle)
            {
                rotateToForward = false;

                FogOfWarShapeCircle shape = GetShapeFromCache<FogOfWarShapeCircle>();
                FillShape(fow, shape, false);
                shape.innerRadius = innerRadius;
                shape.angle = angle;
                shape.maxLineOfSightDistance = centerPoint.magnitude + circleRadius;
                return shape;
            }
            else if (shapeType == FogOfWarShapeType.Box)
            {
                FogOfWarShapeBox shape = GetShapeFromCache<FogOfWarShapeBox>();
                shape.texture = texture;
                shape.pointSample = texture == null || texture.filterMode == FilterMode.Point;
                shape.hasTexture = texture != null;
                shape.maxLineOfSightDistance = centerPoint.magnitude + boxSize.magnitude * 0.5f;
                FillShape(fow, shape, rotateToForward);

                if (fow.renderType == FogOfWarRenderType.Software && fow.multithreaded && texture != null)
                    Debug.LogWarning("FogOfWarUnit texture shapes are not supported with multithreading.");

                return shape;
            }
            else if (shapeType == FogOfWarShapeType.Mesh)
            {
                if (mesh != _lastMesh)
                {
                    _lastMesh = mesh;
                    if (mesh == null)
                    {
                        Debug.LogError("No mesh was specified on FogOfWarUnit.", this);
                        return null;
                    }
                    if (!mesh.isReadable)
                    {
                        Debug.LogError("Mesh set on FogOfWarUnit is not readable.", this);
                        return null;
                    }

                    _meshIndices = mesh.triangles;
                    _meshVertices = mesh.vertices;
                    Bounds meshbounds = mesh.bounds;
                    Rect meshrect = Rect.MinMaxRect(meshbounds.min.x, meshbounds.min.z, meshbounds.max.x, meshbounds.max.z);
                    _meshSize = new Vector2(Mathf.Max(-meshrect.xMin, meshrect.xMax), Mathf.Max(-meshrect.yMin, meshrect.yMax));
                }

                FogOfWarShapeMesh shape = GetShapeFromCache<FogOfWarShapeMesh>();
                shape.mesh = mesh;
                shape.indices = _meshIndices;
                shape.vertices = _meshVertices;
                shape.maxLineOfSightDistance = centerPoint.magnitude + new Vector2(boxSize.x * _meshSize.x, boxSize.y * _meshSize.y).magnitude;
                FillShape(fow, shape, rotateToForward);
                return shape;
            }
            return null;
        }

        /// <summary>
        /// Returns the fog value as visible to only this unit. 0 is fully unfogged and 255 if fully fogged.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public byte GetFogValue(Vector3 position)
        {
            FogOfWarTeam fow = FogOfWarTeam.GetTeam(team);
            if (fow == null)
            {
                Debug.LogError("FogOfWarUnit.GetFogValue() failed to find FogOfWarTeam.");
                return 0;
            }

            Vector2 planeposition = FogOfWarConversion.WorldToFogPlane(position, fow.plane);
            Vector2Int fogposition = fow.WorldPositionToFogPosition(position);
            FogOfWarMap map = new FogOfWarMap(fow);
            return GetVisibility(fow, map, planeposition, fogposition);
        }

        public byte GetVisibility(FogOfWarTeam team, FogOfWarMap map, Vector2 planeposition, Vector2Int fogposition)
        {
            FogOfWarShape shape = CreateShape(team);

            // FogOfWarShape.GetVisibility() is expensive, so do a basic bounds check early
            Vector2 shapeCenter = FogOfWarConversion.WorldToFogPlane(shape.eyePosition, map.plane);
            if ((shapeCenter - planeposition).sqrMagnitude >= shape.maxLineOfSightDistance * shape.maxLineOfSightDistance)
                return 255;

            return shape.GetVisibility(map, fogposition);
        }

        public FogOfWarShape GetShape(FogOfWarTeam fow)
        {
            FogOfWarShape shape = CreateShape(fow);
            if (shape == null)
                return null;

            if (cellBased)
            {
                shape.lineOfSightRays = null;
                shape.visibleCells = CalculateLineOfSightCells(fow, shape.eyePosition, shape.maxLineOfSightDistance, out shape.visibleCellsWidth);
            }
            else
            {
                shape.lineOfSightRays = CalculateLineOfSightRays(fow, shape.eyePosition, fow.plane, shape.maxLineOfSightDistance);
                shape.visibleCells = null;
                shape.visibleCellsWidth = 0;
            }
            return shape;
        }

        void DrawLineOfSightRayGizmos(FogOfWarTeam fow, Vector3 worldpos)
        {
            if (cellBased || _distances == null || _distances.Length < 1)
                return;

            Vector3 fogorigin = FogOfWarConversion.WorldToFogPlane3(worldpos, fow.plane);
            float radius = _cachedShape.maxLineOfSightDistance;
            CalculateRaycastAngles(out float angle, out float raycastoffset, fow.plane);
            for (int i = 0; i < _distances.Length; ++i)
            {
                Vector3 dir = Quaternion.AngleAxis(raycastoffset + angle * i, Vector3.back) * Vector3.up;
                Vector3 pos = FogOfWarConversion.FogPlaneToWorld(fogorigin + dir * radius, fow.plane);
                Gizmos.DrawLine(worldpos, pos);
            }
        }

        void DrawLineOfSightCellGizmos(FogOfWarTeam fow)
        {
            if (!cellBased || _visibleCells == null || _visibleCells.Length < 1)
                return;

            int rad = Mathf.RoundToInt(_cachedShape.maxLineOfSightDistance * fow.mapResolution.x / fow.mapSize);
            int visiblecellswidth = rad + rad + 1;
            Vector3 cellsize = new Vector3(fow.mapSize / fow.mapResolution.x, fow.mapSize / fow.mapResolution.y, fow.mapSize / fow.mapResolution.y) * 1.1f; // do 1.1 to bring it away from the collider a bit so the raycast won't hit it
            for (int y = -rad; y <= rad; ++y)
            {
                for (int x = -rad; x <= rad; ++x)
                {
                    Vector3 worldoffset = GetCellOffset(x, y, fow);
                    int idx = (y + rad) * visiblecellswidth + x + rad;

                    Gizmos.color = _visibleCells[idx] ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);
                    Gizmos.DrawCube(_cachedShape.eyePosition + worldoffset, cellsize * 0.3f);
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            FogOfWarTeam fow = FogOfWarTeam.GetTeam(team);
            if (fow == null)
            {
                foreach (FogOfWarTeam t in FogOfWarUtils.FindObjectsOfType<FogOfWarTeam>(true))
                {
                    if (t.team == team)
                    {
                        fow = t;
                        break;
                    }
                }
                if (fow == null)
                    return;
            }

            if (!Application.isPlaying)
            {
                _transform = transform;
                GetShape(fow);
            }

            if (_cachedShape == null)
                return;

            if (showShapeGizmos)
            {
                Vector3 pos = FogOfWarConversion.FogPlaneToWorld(FogOfWarConversion.WorldToFogPlane3(_cachedShape.eyePosition, fow.plane) + (Vector3)_cachedShape.centerPoint, fow.plane);
                Gizmos.color = Color.red;
                if (shapeType == FogOfWarShapeType.Circle)
                    Gizmos.DrawWireSphere(pos, circleRadius);
                else if (shapeType == FogOfWarShapeType.Box)
                    Gizmos.DrawWireCube(pos, new Vector3(boxSize.x, boxSize.y, boxSize.y));
            }

            if (showRaycastGizmos && lineOfSightMask != 0)
            {
                DrawLineOfSightRayGizmos(fow, _cachedShape.eyePosition);
                DrawLineOfSightCellGizmos(fow);
            }
        }
    }
}
