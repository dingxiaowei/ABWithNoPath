using System;
using System.Text;

public static class HashUtil
{
	private const ulong Fnv64OffsetBasis = 1469598103934665603UL;
	private const ulong Fnv64Prime = 1099511628211UL;

	public static ulong ComputeHash64(string text)
	{
		if (text == null) throw new ArgumentNullException(nameof(text));
		ulong hash = Fnv64OffsetBasis;
		for (int i = 0; i < text.Length; i++)
		{
			hash ^= text[i];
			hash *= Fnv64Prime;
		}
		return hash;
	}

	public static string ToLowerHex16(ulong value)
	{
		return value.ToString("x16");
	}
}


