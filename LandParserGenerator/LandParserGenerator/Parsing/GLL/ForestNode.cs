using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing.GLL
{
	public class ForestNode
	{
		public Label Label { get; set; }
		public List<ForestNode> Children { get; set; } = new List<ForestNode>();
		public int LeftExtent { get; set; }
		public int RightExtent { get; set; }
		public int Pivot { get { return LeftExtent; } set { LeftExtent = value; } }
	}
}
