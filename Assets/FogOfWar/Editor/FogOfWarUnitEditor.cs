using UnityEngine;
using UnityEditor;

namespace FoW
{
    [CustomEditor(typeof(FogOfWarUnit))]
    [CanEditMultipleObjects]
    public class FogOfWarUnitEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Gizmos", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            FogOfWarUnit.showShapeGizmos = EditorGUILayout.Toggle("Shapes", FogOfWarUnit.showShapeGizmos);
            FogOfWarUnit.showRaycastGizmos = EditorGUILayout.Toggle("Raycasts", FogOfWarUnit.showRaycastGizmos);
            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();

            if (targets.Length == 1)
            {
                GetErrors((FogOfWarUnit)target, FogOfWarUtils.FindObjectsOfType<FogOfWarTeam>());
                FogOfWarError.Display(false);
            }
        }

        public static void GetErrors(FogOfWarUnit unit, FogOfWarTeam[] teams)
        {
            if (!System.Array.Exists(teams, t => t.team == unit.team))
                FogOfWarError.Error(unit, "Pointing to FogOfWarTeam index '" + unit.team + "' that does not exist.");

            if (unit.shapeType == FogOfWarShapeType.Mesh && unit.mesh == null)
                FogOfWarError.Warning(unit, "Shape is set to mesh, but has no mesh specified.");
        }
    }
}
