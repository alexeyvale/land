using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Land.Core.Markup
{
	public class HashedNode
	{
		public Parsing.Tree.Node Node { get; set; }
		public byte[] Hash { get; set; }
	}

	public static class FuzzyHashing
	{
		[DllImport("fuzzy.dll", CallingConvention = CallingConvention.Cdecl)]
		private static extern int fuzzy_hash_buf(byte[] buf, int buf_len, byte[] result);

		[DllImport("fuzzy.dll", CallingConvention = CallingConvention.Cdecl)]
		private static extern int fuzzy_compare(byte[] sig1, byte[] sig2);

		public static byte[] GetFuzzyHash(string text)
		{
			var textBytes = Encoding.UTF8.GetBytes(text);
			var hashBuffer = new byte[148];
			fuzzy_hash_buf(textBytes, textBytes.Length, hashBuffer);

			return hashBuffer.Reverse()
				.SkipWhile(b=>b==0).Reverse()
				.ToArray();
		}

		public static int CompareTexts(string txt1, string txt2)
		{
			var hash1 = GetFuzzyHash(txt1);
			var hash2 = GetFuzzyHash(txt2);

			return CompareHashes(hash1, hash2);
		}

		public static int CompareHashes(byte[] hash1, byte[] hash2)
		{
			return fuzzy_compare(hash1, hash2);
		}
	}
}
