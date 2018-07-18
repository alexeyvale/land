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
		protected Grammar GrammarObject { get; set; }
		protected ILexer Lexer { get; set; }

		public Statistics Statistics { get; set; }
		public List<Message> Log { get; protected set; }

		public BaseParser(Grammar g, ILexer lexer)
		{
			GrammarObject = g;
			Lexer = lexer;
		}

		public abstract Node Parse(string text);

		protected void TreePostProcessing(Node root)
		{
			if (GrammarObject.Aliases.Count > 0)
				root.Accept(new PullAliasVisitor(GrammarObject));

			root.Accept(new GhostListOptionProcessingVisitor(GrammarObject));
			root.Accept(new LeafOptionProcessingVisitor(GrammarObject));
			root.Accept(new MergeAnyVisitor(GrammarObject));
			root.Accept(new MappingOptionsProcessingVisitor(GrammarObject));
			root.Accept(new UserifyVisitor(GrammarObject));
		}

		protected string GetTokenInfoForMessage(IToken token)
		{
			var userified = GrammarObject.Userify(token.Name);
			if (userified == token.Name && token.Name != Grammar.ANY_TOKEN_NAME && token.Name != Grammar.EOF_TOKEN_NAME)
				return $"{token.Name}: '{token.Text}'";
			else
				return userified;
		}
	}
}
