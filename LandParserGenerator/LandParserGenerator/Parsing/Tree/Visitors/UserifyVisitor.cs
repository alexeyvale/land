using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;
using Land.Core.Parsing.Tree;

namespace Land.Core.Parsing
{
	public class UserifyVisitor: BaseVisitor
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
