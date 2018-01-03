using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
	public enum Quantifier { ONE, ONE_OR_MORE, ZERO_OR_MORE, ZERO_OR_ONE }

	public class Entry
	{
		public string Value { get; set; }

		public Quantifier Quantifier { get; set; }

		public Entry(string val, Quantifier quant = Quantifier.ONE)
		{
			Value = val;
			Quantifier = quant;
		}

		public static implicit operator String(Entry entry)
		{
			return entry.Value;
		}

		public override string ToString()
		{
			return Value;
		}
	}
}
