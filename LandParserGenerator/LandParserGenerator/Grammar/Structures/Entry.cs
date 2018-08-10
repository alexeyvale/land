using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.Core
{
	/// <summary>
	/// Квантификатор элемента правила
	/// </summary>
	public enum Quantifier { ONE_OR_MORE, ZERO_OR_MORE, ZERO_OR_ONE }

	[Serializable]
	public class Entry
	{
		public string Symbol { get; set; }

		public LocalOptions Options { get; set; }

		public Entry(string val)
		{
			Symbol = val;
			Options = new LocalOptions();
		}

		public Entry(string val, LocalOptions opts)
		{
			Symbol = val;
			Options = opts;
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
