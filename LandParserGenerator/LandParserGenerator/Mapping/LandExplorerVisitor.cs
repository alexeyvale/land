using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;
using LandParserGenerator.Parsing;

namespace LandParserGenerator.Mapping
{
	public class LandExplorerVisitor : BaseVisitor
	{
		public List<Node> Land { get; set; } = new List<Node>();

		public override void Visit(Node node)
		{
			base.Visit(node);

			if (node.Options.IsLand)
				Land.Add(node);
		}
	}
}
