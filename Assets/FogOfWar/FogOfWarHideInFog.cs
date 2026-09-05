using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace FoW
{
    [AddComponentMenu("FogOfWar/FogOfWarHideInFog")]
    public class FogOfWarHideInFog : MonoBehaviour
    {
        [Tooltip("The team index that should be tested against. This should be the same index specified on the corresponding FogOfWarTeam component."), FormerlySerializedAs("team")]
        public int hideFromTeam = 0;
        [Tooltip("The type of visibility that should be compared with.")]
        public FogOfWarValueType visibilityType = FogOfWarValueType.Visible;
        [Range(0.0f, 1.0f), Tooltip("The fog threshold that will trigger the object to show/hide. A lower value will be more visible in higher fog values.")]
        public float minFogStrength = 0.5f;
        
        Transform _transform;
        Renderer _renderer;
        Graphic _graphic;
        Canvas _canvas;

        void Start()
        {
            _transform = transform;
            _renderer = GetComponent<Renderer>();
            _graphic = GetComponent<Graphic>();
            _canvas = GetComponent<Canvas>();
        }

        void Update()
        {
            FogOfWarTeam fow = FogOfWarTeam.GetTeam(hideFromTeam);
            if (fow == null)
            {
                Debug.LogWarning("There is no Fog Of War team for team #" + hideFromTeam.ToString());
                return;
            }

            bool visible = fow.GetFogValue(visibilityType, _transform.position) < minFogStrength * 255;
            if (_renderer != null)
                _renderer.enabled = visible;
            if (_graphic != null)
                _graphic.enabled = visible;
            if (_canvas != null)
                _canvas.enabled = visible;
        }
    }
}
