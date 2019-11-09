using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Parsing.Tree;

namespace Land.Markup.CoreExtension
{
	public class LandExplorerVisitor : BaseTreeVisitor
	{
		public List<Node> Land { get; set; } = new List<Node>();

		public override void Visit(Node node)
		{
			if (node.Options.IsSet(MarkupOption.LAND))
			{
				Land.Add(node);
			}

			base.Visit(node);
		}
	}
}
