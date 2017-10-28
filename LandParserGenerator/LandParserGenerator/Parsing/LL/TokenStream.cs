using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;

namespace LandParserGenerator.Parsing.LL
{
	public class TokenStream
	{
		private ILexer Lexer { get; set; }

		private List<IToken> Tokens { get; set; } = new List<IToken>();

		private int CurrentTokenIndex { get; set; } = 0;

		public TokenStream(ILexer lexer, string text)
		{
			Lexer = lexer;
			Lexer.SetSourceText(text);
		}

		public IToken PrevToken()
		{
			return Tokens[--CurrentTokenIndex];
		}

		public IToken NextToken()
		{
			if(CurrentTokenIndex == Tokens.Count)
			{
				Tokens.Add(Lexer.NextToken());
			}

			return Tokens[CurrentTokenIndex++];
		}
	}
}
