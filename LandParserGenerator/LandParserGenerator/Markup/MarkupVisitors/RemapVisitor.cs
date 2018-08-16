using System;
using System.Collections.Generic;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public class RemapVisitor : BaseMarkupVisitor
	{
		public Dictionary<Node, Node> Mapping;

		public RemapVisitor(Dictionary<Node, Node> mapping)
		{
			Mapping = mapping;
		}

		public override void Visit(ConcernPoint point)
		{
			point.TreeNode = Mapping.ContainsKey(point.TreeNode)
				? Mapping[point.TreeNode] : null;
		}
	}
}
