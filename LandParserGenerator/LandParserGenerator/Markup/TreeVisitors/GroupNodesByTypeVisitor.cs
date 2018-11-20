using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Parsing.Tree;
using Land.Core.Parsing;

namespace Land.Core.Markup
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
			if (Grouped.ContainsKey(node.Type))
				Grouped[node.Type].Add(node);

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
