using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Slate;

namespace GAS
{

    public class DSGroup : DirectorGroup
    {
        public override string name
        {
            get
            {
                return "DS Group";
            }
            set
            { }
        }

        public override GameObject actor
        {
            get
            {
                return this.gameObject;
            }
        }
    }

}
