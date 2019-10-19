using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Specification;
using Land.Core.Lexing;

namespace Land.Core.Parsing.Tree
{
	public class UserifyVisitor: GrammarProvidedTreeVisitor
	{
		protected Grammar grammar { get; set; }

		public UserifyVisitor(Grammar g) : base(g) { }

		public override void Visit(Node node)
		{
			node.UserifiedSymbol = grammar.Userify(node.Symbol);
			base.Visit(node);
		}
	}
}
