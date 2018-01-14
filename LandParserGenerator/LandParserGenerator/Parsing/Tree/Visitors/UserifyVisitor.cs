using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing
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
