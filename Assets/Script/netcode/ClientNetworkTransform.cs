using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode.Components;

namespace SOC.GamePlay
{

    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
