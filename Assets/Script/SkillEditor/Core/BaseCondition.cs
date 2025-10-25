using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace GAS
{
    public abstract class BaseCondition // 是否是可使用
    {
        public abstract bool IsConditionOK(); // 条件是否满足
    }
}
