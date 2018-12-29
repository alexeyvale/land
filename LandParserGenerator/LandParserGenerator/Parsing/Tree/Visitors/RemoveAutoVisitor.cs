using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;

namespace Land.Core.Parsing.Tree
{
	public class RemoveAutoVisitor: BaseTreeVisitor
	{
		protected Grammar grammar { get; set; }

		public RemoveAutoVisitor(Grammar g)
		{
			grammar = g;
		}

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

					/// Если у символа, вместо которого подставляем потомков,
					/// есть явно указанный приоритет, проставляем его же для всех потомков
					if (smbToRemove.Options.Priority.HasValue)
						smbToRemove.Children.ForEach(c =>
							c.Options.Priority = (c.Options.Priority ?? LocalOptions.BASE_PRIORITY) * smbToRemove.Options.Priority);

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
