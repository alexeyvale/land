using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Parsing.Tree;

namespace Land.Markup.CoreExtension
{
	public class GroupNodesByTypeVisitor : BaseTreeVisitor
	{
		public Dictionary<string, List<Node>> Grouped { get; set; } = new Dictionary<string, List<Node>>();

		public GroupNodesByTypeVisitor(IEnumerable<string> targetTypes)
		{
			Grouped = targetTypes.ToDictionary(e => e, e => new List<Node>());
		}

		public override void Visit(Node node)
		{
			if (Grouped.ContainsKey(node.Type)
				&& node.Location != null)
			{
				Grouped[node.Type].Add(node);
			}

			base.Visit(node);
		}

		public static Dictionary<string, List<Node>> GetGroups(Node root, IEnumerable<string> targetTypes)
		{
			var visitor = new GroupNodesByTypeVisitor(targetTypes);

			root.Accept(visitor);

			return visitor.Grouped;
		}
	}
}
