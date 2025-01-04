using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SOC.GamePlay
{
    public abstract class BaseMonoBehaviour : MonoBehaviour
    {
        private bool m_IsDestroyed = false;
        protected void DoDestroy() {
            if (m_IsDestroyed)
                return;
            m_IsDestroyed = true;
            OnInternalDestroyed();
        }

        protected virtual void OnInternalDestroyed() {

        }
        private void OnApplicationQuit() {
            DoDestroy();
        }

        private void OnDestroy() {
            DoDestroy();
        }

    }
}
