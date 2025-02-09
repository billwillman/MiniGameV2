using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;

namespace SOC.GamePlay
{
    [CustomEditor(typeof(PlayerController))]
    public class PlayerControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (EditorApplication.isPlaying)
            {
                PlayerController obj = target as PlayerController;
                if (obj != null)
                {
                    EditorGUILayout.LongField("[OwnerClientId]", (long)obj.OwnerClientId);
                    if (obj.PawnId.CanClientRead(obj.OwnerClientId))
                    {
                        EditorGUILayout.LongField("[PawnId]", (long)obj.PawnId.Value);
                    }
                    if (obj.Pawn != null && obj.Pawn.IsSpawned)
                    {
                        EditorGUILayout.ObjectField("[Pawn]", obj.Pawn, typeof(NetworkObject), true);
                    }
                }
            }
        }
    }
}
