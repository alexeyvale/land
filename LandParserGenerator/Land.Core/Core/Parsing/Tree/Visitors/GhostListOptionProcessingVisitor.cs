using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Land.Core.Specification;
using Land.Markup.CoreExtension;

namespace Land.Core.Parsing.Tree
{
	public class GhostListOptionProcessingVisitor : GrammarProvidedTreeVisitor
	{
		protected Grammar grammar { get; set; }

		public GhostListOptionProcessingVisitor(Grammar g) : base(g) { }

		public override void Visit(Node node)
		{
			// Убираем узел из дерева, если соответствующий символ помечен как ghost 
			for (var i = 0; i < node.Children.Count; ++i)
			{
				/// Если узел призрачный локально или нет локальной опции, но проставлена глобальная
				if (node.Children[i].Options.IsSet(NodeOption.GHOST) || !node.Children[i].Options.GetNodeOptions().Any()
					&& (grammar.Options.IsSet(NodeOption.GHOST, node.Children[i].Symbol) 
					|| !String.IsNullOrEmpty(node.Children[i].Alias) && grammar.Options.IsSet(NodeOption.GHOST, node.Children[i].Alias)))
				{
					var smbToRemove = node.Children[i];
					node.Children.RemoveAt(i);

					for (var j=smbToRemove.Children.Count -1; j >=0; --j)
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
			if (node.Options.IsSet(NodeOption.LIST) 
				|| !node.Options.GetNodeOptions().Any() && (listForAlias || listForSymbol))
			{
				for (var i = 0; i < node.Children.Count; ++i)
				{
					if (listForSymbol && node.Children[i].Symbol == node.Symbol 
						|| listForAlias && node.Children[i].Alias == node.Alias)
					{
						var smbToRemove = node.Children[i];
						node.Children.RemoveAt(i);

						if (smbToRemove.Options.GetPriority().HasValue)
							smbToRemove.Children.ForEach(c => c.Options.SetPriority(
								(c.Options.GetPriority() ?? 1) * smbToRemove.Options.GetPriority().Value
							));

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
