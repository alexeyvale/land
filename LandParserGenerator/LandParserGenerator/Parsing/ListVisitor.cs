using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing
{
	public class NodesEliminationVisitor : BaseVisitor
	{
		protected Grammar grammar { get; set; }

		public NodesEliminationVisitor(Grammar g)
		{
			grammar = g;
		}

		public override void Visit(Node node)
		{
			// Убираем узел из дерева, если соответствующий символ помечен как ghost 
			// или является автоматически сгенерированным и не отмечен как list
			for (var i = 0; i < node.Children.Count; ++i)
			{
				if (grammar.GhostSymbols.Contains(node.Children[i].Symbol) ||
					node.Children[i].Symbol.StartsWith(Grammar.AUTO_RULE_PREFIX) && !grammar.ListSymbols.Contains(node.Children[i].Symbol))
				{
					var smbToRemove = node.Children[i];
					node.Children.RemoveAt(i);
					node.Children.InsertRange(i, smbToRemove.Children);
					--i;
				}
			}

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
		}
	}

	public class UserifyVisitor: BaseVisitor
	{
		protected Grammar grammar { get; set; }

		public UserifyVisitor(Grammar g)
		{
			grammar = g;
		}

		public override void Visit(Node node)
		{
			node.Symbol = grammar.Userify(node.Symbol);
			base.Visit(node);
		}
	}
}
