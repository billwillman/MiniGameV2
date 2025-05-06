using System;
using System.Collections;
using System.Collections.Generic;
using OD;
using UnityEngine;

namespace SOC.GamePlay.Attribute
{
    public class AttributeComponent : MonoBehaviour
    {
        // Êý¾ÝÃæ°å
        public OrderedDictionary<string, AttributeGroup<int>> IntValueBoard = new OrderedDictionary<string, AttributeGroup<int>>();
        public OrderedDictionary<string, AttributeGroup<float>> FloatValueBoard = new OrderedDictionary<string, AttributeGroup<float>>();
        public OrderedDictionary<string, AttributeGroup<string>> StringValueBoard = new OrderedDictionary<string, AttributeGroup<string>>();
        public OrderedDictionary<string, AttributeGroup<long>> LongValueBoard = new OrderedDictionary<string, AttributeGroup<long>>();
        //---------
    }
}
