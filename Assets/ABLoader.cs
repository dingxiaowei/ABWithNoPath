using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ABLoader : MonoBehaviour
{
    private const ulong CubeAssetId = 18223372286641534226;

    private async void Start()
    {
        // 第一次调用会自动 Initialize 并加载清单
        GameObject prefab = await HashedABLoader.LoadAsync<GameObject>(CubeAssetId);
        if (prefab != null)
        {
            Instantiate(prefab);
        }
        else
        {
            Debug.LogError("资源为空");
        }
    }

}
