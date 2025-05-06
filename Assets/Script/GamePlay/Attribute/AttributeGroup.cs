using System.Collections;
using System.Collections.Generic;
using OD;
using UnityEngine;

namespace SOC.GamePlay.Attribute
{

   public class AttributeGroup<T>
    {
        public OrderedDictionary<ushort, T> AttributeMap = new OrderedDictionary<ushort, T>();
    }
}
