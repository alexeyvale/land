using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Specification;
using Land.Core.Lexing;

namespace Land.Core.Parsing.Tree
{
	public class MergeAnyVisitor: GrammarProvidedTreeVisitor
	{
		protected Grammar grammar { get; set; }

		public MergeAnyVisitor(Grammar g) : base(g) { }

		public override void Visit(Node node)
		{
			for (var i = 1; i < node.Children.Count; ++i)
			{
				if (node.Children[i].Symbol == Grammar.ANY_TOKEN_NAME
					&& node.Children[i - 1].Symbol == Grammar.ANY_TOKEN_NAME
					&& node.Children[i].Location != null)
				{
					node.Children[i - 1].SetLocation(
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
