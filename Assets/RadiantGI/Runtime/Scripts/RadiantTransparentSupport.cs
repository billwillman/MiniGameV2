using UnityEngine;

namespace RadiantGI.Universal {

    [ExecuteAlways]
    public class RadiantTransparentSupport : MonoBehaviour {

        [HideInInspector]
        public Renderer theRenderer;

        public void OnEnable () {
            if (theRenderer == null) {
                theRenderer = GetComponent<Renderer>();
            }
            RadiantRenderFeature.RegisterTransparentSupport(this);
        }

        public void OnDisable () {
            RadiantRenderFeature.UnregisterTransparentSupport(this);
        }

    }
}
