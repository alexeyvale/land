using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;

namespace Land.Core.Parsing.Tree
{
	public class UserifyVisitor: BaseTreeVisitor
	{
		protected Grammar grammar { get; set; }

		public UserifyVisitor(Grammar g)
		{
			grammar = g;
		}

		public override void Visit(Node node)
		{
			node.Symbol = grammar.Userify(node.Symbol);
			base.Visit(node);
		}
	}
}
