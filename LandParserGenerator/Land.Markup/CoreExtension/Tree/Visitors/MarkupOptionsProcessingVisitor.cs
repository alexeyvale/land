using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Land.Core.Specification;
using Land.Core.Parsing.Tree;

namespace Land.Markup.CoreExtension
{
	public class MarkupOptionsProcessingVisitor : GrammarProvidedTreeVisitor
	{
		private HashSet<string> Land { get; set; }
		private Dictionary<string, double> GlobalPriorities { get; set; }

		public MarkupOptionsProcessingVisitor(Grammar g): base(g)
		{
			Land = g.Options.GetSymbols(MarkupOption.GROUP_NAME, MarkupOption.LAND);

			GlobalPriorities = g.Options.GetSymbols(MarkupOption.GROUP_NAME, MarkupOption.PRIORITY)
				.ToDictionary(e => e, e => (double)g.Options.GetParams(MarkupOption.GROUP_NAME, MarkupOption.PRIORITY, e).First());
		}

		public override void Visit(Node node)
		{
			if(node.Type == Grammar.CUSTOM_BLOCK_RULE_NAME)
			{
				node.Options.Set(MarkupOption.GROUP_NAME, MarkupOption.LAND, null);

				node.Children.First().Options.Set(MarkupOption.GROUP_NAME, MarkupOption.EXACTMATCH, null);
				node.Children.First().Options.SetPriority(OptionsExtension.DEFAULT_PRIORITY);
				node.Children.Last().Options.Set(MarkupOption.GROUP_NAME, MarkupOption.EXACTMATCH, null);
				node.Children.Last().Options.SetPriority(OptionsExtension.DEFAULT_PRIORITY);

				/// TODO Возможно, тут надо как-то обработать опцию core
			}

			if (Land.Contains(node.Symbol) || Land.Contains(node.Alias))
			{
				node.Options.Set(MarkupOption.GROUP_NAME, MarkupOption.LAND, null);
			}

			if (GrammarObject.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.EXACTMATCH, node.Symbol)
				|| !String.IsNullOrEmpty(node.Alias) && GrammarObject.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.EXACTMATCH, node.Alias))
			{
				node.Options.Set(MarkupOption.GROUP_NAME, MarkupOption.EXACTMATCH, null);
			}

			if (!node.Options.GetPriority().HasValue)
			{
				node.Options.SetPriority(
					!String.IsNullOrEmpty(node.Alias) && GlobalPriorities.ContainsKey(node.Alias)
						? GlobalPriorities[node.Alias]
						: GlobalPriorities.ContainsKey(node.Symbol)
							? GlobalPriorities[node.Symbol]
							: node.Symbol == Grammar.ANY_TOKEN_NAME ? 0 : OptionsExtension.DEFAULT_PRIORITY
				);
			}

			if(GrammarObject.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.HEADERCORE, node.Symbol))
			{
				node.Options.Set(
					MarkupOption.GROUP_NAME,
					MarkupOption.HEADERCORE,
					GrammarObject.Options.GetParams(MarkupOption.GROUP_NAME, MarkupOption.HEADERCORE, node.Symbol)
				);
			}

			if (GrammarObject.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.HEADERCORE, node.Alias))
			{
				node.Options.Set(
					MarkupOption.GROUP_NAME,
					MarkupOption.HEADERCORE,
					GrammarObject.Options.GetParams(MarkupOption.GROUP_NAME, MarkupOption.HEADERCORE, node.Alias)
				);
			}

			if (GrammarObject.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.NOTUNIQUE, node.Symbol)
				|| !String.IsNullOrEmpty(node.Alias) && GrammarObject.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.NOTUNIQUE, node.Alias))
			{
				node.Options.Set(MarkupOption.GROUP_NAME, MarkupOption.NOTUNIQUE, null);
			}

			base.Visit(node);
		}
	}
}
