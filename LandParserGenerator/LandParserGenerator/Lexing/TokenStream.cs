using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Lexing
{
	public class TokenStream
	{
		private ILexer Lexer { get; set; }

		public List<IToken> Tokens { get; set; } = new List<IToken>();

		public TokenStream(ILexer lexer, string text)
		{
			Lexer = lexer;
			Lexer.SetSourceText(text);
		}

		/// <summary>
		/// Переход к следующему токену потока
		/// </summary>
		/// <returns></returns>
		public IToken NextToken()
		{
			if(Tokens.Count == 0 || Tokens.Last().Name != Grammar.EOF_TOKEN_NAME)
				Tokens.Add(Lexer.NextToken());

			return Tokens[Tokens.Count - 1];
		}

		/// <summary>
		/// Текущий токен потока
		/// </summary>
		/// <returns></returns>
		public IToken CurrentToken()
		{
			return Tokens.LastOrDefault();
		}
	}
}
