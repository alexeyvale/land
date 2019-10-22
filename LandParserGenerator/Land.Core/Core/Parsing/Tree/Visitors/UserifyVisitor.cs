using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Specification;
using Land.Core.Lexing;

namespace Land.Core.Parsing.Tree
{
	public class UserifyVisitor: GrammarProvidedTreeVisitor
	{
		public UserifyVisitor(Grammar g) : base(g) { }

		public override void Visit(Node node)
		{
			node.UserifiedSymbol = GrammarObject.Userify(node.Symbol);
			base.Visit(node);
		}
	}
}
