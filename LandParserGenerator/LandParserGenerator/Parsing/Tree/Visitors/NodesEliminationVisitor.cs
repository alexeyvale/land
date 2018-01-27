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
			for (var i = 0; i < node.Children.Count; ++i)
			{
				if (grammar.TreeProcessingOptions[NodeOption.GHOST].Contains(node.Children[i].Symbol) 
					|| node.Children[i].Symbol.StartsWith(Grammar.AUTO_RULE_PREFIX) 
					||  node.ProcessingOption == NodeOption.GHOST)
				{
					var smbToRemove = node.Children[i];
					node.Children.RemoveAt(i);
					node.Children.InsertRange(i, smbToRemove.Children);
					--i;
				}
			}

			// Если символ помечен как List, убираем подузлы того же типа
			if (grammar.TreeProcessingOptions[NodeOption.LIST].Contains(node.Symbol) 
				|| node.ProcessingOption == NodeOption.LIST)
			{
				for (var i = 0; i < node.Children.Count; ++i)
				{
					if (node.Children[i].Symbol == node.Symbol)
					{
						var smbToRemove = node.Children[i];
						node.Children.RemoveAt(i);
						node.Children.InsertRange(i, smbToRemove.Children);
						--i;
					}
				}
			}

			base.Visit(node);
		}
	}
}
