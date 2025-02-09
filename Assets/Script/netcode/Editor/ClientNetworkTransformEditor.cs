using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Editor;

namespace SOC.GamePlay
{

    [CustomEditor(typeof(ClientNetworkTransform))]
    public class ClientNetworkTransformEditor : NetworkTransformEditor
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
