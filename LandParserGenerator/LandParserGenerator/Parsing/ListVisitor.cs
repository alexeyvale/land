using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing
{
	public class ListVisitor : BaseVisitor
	{
		protected Grammar grammar { get; set; }

		public ListVisitor(Grammar g)
		{
			grammar = g;
		}

		public override void Visit(Node node)
		{
			if (grammar.ListSymbols.Contains(node.Symbol))
			{
				for (var i = 0; i < node.Children.Count; ++i)
				{
					if (node.Children[i].Symbol == node.Symbol)
					{
						var smbToRemove = node.Children[i];
						node.Children.RemoveAt(i);
						node.Children.InsertRange(i, smbToRemove.Children);
					}
				}
			}

			base.Visit(node);

			for (var i = 0; i < node.Children.Count; ++i)
			{
				if(grammar.GhostSymbols.Contains(node.Children[i].Symbol))
				{
					var smbToRemove = node.Children[i];
					node.Children.RemoveAt(i);
					node.Children.InsertRange(i, smbToRemove.Children);
				}
			}
		}
	}
}
