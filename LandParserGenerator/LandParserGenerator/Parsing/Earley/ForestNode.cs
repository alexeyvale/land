using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing.Earley
{
	public class ForestNode
	{
		public string Symbol { get; set; }
		public Marker Marker { get; set; }

		public int StartIndex { get; set; }
		public int EndIndex { get; set; }

		public override bool Equals(object obj)
		{
			var node = obj as ForestNode;

			if (node == null)
				return false;

			return (!String.IsNullOrEmpty(Symbol) && Symbol == node.Symbol || Marker != null && Marker.Equals(node.Marker))
				&& StartIndex == node.StartIndex
				&& EndIndex == node.EndIndex;
		}

		public override int GetHashCode()
		{
			var firstHash = String.IsNullOrEmpty(Symbol) ? Marker.GetHashCode() : Symbol.GetHashCode();
			return (firstHash * 7 + StartIndex) * 7 + EndIndex;
		}
	}
}
