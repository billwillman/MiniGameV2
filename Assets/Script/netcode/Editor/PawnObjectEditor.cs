using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SOC.GamePlay
{
    [CustomEditor(typeof(PawnNetworkObject))]
    public class PawnObjectEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
        }
    }

}
