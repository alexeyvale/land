using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Land.Core.Markup
{
	public static class FuzzyHashing
	{
		public const int MIN_TEXT_LENGTH = 25;

		public static byte[] GetFuzzyHash(string text)
		{
			var tlshObject = new TLSH.HashObject();
			var textBytes = Encoding.Unicode.GetBytes(text);
			tlshObject.final(textBytes, (uint)textBytes.Length, 1);
			return tlshObject.getHash();
		}

		public static double CompareTexts(string txt1, string txt2)
		{
			var hash1 = GetFuzzyHash(txt1);
			var hash2 = GetFuzzyHash(txt2);

			return CompareHashes(hash1, hash2);
		}

		public static double CompareHashes(byte[] hash1, byte[] hash2)
		{
			TLSH.HashObject tlsh1 = new TLSH.HashObject(), tlsh2 = new TLSH.HashObject();
			tlsh1.fromTlshStr(hash1);
			tlsh2.fromTlshStr(hash2);

			return Math.Max(0, (600 - tlsh1.totalDiff(tlsh2)) / 600.0);
		}
	}
}
