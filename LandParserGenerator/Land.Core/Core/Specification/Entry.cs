using System;
using System.Collections.Generic;
using System.Linq;

namespace Land.Core.Specification
{
	/// <summary>
	/// Квантификатор элемента правила
	/// </summary>
	public enum Quantifier { ONE_OR_MORE, ZERO_OR_MORE, ZERO_OR_ONE }

	[Serializable]
	public class Entry
	{
		public string Symbol { get; set; }

		public SymbolOptionsManager Options { get; set; }

		public SymbolArguments Arguments { get; set; }

		public Entry(string val)
		{
			Symbol = val;
			Options = new SymbolOptionsManager();
		}

		public Entry(string val, SymbolOptionsManager opts)
		{
			Symbol = val;
			Options = opts;
		}

		public Entry(string val, SymbolOptionsManager opts, SymbolArguments args)
		{
			Symbol = val;
			Options = opts;
			Arguments = args;
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
