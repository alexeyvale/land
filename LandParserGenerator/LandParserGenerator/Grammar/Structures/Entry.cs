using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
	public enum Quantifier { ONE_OR_MORE, ZERO_OR_MORE, ZERO_OR_ONE }

	public enum NodeOption { NONE, LAND, GHOST, LIST, LEAF }

	public enum ParsingOption { START, SKIP }

	public class Entry
	{
		public string Symbol { get; set; }

		public NodeOption Option { get; set; }

		public Entry(string val)
		{
			Symbol = val;
			Option = NodeOption.NONE;
		}

		public Entry(string val, NodeOption opt)
		{
			Symbol = val;
			Option = opt;
		}


		public static implicit operator String(Entry entry)
		{
			return entry.Symbol;
		}

		public override string ToString()
		{
			return Symbol;
		}
	}
}
