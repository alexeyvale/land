using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Specification;

namespace Land.Core.Lexing
{
	public class TokenStream
	{
		protected ILexer Lexer { get; set; }

		private List<IToken> Tokens { get; set; } = new List<IToken>();

		public TokenStream(ILexer lexer, string text)
		{
			Lexer = lexer;
			Lexer.SetSourceText(text);
		}

		/// <summary>
		/// Переход к следующему токену потока
		/// </summary>
		/// <returns></returns>
		public virtual IToken GetNextToken()
		{
			/// Если токен с нужным индексом ещё не считан
			/// и последний считанный токен - не признак конца файла
			if (CurrentIndex + 1 == Tokens.Count)
			{
				if (Tokens.Count == 0 || Tokens.Last().Name != Grammar.EOF_TOKEN_NAME)
				{
					++CurrentIndex;
					Tokens.Add(Lexer.NextToken());
				}
			}
			else
			{
				++CurrentIndex;
			}

			return Tokens[CurrentIndex];
		}

		/// <summary>
		/// Текущий токен потока
		/// </summary>
		/// <returns></returns>
		public IToken CurrentToken { get { return CurrentIndex >= 0 ? Tokens[CurrentIndex] : null; } }

		public int CurrentIndex { get; private set; } = -1;

		public int Count => Tokens.Count;

		public IToken MoveTo(int idx)
		{
			if (idx >= 0 && idx < Tokens.Count)
			{
				CurrentIndex = idx;
				return Tokens[CurrentIndex];
			}
			else
			{
				return null;
			}
		}
	}
}
