using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using Animancer;

[RequireComponent(typeof(AnimancerComponent))]
public class AnimancerTimeline : MonoBehaviour
{
    private AnimancerComponent m_Playable = null;

    public PlayableAssetTransition m_CurrentTimeline = null;
    public bool m_PlayOnAwake = false;

    public AnimancerComponent Playable {
        get {
            if (m_Playable == null)
                m_Playable = GetComponent<AnimancerComponent>();
            return m_Playable;
        }
    }

    private void Awake() {
        var director = GetComponent<PlayableDirector>();
        if (director != null)
            director.enabled = false;
        if (m_PlayOnAwake && m_CurrentTimeline != null) {
            var playGraph = this.Playable;
            if (playGraph != null) {
                playGraph.Play(m_CurrentTimeline);
               // m_CurrentTimeline.CreateStateAndApply(this.Playable.Playable).
            }
        }
    }
}
