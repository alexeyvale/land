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

		public int CurrentTokenIndex { get; private set; } = -1;

		public TokenStream(ILexer lexer, string text)
		{
			Lexer = lexer;
			Lexer.SetSourceText(text);
		}

		/// <summary>
		/// Переход к предыдущему токену
		/// </summary>
		/// <returns></returns>
		public IToken PrevToken()
		{
			return Tokens[--CurrentTokenIndex];
		}

		/// <summary>
		/// Переход к следующему токену потока
		/// </summary>
		/// <returns></returns>
		public IToken NextToken()
		{
			++CurrentTokenIndex;

			if(CurrentTokenIndex == Tokens.Count)
			{
				Tokens.Add(Lexer.NextToken());
			}

			return Tokens[CurrentTokenIndex];
		}

		/// <summary>
		/// Текущий токен потока
		/// </summary>
		/// <returns></returns>
		public IToken CurrentToken()
		{
			if (CurrentTokenIndex == Tokens.Count)
			{
				Tokens.Add(Lexer.NextToken());
			}

			return Tokens[CurrentTokenIndex];
		}

		/// <summary>
		/// Переход к ранее прочитанному токену с заданным индексом
		/// </summary>
		public IToken BackToToken(int pos)
		{
			if (pos < Tokens.Count)
			{
				CurrentTokenIndex = pos;
				return Tokens[pos];
			}
			else
				return null;
		}
	}
}
