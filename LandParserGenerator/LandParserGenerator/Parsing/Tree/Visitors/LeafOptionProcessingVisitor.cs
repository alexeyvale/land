using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;

namespace Land.Core.Parsing.Tree
{
	public class LeafOptionProcessingVisitor : BaseTreeVisitor
	{
		protected Grammar grammar { get; set; }

		public LeafOptionProcessingVisitor(Grammar g)
		{
			grammar = g;
		}

		public override void Visit(Node node)
		{
			Visit(node, false);
		}

		private void Visit(Node node, bool computeValue)
		{
			/// Если текущий узел должен быть листовым
			if (grammar.Options.IsSet(NodeOption.LEAF, node.Symbol)
				|| !String.IsNullOrEmpty(node.Alias) &&  grammar.Options.IsSet(NodeOption.LEAF, node.Alias)
				|| node.Options.NodeOption == NodeOption.LEAF
				|| computeValue)
			{
				foreach (var child in node.Children)
				{
					Visit(child, true);
					node.Value.AddRange(child.Value);
				}

				/// Перед тем, как удалить дочерние узлы, вычисляем соответствие нового листа тексту
				var tmp = node.Anchor;

				node.Children.Clear();

				if(node.Anchor != null)
					node.SetAnchor(tmp.Start, tmp.End);
			}
			else
				base.Visit(node);
		}
	}
}
