using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HashedBundleIndex", menuName = "AssetBundles/Hashed Bundle Index", order = 0)]
public class BundleIndex : ScriptableObject
{
	[Serializable]
	public struct Entry
	{
		public ulong assetId; // FNV-1a64 of asset GUID
		public ulong bundleId; // FNV-1a64 of relative folder under source root
	}

	[SerializeField]
	private List<Entry> entries = new List<Entry>();

	private Dictionary<ulong, ulong> assetToBundle;

	public void SetEntries(Dictionary<ulong, ulong> assetIdToBundleId)
	{
		entries.Clear();
		foreach (var kv in assetIdToBundleId)
		{
			entries.Add(new Entry { assetId = kv.Key, bundleId = kv.Value });
		}
		assetToBundle = null;
	}

	public bool TryGetBundleId(ulong assetId, out ulong bundleId)
	{
		EnsureLookup();
		return assetToBundle.TryGetValue(assetId, out bundleId);
	}

	private void EnsureLookup()
	{
		if (assetToBundle != null) return;
		assetToBundle = new Dictionary<ulong, ulong>(entries.Count);
		for (int i = 0; i < entries.Count; i++)
		{
			assetToBundle[entries[i].assetId] = entries[i].bundleId;
		}
	}
}


