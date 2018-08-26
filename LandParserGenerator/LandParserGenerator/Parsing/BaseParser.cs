using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;
using Land.Core.Parsing.Tree;

namespace Land.Core.Parsing
{
	public abstract class BaseParser
	{
		protected ILexer Lexer { get; set; }
		public Grammar GrammarObject { get; protected set; }

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
			root.Accept(new RemoveAutoVisitor(GrammarObject));
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
