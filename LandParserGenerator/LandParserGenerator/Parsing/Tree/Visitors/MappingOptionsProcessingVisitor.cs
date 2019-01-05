using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;

namespace Land.Core.Parsing.Tree
{
	public class MappingOptionsProcessingVisitor : BaseTreeVisitor
	{
		private Grammar GrammarObject { get; set; }

		private HashSet<string> Land { get; set; }
		private Dictionary<string, double> GlobalPriorities { get; set; }

		public MappingOptionsProcessingVisitor(Grammar g)
		{
			GrammarObject = g;
			Land = g.Options.GetSymbols(MappingOption.LAND);

			GlobalPriorities = g.Options.GetSymbols(MappingOption.PRIORITY)
				.ToDictionary(e => e, e => (double)g.Options.GetParams(MappingOption.PRIORITY, e).First());
		}

		public override void Visit(Node node)
		{
			if (Land.Contains(node.Symbol) || Land.Contains(node.Alias))
				node.Options.Set(MappingOption.LAND);

			if (GrammarObject.Options.IsSet(MappingOption.EXACTMATCH, node.Symbol)
				|| !String.IsNullOrEmpty(node.Alias) && GrammarObject.Options.IsSet(MappingOption.EXACTMATCH, node.Alias))
				node.Options.ExactMatch = true;

			if (!node.Options.Priority.HasValue)
			{
				node.Options.Priority = !String.IsNullOrEmpty(node.Alias) && GlobalPriorities.ContainsKey(node.Alias)
					? GlobalPriorities[node.Alias]
					: GlobalPriorities.ContainsKey(node.Symbol)
						? GlobalPriorities[node.Symbol]
						: node.Symbol == Grammar.ANY_TOKEN_NAME ? 0 : LocalOptions.BASE_PRIORITY;
			}

			base.Visit(node);
		}
	}
}
