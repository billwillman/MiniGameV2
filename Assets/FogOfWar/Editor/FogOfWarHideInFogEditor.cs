using UnityEngine;
using UnityEditor;

namespace FoW
{
    [CustomEditor(typeof(FogOfWarHideInFog))]
    [CanEditMultipleObjects]
    public class FogOfWarHideInFogEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets.Length == 1)
            {
                GetErrors((FogOfWarHideInFog)target, FogOfWarUtils.FindObjectsOfType<FogOfWarTeam>());
                FogOfWarError.Display(false);
            }
        }

        public static void GetErrors(FogOfWarHideInFog hideinfog, FogOfWarTeam[] teams)
        {
            if (!System.Array.Exists(teams, t => t.team == hideinfog.hideFromTeam))
                FogOfWarError.Error(hideinfog, "Pointing to FogOfWarTeam index '" + hideinfog.hideFromTeam + "' that does not exist.");
        }
    }
}
