using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SOC.GamePlay
{

    public class TestMainRun : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            TimerMgr.Instance.ScaleTick(Time.deltaTime);
            TimerMgr.Instance.UnScaleTick(Time.unscaledDeltaTime);
        }
    }

}
