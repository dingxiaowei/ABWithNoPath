using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ABLoader : MonoBehaviour
{
    private const ulong CubeAssetId = 18223372286641534226;

    private async void Start()
    {
        // ��һ�ε��û��Զ� Initialize �������嵥
        GameObject prefab = await HashedABLoader.LoadAsync<GameObject>(CubeAssetId);
        if (prefab != null)
        {
            Instantiate(prefab);
        }
        else
        {
            Debug.LogError("��ԴΪ��");
        }
    }

}
