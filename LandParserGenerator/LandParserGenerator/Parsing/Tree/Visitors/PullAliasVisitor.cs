using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;
using Land.Core.Parsing.Tree;

namespace Land.Core.Parsing
{
	public class PullAliasVisitor: BaseVisitor
	{
		protected Grammar grammar { get; set; }

		public PullAliasVisitor(Grammar g)
		{
			grammar = g;
		}

		public override void Visit(Node node)
		{
			foreach (var child in node.Children)
				Visit(child);

			var lastChild = node.Children.LastOrDefault();

			/// Псевдонимы протягиваются через автосгенерированные нетерминалы
			/// к ближайшему явным образом описанному родителю
			if (lastChild != null && lastChild.Symbol.StartsWith(Grammar.AUTO_RULE_PREFIX))
				if (!String.IsNullOrEmpty(lastChild.Alias))
				{
					node.Alias = lastChild.Alias;
					lastChild.Alias = null;
				}
		}
	}
}
