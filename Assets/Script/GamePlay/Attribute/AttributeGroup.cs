using System.Collections;
using System.Collections.Generic;
using OD;
using UnityEngine;

namespace SOC.GamePlay.Attribute
{

   public class AttributeGroup<T>
    {
        public OrderedDictionary<string, T> AttributeMap = new OrderedDictionary<string, T>();
    }
}
