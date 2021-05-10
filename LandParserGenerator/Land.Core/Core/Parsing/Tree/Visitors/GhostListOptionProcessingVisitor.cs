using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Land.Core.Specification;

namespace Land.Core.Parsing.Tree
{
	public class GhostListOptionProcessingVisitor : GrammarProvidedTreeVisitor
	{
		public GhostListOptionProcessingVisitor(Grammar g) : base(g) { }

		public override void Visit(Node node)
		{
			/// Убираем узел из дерева, если соответствующий символ помечен как ghost или void
			for (var i = 0; i < node.Children.Count; ++i)
			{
				if (IsOptionSet(NodeOption.VOID, node.Children[i]))
				{
					var smbToRemove = node.Children[i];
					node.Children.RemoveAt(i);

					--i;
					continue;
				}

				/// Если узел призрачный локально или нет локальной опции, но проставлена глобальная
				if (IsOptionSet(NodeOption.GHOST, node.Children[i]))
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

			var listForAlias = !String.IsNullOrEmpty(node.Alias) && GrammarObject.Options.IsSet(NodeOption.GROUP_NAME, NodeOption.LIST, node.Alias);
			var listForSymbol = GrammarObject.Options.IsSet(NodeOption.GROUP_NAME, NodeOption.LIST, node.Symbol);

			// Если символ помечен как List, убираем подузлы того же типа
			if (node.Options.IsSet(NodeOption.GROUP_NAME, NodeOption.LIST) 
				|| !node.Options.GetOptions(NodeOption.GROUP_NAME).Any() && (listForAlias || listForSymbol))
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

		private bool IsOptionSet(string option, Node nodeToCheck) =>
			nodeToCheck.Options.IsSet(NodeOption.GROUP_NAME, option)
				|| !nodeToCheck.Options.GetOptions(NodeOption.GROUP_NAME).Any()
				&& (GrammarObject.Options.IsSet(NodeOption.GROUP_NAME, option, nodeToCheck.Symbol)
				|| !String.IsNullOrEmpty(nodeToCheck.Alias)
				&& GrammarObject.Options.IsSet(NodeOption.GROUP_NAME, option, nodeToCheck.Alias));
	}
}
