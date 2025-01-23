using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
}
