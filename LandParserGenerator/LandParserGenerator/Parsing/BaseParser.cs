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

		public Statistics Statistics { get; set; }
		public List<Message> Log { get; protected set; }

		public BaseParser(Grammar g, ILexer lexer)
		{
			grammar = g;
			Lexer = lexer;
		}

		public abstract Node Parse(string text);

		protected void TreePostProcessing(Node root)
		{
			root.Accept(new GhostListOptionProcessingVisitor(grammar));
			root.Accept(new LeafOptionProcessingVisitor(grammar));
			root.Accept(new MergeAnyVisitor(grammar));
			root.Accept(new MappingOptionsProcessingVisitor(grammar));
			root.Accept(new UserifyVisitor(grammar));
		}

		protected string GetTokenInfoForMessage(IToken token)
		{
			var userified = grammar.Userify(token.Name);
			if (userified == token.Name && token.Name != Grammar.ANY_TOKEN_NAME && token.Name != Grammar.EOF_TOKEN_NAME)
				return $"{token.Name}: '{token.Text}'";
			else
				return userified;
		}
	}
}
