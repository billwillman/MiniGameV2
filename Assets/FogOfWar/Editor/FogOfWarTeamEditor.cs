using UnityEngine;
using UnityEditor;

namespace FoW
{
    [CustomEditor(typeof(FogOfWarTeam))]
    [CanEditMultipleObjects]
    public class FogOfWarTeamEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (targets.Length == 1)
            {
                FogOfWarTeam team = (FogOfWarTeam)target;

                GetErrors(team, FogOfWarUtils.FindObjectsOfType<FogOfWarTeam>());
                FogOfWarError.Display(false);

                if (team.renderType == FogOfWarRenderType.Software && team.multithreaded)
                    EditorGUILayout.HelpBox("Note: Multithreading does not support textures shapes on the FogOfWarUnit component.", MessageType.Info);
            }

            foreach (FogOfWarTeam team in targets)
            {
                team.EnsureTextureReaderShaderExists();
                if (team.blurIterations <= 0)
                {
                    team.blurIterations = 1;
                    EditorUtility.SetDirty(team);
                }
            }

            if (Application.isPlaying)
            {
                foreach (FogOfWarTeam team in targets)
                {
                    if (team.fogTexture != null)
                        EditorGUILayout.ObjectField("Output Texture", team.fogTexture, typeof(Texture), false);
                }

                if (GUILayout.Button("Reinitialize"))
                {
                    foreach (Object objtarget in targets)
                        ((FogOfWarTeam)objtarget).Reinitialize();
                }
                if (CanDoManualUpdate() && GUILayout.Button("Manual Update"))
                {
                    foreach (Object objtarget in targets)
                        ((FogOfWarTeam)objtarget).ManualUpdate(1);
                }
            }
        }

        bool CanDoManualUpdate()
        {
            foreach (Object objtarget in targets)
            {
                FogOfWarTeam team = (FogOfWarTeam)objtarget;
                if (!team.updateAutomatically || !team.updateUnits)
                    return true;
            }
            return false;
        }

        public static void GetErrors(FogOfWarTeam team, FogOfWarTeam[] teams)
        {
            foreach (FogOfWarTeam otherteam in teams)
            {
                if (otherteam != team && otherteam.team == team.team)
                {
                    FogOfWarError.Error(team, "There are multiple FogOfWarTeam components with the same team index (" + team.team.ToString() + ")!");
                    break;
                }
            }

            if (team.mapResolution.x <= 0 || team.mapResolution.y <= 0)
                FogOfWarError.Error(team, "Cannot have a zero map resolution!");
            if (team.mapSize < 0.0001f)
                FogOfWarError.Error(team, "Cannot have a zero map size!");
            if (team.physics == FogOfWarPhysics.Physics2D && team.plane != FogOfWarPlane.XY)
                FogOfWarError.Warning(team, "Using Physics2D, but is on a non-XY plane. You probably don't want to do this.");
            if (team.renderType == FogOfWarRenderType.Hardware && team.multithreaded)
                FogOfWarError.Warning(team, "Multithreading will have no effect in hardware mode!");
        }
    }
}
