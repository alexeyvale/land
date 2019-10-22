using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Specification;
using Land.Core.Lexing;

namespace Land.Core.Parsing.Tree
{
	public class LeafOptionProcessingVisitor : GrammarProvidedTreeVisitor
	{
		public LeafOptionProcessingVisitor(Grammar g) : base(g) { }

		public override void Visit(Node node)
		{
			/// Если текущий узел должен быть листовым
			if (node.Options.IsSet(NodeOption.LEAF) || 
				!node.Options.GetNodeOptions().Any() && (GrammarObject.Options.IsSet(NodeOption.LEAF, node.Symbol) ||
				!String.IsNullOrEmpty(node.Alias) && GrammarObject.Options.IsSet(NodeOption.LEAF, node.Alias)))
			{
				node.Value = node.GetValue();

				/// Перед тем, как удалить дочерние узлы, вычисляем соответствие нового листа тексту
				var tmp = node.Location;

				node.Children.Clear();

				if(node.Location != null)
					node.SetLocation(tmp.Start, tmp.End);
			}
			else
				base.Visit(node);
		}
	}
}
