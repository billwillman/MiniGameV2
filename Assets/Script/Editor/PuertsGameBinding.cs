using System;
using System.Collections.Generic;
using Puerts;
using SOC.GamePlay;

[Configure]
public class PuertsGameBinding
{
    [Binding]
    static IEnumerable<Type> Bindings
    {
        get
        {
            return new List<Type>()
            {
                typeof(JsGameStart),
                typeof(ITSBinder),
                typeof(TSUIBinder),
            };
        }
    }
}
