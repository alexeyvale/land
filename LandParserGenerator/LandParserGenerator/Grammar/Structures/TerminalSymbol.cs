using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
	public class TerminalSymbol: ISymbol
	{
		public string Name { get; private set; }
		public string Pattern { get; set; }

		public TerminalSymbol(string name, string pattern)
		{
			Name = name;
			Pattern = pattern;
		}

		public override bool Equals(object obj)
		{
			return obj is TerminalSymbol && ((TerminalSymbol)obj).Name == Name;
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
