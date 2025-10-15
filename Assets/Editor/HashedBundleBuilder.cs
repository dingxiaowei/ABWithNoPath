using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class HashedBundleBuilder
{
	private const string DefaultSourceRoot = "Assets/ABSource"; // Change to your asset root
	private const string OutputRootUnderStreamingAssets = "bundles";
	private const string IndexAssetResourcesPath = "HashedBundleIndex"; // Resources/HashedBundleIndex.asset

	[MenuItem("Tools/Build/Build Hashed AssetBundles (FNV-1a 64)")]
	public static void BuildAll()
	{
		string sourceRoot = DefaultSourceRoot;
		if (!AssetDatabase.IsValidFolder(sourceRoot))
		{
			Debug.LogWarning($"Source root '{sourceRoot}' not found. Creating it. Put assets under this folder.");
			CreateFolderRecursive(sourceRoot);
			AssetDatabase.Refresh();
		}

		string outputRoot = Path.Combine(Application.streamingAssetsPath, OutputRootUnderStreamingAssets);
		CreateDirectoryIfNotExists(outputRoot);

		BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;
		BuildAssetBundleOptions options = BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DeterministicAssetBundle;

		var (builds, bundleIdByBundleName, assetIdToBundleId) = CreateBuildMap(sourceRoot);
		string platformDir = GetPlatformFolderName(buildTarget);
		string outputPath = Path.Combine(outputRoot, platformDir);
		CreateDirectoryIfNotExists(outputPath);

		AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(outputPath, builds.ToArray(), options, buildTarget);
		if (manifest == null)
		{
			throw new Exception("BuildAssetBundles failed: manifest is null.");
		}

		WriteOrUpdateIndex(assetIdToBundleId);

		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log($"Hashed AssetBundles built to: {outputPath}");
	}

	private static (List<AssetBundleBuild> builds, Dictionary<string, ulong> bundleIdByBundleName, Dictionary<ulong, ulong> assetIdToBundleId) CreateBuildMap(string sourceRoot)
	{
		string[] allGuids = AssetDatabase.FindAssets("t:Object", new[] { sourceRoot });
		var groups = new Dictionary<string, List<(string assetPath, string guid)>>();

		foreach (string guid in allGuids)
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			if (Directory.Exists(path)) continue; // skip folders
			if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue; // skip scripts
			if (path.StartsWith("Assets/Editor", StringComparison.OrdinalIgnoreCase)) continue; // skip editor-only

			string relDir = GetRelativeDirectoryUnderRoot(sourceRoot, path);
			if (!groups.TryGetValue(relDir, out var list))
			{
				list = new List<(string assetPath, string guid)>();
				groups[relDir] = list;
			}
			list.Add((path, guid));
		}

		var builds = new List<AssetBundleBuild>(groups.Count);
		var bundleIdByBundleName = new Dictionary<string, ulong>(groups.Count);
		var assetIdToBundleId = new Dictionary<ulong, ulong>(capacity: allGuids.Length);

		foreach (var kv in groups)
		{
			string relDir = kv.Key; // e.g., "Characters/Orc"
			ulong bundleId = HashUtil.ComputeHash64(relDir.Replace('\\', '/'));
			string bundleName = ToLowerHex16(bundleId);
			bundleIdByBundleName[bundleName] = bundleId;

			var assetPaths = kv.Value.Select(v => v.assetPath).ToArray();
			var addressableNames = new string[assetPaths.Length];

			for (int i = 0; i < assetPaths.Length; i++)
			{
				string guid = kv.Value[i].guid;
				ulong assetId = HashUtil.ComputeHash64(guid);
				addressableNames[i] = ToLowerHex16(assetId);
				assetIdToBundleId[assetId] = bundleId;
			}

			for(int i = 0;i < addressableNames.Length;i++)
			{
                Debug.Log($"Asset: {assetPaths[i]}, GUID: {kv.Value[i].guid}, AssetId: {addressableNames[i]}, Bundle: {bundleName}");
            }

            var abb = new AssetBundleBuild
			{
				assetBundleName = bundleName,
				assetNames = assetPaths,
				addressableNames = addressableNames
			};
			builds.Add(abb);
		}

		return (builds, bundleIdByBundleName, assetIdToBundleId);
	}

	private static void WriteOrUpdateIndex(Dictionary<ulong, ulong> assetIdToBundleId)
	{
		string resourcesDir = "Assets/Resources";
		if (!AssetDatabase.IsValidFolder(resourcesDir))
		{
			CreateFolderRecursive(resourcesDir);
		}
		string indexAssetPath = Path.Combine(resourcesDir, IndexAssetResourcesPath + ".asset");

		BundleIndex index = AssetDatabase.LoadAssetAtPath<BundleIndex>(indexAssetPath);
		if (index == null)
		{
			index = ScriptableObject.CreateInstance<BundleIndex>();
			AssetDatabase.CreateAsset(index, indexAssetPath);
		}

		index.SetEntries(assetIdToBundleId);
		EditorUtility.SetDirty(index);
	}

	private static void CreateDirectoryIfNotExists(string path)
	{
		if (!Directory.Exists(path)) Directory.CreateDirectory(path);
	}

	private static void CreateFolderRecursive(string assetPath)
	{
		if (string.IsNullOrEmpty(assetPath)) return;
		string[] parts = assetPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0) return;
		// parts[0] expected to be "Assets"
		for (int i = 1; i < parts.Length; i++)
		{
			string parent = string.Join("/", parts.Take(i));
			string folderName = parts[i];
			string full = parent + "/" + folderName;
			if (!AssetDatabase.IsValidFolder(full))
			{
				AssetDatabase.CreateFolder(parent, folderName);
			}
		}
	}

	private static string GetRelativeDirectoryUnderRoot(string root, string assetPath)
	{
		string rel = assetPath.Substring(root.Length).TrimStart('/', '\\');
		string dir = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? string.Empty;
		return string.IsNullOrEmpty(dir) ? "_root" : dir; // group files at root into one bundle
	}

	private static string GetPlatformFolderName(BuildTarget target)
	{
		switch (target)
		{
			case BuildTarget.StandaloneWindows:
			case BuildTarget.StandaloneWindows64: return "StandaloneWindows64";
			case BuildTarget.Android: return "Android";
			case BuildTarget.iOS: return "iOS";
			case BuildTarget.WebGL: return "WebGL";
			default: return target.ToString();
		}
	}

	private static string ToLowerHex16(ulong value)
	{
		return value.ToString("x16");
	}
}


