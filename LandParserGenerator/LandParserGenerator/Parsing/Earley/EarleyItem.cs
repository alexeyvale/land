using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing.Earley
{
	public class EarleyItem
	{
		public Marker Marker { get; set; }
		public int InputIndex { get; set; }
		public ForestNode TreeNode { get; set; }

		public override bool Equals(object obj)
		{
			var item = obj as EarleyItem;

			if (item == null)
				return false;

			return Marker.Equals(item.Marker)
				&& InputIndex == item.InputIndex
				&& TreeNode == item.TreeNode;
		}

		public override int GetHashCode()
		{
			return Marker.GetHashCode() * 7 + InputIndex;
		}
	}
}
