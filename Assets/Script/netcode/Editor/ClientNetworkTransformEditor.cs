using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;

namespace SOC.GamePlay
{

    [CustomEditor(typeof(ClientNetworkTransform))]
    public class ClientNetworkTransformEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (EditorApplication.isPlaying)
            {
                ClientNetworkTransform obj = target as ClientNetworkTransform;
                if (obj != null)
                {
                    EditorGUILayout.ToggleLeft("[HasAuthority]", obj.CanCommitToTransform);
                }
            }
        }
    }

}
