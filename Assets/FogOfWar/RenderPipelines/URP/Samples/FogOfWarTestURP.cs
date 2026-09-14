using UnityEngine;
using UnityEngine.Rendering;

namespace FoW
{
    public class FogOfWarTestURP : FogOfWarTestPlatform
    {
        public Volume volume;

        public override void SetCameraTeam(Camera cam, int team)
        {
            VolumeProfile profile = volume.sharedProfile;
            FogOfWarURP fow;
            if (profile != null && profile.TryGet(out fow))
                fow.team.value = team;
        }
    }
}
