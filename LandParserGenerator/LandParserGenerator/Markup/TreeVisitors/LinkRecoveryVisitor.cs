using System;
using System.Collections.Generic;

using Land.Core.Parsing;
using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public class LinkRecoveryVisitor : BaseTreeVisitor
	{
		public Dictionary<int, List<ConcernPoint>> PointsToRecover { get; set; }

		public LinkRecoveryVisitor(Dictionary<int, List<ConcernPoint>> points)
		{
			PointsToRecover = points;
		}

		public override void Visit(Node node)
		{
			if (PointsToRecover.ContainsKey(node.Id))
			{
				foreach (var elem in PointsToRecover[node.Id])
					elem.TreeNode = node;
			}

			base.Visit(node);
		}
	}
}
