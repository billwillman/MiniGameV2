using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;
using Animancer;

[TrackClipType(typeof(AnimancerTrackAsset))]
public class AnimancerTrack : TrackAsset
{
    public AnimancerComponent m_Animancer = null;
}
