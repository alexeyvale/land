using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing
{
	public class ValueAccumulatingVisitor : BaseVisitor
	{
		protected Grammar grammar { get; set; }

		public ValueAccumulatingVisitor(Grammar g)
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
			if (grammar.TreeProcessingOptions[NodeOption.LEAF].Contains(node.Symbol)
					|| node.ProcessingOption == NodeOption.LEAF
					|| computeValue)
			{
				foreach (var child in node.Children)
				{
					Visit(child, true);
					node.Value.AddRange(child.Value);
				}

				node.Children.Clear();
			}
			else
				base.Visit(node);
		}
	}
}
