using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing
{
	public class GhostListOptionProcessingVisitor : BaseVisitor
	{
		protected Grammar grammar { get; set; }

		public GhostListOptionProcessingVisitor(Grammar g)
		{
			grammar = g;
		}

		public override void Visit(Node node)
		{
			// Убираем узел из дерева, если соответствующий символ помечен как ghost 
			for (var i = 0; i < node.Children.Count; ++i)
			{
				if (grammar.Options.IsSet(NodeOption.GHOST, node.Children[i].Symbol) 
					|| node.Children[i].Symbol.StartsWith(Grammar.AUTO_RULE_PREFIX) 
					||  node.Options.NodeOption == NodeOption.GHOST)
				{
					var smbToRemove = node.Children[i];
					node.Children.RemoveAt(i);

					for(var j=smbToRemove.Children.Count -1; j >=0; --j)
					{
						smbToRemove.Children[j].Parent = node;
						node.Children.Insert(i, smbToRemove.Children[j]);
					}

					--i;
				}
			}

			// Если символ помечен как List, убираем подузлы того же типа
			if (grammar.Options.IsSet(NodeOption.LIST, node.Symbol) 
				|| node.Options.NodeOption == NodeOption.LIST)
			{
				for (var i = 0; i < node.Children.Count; ++i)
				{
					if (node.Children[i].Symbol == node.Symbol)
					{
						var smbToRemove = node.Children[i];
						node.Children.RemoveAt(i);

						for (var j = smbToRemove.Children.Count - 1; j >= 0; --j)
						{
							smbToRemove.Children[j].Parent = node;
							node.Children.Insert(i, smbToRemove.Children[j]);
						}

						--i;
					}
				}
			}

			base.Visit(node);
		}
	}
}
