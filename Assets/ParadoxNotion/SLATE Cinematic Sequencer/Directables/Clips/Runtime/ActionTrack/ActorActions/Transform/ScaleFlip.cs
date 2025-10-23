using UnityEngine;

namespace Slate.ActionClips
{

    [Category("Transform")]
    [Description("Flips the object by invert scaling it. Useful for 2D objects where commonly sprites are facing to the right by default.")]
    public class ScaleFlip : ActorActionClip
    {

        public bool flipX = true;
        public bool flipY;

        private Vector3 originalScale;

        public override string info => string.Format("Flip\n{0} {1}", flipX ? "X" : string.Empty, flipY ? "Y" : string.Empty);

        protected override void OnEnter() {
            originalScale = actor.transform.localScale;

            var targetScale = originalScale;

            if ( flipX ) {
                targetScale.x *= -1;
            }
            if ( flipY ) {
                targetScale.y *= -1;
            }

            actor.transform.localScale = targetScale;
        }

        protected override void OnReverse() {
            actor.transform.localScale = originalScale;
        }
    }
}