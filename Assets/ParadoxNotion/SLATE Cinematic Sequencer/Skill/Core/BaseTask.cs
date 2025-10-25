using Slate;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GAS
{
    // 基本类
    //[Attachable(typeof(CommonGroup))]
    //[Name("BaseTask")]
    public class BaseTask: CutsceneTrack
    {
        public bool CanTimeSkip = false; // 能否时间过了跳过，默认不可以
    }

}
