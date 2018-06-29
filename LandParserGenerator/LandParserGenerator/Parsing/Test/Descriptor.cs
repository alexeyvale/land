using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing.Test
{
	public class Descriptor
	{
		public Alternative Alternative { get; set; }
		public int ElementIdx { get; set; }
		public HashSet<Descriptor> Predecessors { get; set; } = new HashSet<Descriptor>();

		public string Nonterminal { get { return Alternative.NonterminalSymbolName; } }
		public string Element { get { return Alternative[ElementIdx]; } }

		public override bool Equals(object obj)
		{
			var descr = obj as Descriptor;

			if (descr == null)
				return false;

			return Alternative == descr.Alternative
				&& ElementIdx == descr.ElementIdx;
		}

		public override int GetHashCode()
		{
			return Alternative.GetHashCode() * 7 + ElementIdx;
		}
	}
}
