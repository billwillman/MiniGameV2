using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using Animancer;

[System.Serializable]
public class AnimancerTrackAsset : PlayableAsset
{
    public ClipTransition m_AnimationClip = null;
    // Factory method that generates a playable based on this asset
    public override Playable CreatePlayable(PlayableGraph graph, GameObject go)
    {
        var scriptPlayable = ScriptPlayable<AnimancerBehaviour>.Create(graph, 1);
        return scriptPlayable;
    }
}