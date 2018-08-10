using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;
using Land.Core.Parsing.Tree;

namespace Land.Core.Parsing
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
					|| !String.IsNullOrEmpty(node.Children[i].Alias) && grammar.Options.IsSet(NodeOption.GHOST, node.Children[i].Alias)
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

			var listForAlias = !String.IsNullOrEmpty(node.Alias) && grammar.Options.IsSet(NodeOption.LIST, node.Alias);
			var listForSymbol = grammar.Options.IsSet(NodeOption.LIST, node.Symbol);

			// Если символ помечен как List, убираем подузлы того же типа
			if (listForAlias || listForSymbol || node.Options.NodeOption == NodeOption.LIST)
			{
				for (var i = 0; i < node.Children.Count; ++i)
				{
					if (listForSymbol && node.Children[i].Symbol == node.Symbol 
						|| listForAlias && node.Children[i].Alias == node.Alias)
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
