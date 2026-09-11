using UnityEngine;
using UnityEngine.Rendering;

namespace RadiantGI.Universal
{
    public class ToggleEffect : MonoBehaviour
    {

        public VolumeProfile profile;

        RadiantGlobalIllumination radiant;

        void OnEnable() {
            InputProxy.SetupEventSystem();
        }

        void Start()
        {
            profile.TryGet(out radiant);
            if (radiant != null) radiant.active = true;
        }


        void Update()
        {
            if (InputProxy.GetKeyDown(KeyCode.Space))
            {
                radiant.active = !radiant.active;
            }
        }
    }


}

