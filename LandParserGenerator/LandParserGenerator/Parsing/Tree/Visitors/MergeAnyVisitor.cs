using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing
{
	public class MergeAnyVisitor: BaseVisitor
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
					&& node.Children[i-1].StartOffset.HasValue
					&& node.Children[i - 1].EndOffset.HasValue)
				{
					node.Children[i - 1].SetAnchor(
						node.Children[i - 1].StartOffset.HasValue ? node.Children[i - 1].StartOffset.Value : node.Children[i].StartOffset.Value,
						node.Children[i].EndOffset.Value
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
