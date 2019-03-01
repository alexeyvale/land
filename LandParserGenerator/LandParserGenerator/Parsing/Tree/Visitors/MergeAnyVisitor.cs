using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;

namespace Land.Core.Parsing.Tree
{
	public class MergeAnyVisitor: BaseTreeVisitor
	{
		protected Grammar grammar { get; set; }

		public MergeAnyVisitor(Grammar g)
		{
			grammar = g;
		}

		public override void Visit(Node node)
		{
			for (var i = 1; i < node.Children.Count; ++i)
			{
				if (node.Children[i].Symbol == Grammar.ANY_TOKEN_NAME
					&& node.Children[i - 1].Symbol == Grammar.ANY_TOKEN_NAME
					&& node.Children[i].Location != null)
				{
					node.Children[i - 1].SetAnchor(
						node.Children[i - 1].Location != null ? node.Children[i - 1].Location.Start : node.Children[i].Location.Start,
						node.Children[i].Location.End
					);

					node.Children[i - 1].Value.AddRange(node.Children[i].Value);

					node.Children.RemoveAt(i);
					--i;
				}
			}

			base.Visit(node);
		}
	}
}
