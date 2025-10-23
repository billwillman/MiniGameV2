using UnityEngine;
using System.Linq;
using UnityEngine.AI;

namespace Slate.ActionClips
{

    [Category("Paths")]
    [Description("For this clip to work you only need to have a baked NavMesh. The actor does NOT need, or use a NavMeshAgent Component. The length of the clip is determined by the path's length and the speed parameter set, while the Blend In parameter is used only for the initial look ahead blending")]
    public class PathfindTo : ActorActionClip
    {

        [SerializeField, HideInInspector]
        private float _blendIn = 0.5f;
        [SerializeField, HideInInspector]
        private float _blendOut;

        [Min(0.01f)]
        public float speed = 3f;
        public TransformRefPosition targetPosition;

        public EaseType blendInterpolation = EaseType.QuadraticInOut;

        private Vector3[] pathPoints;

        private Vector3 wasPosition;
        private Quaternion wasRotation;

        private Vector3 lastFrom;
        private Vector3 lastTo;

        private bool lockCalc;

        public override string info {
            get { return string.Format("Pathfind To\n{0}", targetPosition.ToString()); }
        }

        public override float length {
            get
            {
                if ( isValid ) {
                    if ( !lockCalc ) {
                        TryCalculatePath();
                    }
                    return Path.GetLength(pathPoints) / speed;
                }
                return 0;
            }
        }

        public override float blendIn {
            get { return _blendIn; }
            set { _blendIn = value; }
        }

        public override float blendOut {
            get { return _blendOut; }
            set { _blendOut = value; }
        }

        protected override void OnEnter() {
            lockCalc = true;
            pathPoints = null; //re-calc
            TryCalculatePath();
            wasPosition = actor.transform.position;
            wasRotation = actor.transform.rotation;
            if ( pathPoints == null || pathPoints.Length == 0 ) {
                Debug.LogWarning(string.Format("Actor '{0}' can't pathfind to '{1}'", actor.name, targetPosition.value), actor);
            }
        }

        protected override void OnUpdate(float time) {
            if ( pathPoints != null && pathPoints.Length > 1 ) {

                if ( length == 0 ) {
                    actor.transform.position = Path.GetPosition(0, pathPoints);
                    return;
                }

                //POSITION
                var pos = Path.GetPosition(time / length, pathPoints);
                if ( NavMesh.SamplePosition(pos, out NavMeshHit hit, 10f, -1) ) {
                    pos.y = hit.position.y;
                }
                actor.transform.position = Easing.Ease(blendInterpolation, wasPosition, pos, GetClipWeight(time));


                //ROTATION
                var lookPos = Path.GetPosition(( time / length ) + 0.01f, pathPoints);
                var dir = lookPos - actor.transform.position;
                if ( dir.magnitude > 0.001f ) {
                    var lookRot = Quaternion.LookRotation(dir, Vector3.up);
                    actor.transform.rotation = Easing.Ease(blendInterpolation, wasRotation, lookRot, GetClipWeight(time));
                }
            }
        }

        protected override void OnReverse() {
            lockCalc = false;
            actor.transform.position = wasPosition;
            actor.transform.rotation = wasRotation;
            pathPoints = null;
        }

        void TryCalculatePath() {
            var pos = TransformPosition(targetPosition.value, targetPosition.space);
            if ( pathPoints == null || lastFrom != actor.transform.position || lastTo != pos ) {
                var navPath = new NavMeshPath();
                NavMesh.CalculatePath(actor.transform.position, pos, -1, navPath);
                pathPoints = navPath.corners.ToArray();
            }
            lastFrom = actor.transform.position;
            lastTo = pos;
        }


        ///----------------------------------------------------------------------------------------------
        ///---------------------------------------UNITY EDITOR-------------------------------------------
#if UNITY_EDITOR

        protected override void OnDrawGizmosSelected() {

            var pos = TransformPosition(targetPosition.value, targetPosition.space);
            Gizmos.DrawSphere(pos, 0.2f);

            if ( pathPoints != null && pathPoints.Length > 1 ) {
                Gizmos.color = Color.red;
                for ( int i = 0; i < pathPoints.Length; i++ ) {
                    var a = pathPoints[i];
                    var b = ( i == pathPoints.Length - 1 ) ? pathPoints[i] : pathPoints[i + 1];
                    Gizmos.DrawLine(a, b);
                }
                Gizmos.color = Color.white;
            }
        }

        protected override void OnSceneGUI() {
            var value = targetPosition.value;
            DoVectorPositionHandle(targetPosition.space, ref value);
            targetPosition.value = value;
        }

#endif

    }
}