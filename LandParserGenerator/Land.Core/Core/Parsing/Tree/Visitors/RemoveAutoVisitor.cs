using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Specification;

namespace Land.Core.Parsing.Tree
{
	public class RemoveAutoVisitor: GrammarProvidedTreeVisitor
	{
		protected Grammar grammar { get; set; }

		public RemoveAutoVisitor(Grammar g) : base(g) { }

		public override void Visit(Node node)
		{
			for (var i = 0; i < node.Children.Count; ++i)
			{
				if (node.Children[i].Symbol.StartsWith(Grammar.AUTO_RULE_PREFIX))
				{
					/// Если последний потомок - автонетерминал с установленным псевдонимом
					if(i == node.Children.Count - 1 && !String.IsNullOrEmpty(node.Children[i].Alias))
					{
						node.Alias = node.Children[i].Alias;
					}

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

			base.Visit(node);
		}
	}
}
