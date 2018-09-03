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
		private BasePreprocessor Preproc { get; set; }

		public Statistics Statistics { get; set; }
		public List<Message> Log { get; protected set; }

		public BaseParser(Grammar g, ILexer lexer)
		{
			GrammarObject = g;
			Lexer = lexer;
		}

		public Node Parse(string text)
		{
			Log = new List<Message>();

			/// Если парсеру передан препроцессор
			if(Preproc != null)
			{
				/// Предобрабатываем текст
				text = Preproc.Preprocess(text, out bool success);

				/// Если препроцессор сработал успешно, можно парсить
				if(success)
				{
					var root = ParseBody(text);
					Preproc.Postprocess(root, Log);
					return root;
				}
				else
				{
					return null;
				}
			}

			return ParseBody(text);
		}

		public abstract Node ParseBody(string text);

		public void SetPreprocessor(BasePreprocessor preproc)
		{
			Preproc = preproc;
		}

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
