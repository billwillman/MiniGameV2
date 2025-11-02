using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Slate;

namespace GAS
{

    public class ClientGroup : DirectorGroup
    {
        public override string name
        {
            get
            {
                return "Client Group";
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
