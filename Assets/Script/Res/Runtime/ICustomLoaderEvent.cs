using UnityEngine;

public interface ICustomLoaderEvent
{
    MonoBehaviour CustomLoaderBehaviour {
        get;
    }

    bool OnLoaded(UnityEngine.Object targetRes, BaseResLoaderAsyncType asyncType, string resName, string tag);
}
