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

		public string TargetType { get; set; }

		public GroupNodesByTypeVisitor(string targetType = null)
		{
			TargetType = targetType;
		}

		public override void Visit(Node node)
		{
			if (String.IsNullOrEmpty(TargetType) || node.Type == TargetType)
			{
				if (!Grouped.ContainsKey(node.Type))
					Grouped[node.Type] = new List<Node>();

				Grouped[node.Type].Add(node);
			}

			base.Visit(node);
		}
	}
}
