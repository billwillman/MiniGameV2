using UnityEngine;
using UnityEngine.Events;

namespace FoW
{
    [AddComponentMenu("FogOfWar/FogOfWarEvents")]
    public class FogOfWarEvents : MonoBehaviour
    {
        [Tooltip("The team index that should be checked. This should be the same index specified on the corresponding FogOfWarTeam component.")]
        public int team = 0;
        [Tooltip("The type of visibility that should be compared with.")]
        public FogOfWarValueType visibilityType = FogOfWarValueType.Visible;
        [Range(0.0f, 1.0f), Tooltip("The fog threshold that will trigger the object to show/hide. A lower value will be more visible in higher fog values.")]
        public float minFogStrength = 0.5f;
        [Tooltip("Triggered when this object enters the fog.")]
        public UnityEvent onFogEnter;
        [Tooltip("Triggered when this object exits the fog.")]
        public UnityEvent onFogExit;

        bool _isInFog = false;
        Transform _transform;

        void Start()
        {
            _transform = transform;
        }

        void Update()
        {
            bool isinfog = FogOfWarTeam.GetTeam(team).GetFogValue(visibilityType, _transform.position) > (byte)(minFogStrength * 255);
            if (_isInFog == isinfog)
                return;

            _isInFog = !_isInFog;

            if (_isInFog)
                onFogEnter.Invoke();
            else
                onFogExit.Invoke();
        }
    }
}
