using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing
{
	public abstract class BaseParser
	{
		protected Grammar grammar { get; set; }
		protected ILexer Lexer { get; set; }

		public List<ParsingMessage> Log { get; protected set; }
		public List<ParsingMessage> Errors { get; protected set; }

		public BaseParser(Grammar g, ILexer lexer)
		{
			grammar = g;
			Lexer = lexer;
		}

		public abstract Node Parse(string text);
	}
}
