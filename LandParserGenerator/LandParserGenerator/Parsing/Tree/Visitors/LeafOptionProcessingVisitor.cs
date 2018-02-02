using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing
{
	public class LeafOptionProcessingVisitor : BaseVisitor
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
					|| node.Options.NodeOption == NodeOption.LEAF
					|| computeValue)
			{
				foreach (var child in node.Children)
				{
					Visit(child, true);
					node.Value.AddRange(child.Value);
				}

				/// Перед тем, как удалить дочерние узлы, вычисляем соответствие нового листа тексту
				var temp = node.StartOffset;

				node.Children.Clear();
			}
			else
				base.Visit(node);
		}
	}
}
