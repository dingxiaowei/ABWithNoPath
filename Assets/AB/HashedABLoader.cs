using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public static class HashedABLoader
{
    private const string OutputRootUnderStreamingAssets = "bundles";
    private const string IndexAssetResourcesPath = "HashedBundleIndex";

    private static readonly Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();
    private static AssetBundleManifest manifest;
    private static BundleIndex index;
    private static string bundlesRoot;

    public static void Initialize()
    {
        if (index == null)
        {
            index = Resources.Load<BundleIndex>(IndexAssetResourcesPath);
            if (index == null)
            {
                throw new Exception($"Missing Resources/{IndexAssetResourcesPath}.asset. Build bundles first.");
            }
        }
        if (bundlesRoot == null)
        {
            string platform = GetPlatformFolderName();
            bundlesRoot = Path.Combine(Application.streamingAssetsPath, OutputRootUnderStreamingAssets, platform);
        }
        if (manifest == null)
        {
            string manifestBundlePath = Path.Combine(bundlesRoot, GetPlatformFolderName());
            var rootBundle = AssetBundle.LoadFromFile(manifestBundlePath);
            if (rootBundle == null) throw new Exception("Failed to load root manifest bundle: " + manifestBundlePath);
            manifest = rootBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            rootBundle.Unload(unloadAllLoadedObjects: false);
        }
    }

    public static async Task<T> LoadAsync<T>(ulong assetId) where T : UnityEngine.Object
    {
        Initialize();
        if (!index.TryGetBundleId(assetId, out ulong bundleId)) throw new Exception($"AssetId not found in index: 0x{assetId:x16}");
        string bundleName = HashUtil.ToLowerHex16(bundleId);
        await LoadBundleWithDependenciesAsync(bundleName);
        string address = HashUtil.ToLowerHex16(assetId);
        AssetBundle ab = loadedBundles[bundleName];
        var req = ab.LoadAssetAsync<T>(address);
        await Awaiter(req);
        return req.asset as T;
    }

    public static T Load<T>(ulong assetId) where T : UnityEngine.Object
    {
        Initialize();
        if (!index.TryGetBundleId(assetId, out ulong bundleId)) throw new Exception($"AssetId not found in index: 0x{assetId:x16}");
        string bundleName = HashUtil.ToLowerHex16(bundleId);
        LoadBundleWithDependencies(bundleName);
        string address = HashUtil.ToLowerHex16(assetId);
        AssetBundle ab = loadedBundles[bundleName];
        return ab.LoadAsset<T>(address);
    }

    public static void UnloadAll(bool unloadAllLoadedObjects = false)
    {
        foreach (var kv in loadedBundles)
        {
            kv.Value.Unload(unloadAllLoadedObjects);
        }
        loadedBundles.Clear();
    }

    private static async Task LoadBundleWithDependenciesAsync(string bundleName)
    {
        if (loadedBundles.ContainsKey(bundleName)) return;
        string[] deps = manifest.GetAllDependencies(bundleName);
        for (int i = 0; i < deps.Length; i++)
        {
            await LoadBundleAsync(deps[i]);
        }
        await LoadBundleAsync(bundleName);
    }

    private static void LoadBundleWithDependencies(string bundleName)
    {
        if (loadedBundles.ContainsKey(bundleName)) return;
        string[] deps = manifest.GetAllDependencies(bundleName);
        for (int i = 0; i < deps.Length; i++)
        {
            LoadBundle(deps[i]);
        }
        LoadBundle(bundleName);
    }

    private static async Task LoadBundleAsync(string bundleName)
    {
        if (loadedBundles.ContainsKey(bundleName)) return;
        string path = Path.Combine(bundlesRoot, bundleName);
        var req = AssetBundle.LoadFromFileAsync(path);
        await Awaiter(req);
        loadedBundles[bundleName] = req.assetBundle;
    }

    private static void LoadBundle(string bundleName)
    {
        if (loadedBundles.ContainsKey(bundleName)) return;
        string path = Path.Combine(bundlesRoot, bundleName);
        AssetBundle ab = AssetBundle.LoadFromFile(path);
        if (ab == null) throw new Exception("Failed to load bundle: " + path);
        loadedBundles[bundleName] = ab;
    }

    private static string GetPlatformFolderName()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.WindowsEditor:
                return "StandaloneWindows64";
            case RuntimePlatform.Android:
                return "Android";
            case RuntimePlatform.IPhonePlayer:
                return "iOS";
            case RuntimePlatform.WebGLPlayer:
                return "WebGL";
            default:
                return Application.platform.ToString();
        }
    }

    private static Task Awaiter(AssetBundleCreateRequest req)
    {
        var tcs = new TaskCompletionSource<bool>();
        req.completed += _ => tcs.SetResult(true);
        return tcs.Task;
    }

    private static Task Awaiter(AssetBundleRequest req)
    {
        var tcs = new TaskCompletionSource<bool>();
        req.completed += _ => tcs.SetResult(true);
        return tcs.Task;
    }
}


