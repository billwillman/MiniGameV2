using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Slate;

namespace GAS
{
    [Attachable(typeof(CommonGroup), typeof(ClientGroup), typeof(DSGroup))]
    [Name("CommonTaskTrack")]
    public class CommonTaskTrack : CutsceneTrack
    {}

}
