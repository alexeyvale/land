using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.Core
{
	[Serializable]
	public class PairSymbol: ISymbol
	{
		public string Name { get; set; }
		public int Index { get; set; }
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
