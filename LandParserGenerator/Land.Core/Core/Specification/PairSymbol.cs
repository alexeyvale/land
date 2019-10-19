using System;
using System.Collections.Generic;
using System.Linq;

namespace Land.Core.Specification
{
	[Serializable]
	public class PairSymbol: ISymbol
	{
		public string Name { get; set; }
		public HashSet<string> Left { get; set; }
		public HashSet<string> Right { get; set; }

		public override bool Equals(object obj)
		{
			return obj is PairSymbol && ((PairSymbol)obj).Name == Name;
		}

		public override int GetHashCode()
		{
			return Name.GetHashCode();
		}

		public override string ToString()
		{
			return Name;
		}
	}
}
