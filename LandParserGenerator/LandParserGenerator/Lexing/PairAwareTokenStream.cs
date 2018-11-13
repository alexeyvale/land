using System;
using System.Collections.Generic;
using System.Linq;

namespace Land.Core.Lexing
{
	public class PairAwareState
	{
		public Stack<PairSymbol> PairStack { get; set; }

		public Direction CurrentTokenDirection { get; set; }

		public PairSymbol OpenedPair { get; set; }
	}

	public enum Direction { Forward, Up, Down };

	public class PairAwareTokenStream: TokenStream, IGrammarProvided
	{
		public Grammar GrammarObject { get; private set; }

		private List<Message> Log { get; set; }

		/// <summary>
		/// Стек открытых на момент прочтения последнего токена пар
		/// </summary>
		private Stack<PairSymbol> PairStack { get; set; } = new Stack<PairSymbol>();

		/// <summary>
		/// Пара, левой границей которой является текущий токен
		/// </summary>
		private PairSymbol OpenedPair { get; set; }

		/// <summary>
		/// Направление перехода по вложенностям, которое задаётся текущим токеном
		/// </summary>
		public Direction CurrentTokenDirection { get; private set; }

		public PairAwareTokenStream(Grammar grammar, ILexer lexer, string text, List<Message> log): base(lexer, text)
		{
			GrammarObject = grammar;
			Log = log;
		}

		public PairAwareState GetPairsState()
		{
			return new PairAwareState()
			{
				CurrentTokenDirection = CurrentTokenDirection,
				PairStack = new Stack<PairSymbol>(PairStack.Reverse()),
				OpenedPair = OpenedPair
			};
		}

		public int GetPairsCount()
		{
			return PairStack.Count;
		}

		public IToken MoveTo(int idx, PairAwareState state)
		{
			var token = base.MoveTo(idx);

			if (token != null)
			{
				CurrentTokenDirection = state.CurrentTokenDirection;
				PairStack = state.PairStack;
				OpenedPair = state.OpenedPair;
			}

			return token;
		}

		public override IToken GetNextToken()
		{
			switch(CurrentTokenDirection)
			{
				case Direction.Down:
					PairStack.Push(OpenedPair);
					OpenedPair = null;
					break;
				case Direction.Up:
					PairStack.Pop();
					break;
			}

			CurrentTokenDirection = Direction.Forward;
			var token = base.GetNextToken();

			/// Предполагается, что токен может быть началом ровно одной пары, или концом ровно одной пары,
			/// или одновременно началом и концом ровно одной пары
			var closed = GrammarObject.Pairs.FirstOrDefault(p => p.Value.Right.Contains(token.Name));

			/// Если текущий токен закрывает некоторую конструкцию
			if (closed.Value != null)
			{
				/// и при этом не открывает её же
				if (!closed.Value.Left.Contains(token.Name))
				{
					/// проверяем, есть ли на стеке то, что можно этой конструкцией закрыть
					if (PairStack.Count == 0)
					{
						Log.Add(Message.Error(
							$"Отсутствует открывающая конструкция для парной закрывающей {this.GetTokenInfoForMessage(token)}",
							token.Location.Start
						));

						return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
					}
					else if (PairStack.Peek() != closed.Value)
					{
						Log.Add(Message.Error(
							$"Неожиданная закрывающая конструкция {this.GetTokenInfoForMessage(token)}, ожидается {String.Join("или ", PairStack.Peek().Right.Select(e => GrammarObject.Userify(e)))} для открывающей конструкции {String.Join("или ", PairStack.Peek().Left.Select(e => GrammarObject.Userify(e)))}",
							token.Location.Start
						));

						return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
					}
					else
					{
						CurrentTokenDirection = Direction.Up;
					}
				}
				/// иначе, если текущий токен одновременно открывающий и закрывающий
				else
				{
					/// и есть что закрыть, закрываем
					if (PairStack.Count > 0 && PairStack.Peek() == closed.Value)
						CurrentTokenDirection = Direction.Up;
					else
					{
						CurrentTokenDirection = Direction.Down;
						OpenedPair = closed.Value;
					}
				}
			}
			else
			{
				var opened = GrammarObject.Pairs.FirstOrDefault(p => p.Value.Left.Contains(token.Name));

				if (opened.Value != null)
				{
					CurrentTokenDirection = Direction.Down;
					OpenedPair = opened.Value;
				}
			}

			return token;
		}

		/// <summary>
		/// Получение следующего токена, находящегося на заданном уровне вложенности пар
		/// </summary>
		public IToken GetNextToken(int level, out List<IToken> skipped)
		{
			skipped = new List<IToken>();

			while (true)
			{
				var next = GetNextToken();

				/// Возвращаем следующий токен, если перешли на искомый уровень
				/// или готовимся сделать шаг в направлении, отличном от разрешённого
				if (PairStack.Count == level
					|| next.Name == Grammar.EOF_TOKEN_NAME
					|| next.Name == Grammar.ERROR_TOKEN_NAME)
					return next;
				else
					skipped.Add(next);
			}
		}
	}
}
