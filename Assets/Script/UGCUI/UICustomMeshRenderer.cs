using UnityEngine.UI;
using UnityEngine;

namespace SOC.UI
{
    [RequireComponent(typeof(MeshRenderer))]
    public class UICustomMeshRenderer : MonoBehaviour
    {
        public int m_RelativeSortOrder = 0;
        public Canvas m_Canvas = null;
        private MeshRenderer m_MeshRenderer;

        public MeshRenderer CurrentMeshRenderer {
            get {
                if (m_MeshRenderer == null)
                    m_MeshRenderer = GetComponent<MeshRenderer>();
                return m_MeshRenderer;
            }
        }
        private void Awake() {
            if (m_Canvas != null) {
                var meshRenderer = CurrentMeshRenderer;
                if (meshRenderer != null) {
                    meshRenderer.sortingOrder = m_Canvas.sortingOrder + m_RelativeSortOrder;
                }
            }
        }
    }
}
