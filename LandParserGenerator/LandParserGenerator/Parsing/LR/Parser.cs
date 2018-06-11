using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing.LR
{
	public class Parser: BaseParser
	{
		private TableLR1 Table { get; set; }

		private ParsingStack Stack { get; set; }
		private TokenStream LexingStream { get; set; }
		private DecisionStack Decisions { get; set; }

		public Parser(Grammar g, ILexer lexer): base(g, lexer)
		{
			Table = new TableLR1(g);
		}

		public override Node Parse(string text)
		{
			Statistics = new Statistics();
			Log = new List<Message>();
			Node root = null;	

			/// Готовим лексер
			LexingStream = new TokenStream(Lexer, text);
			/// Читаем первую лексему из входного потока
			var token = LexingStream.NextToken();
			/// Создаём стек
			Stack = new ParsingStack(LexingStream);
			Stack.Push(0);

			Decisions = new DecisionStack(Stack, LexingStream);

			while (true)
			{
				var currentState = Stack.PeekState();

				if(token.Name != Grammar.ERROR_TOKEN_NAME)
					Log.Add(Message.Trace(
						$"Текущий токен: {GetTokenInfoForMessage(token)} | Стек: {Stack.ToString(grammar)}",
						token.Line,
						token.Column
					));

				if (token.Name == Grammar.ERROR_TOKEN_NAME)
				{
					var errorToken = LexingStream.CurrentToken();

					if (grammar.Options.IsSet(ParsingOption.BACKTRACKING))
					{
						Log.Add(Message.Warning(
							$"Неожиданный символ {GetTokenInfoForMessage(errorToken)} для состояния{Environment.NewLine}\t\t" + Table.ToString(Stack.PeekState(), null, "\t\t"),
							errorToken.Line,
							errorToken.Column
						));

						token = Backtrack();

						if (token == null)
						{
							Log.Add(Message.Error(
								$"Не удалось возобновить разбор",
								errorToken.Line,
								errorToken.Column
							));

							break;
						}
					}
					else
					{
						Log.Add(Message.Error(
							$"Неожиданный символ {GetTokenInfoForMessage(errorToken)} для состояния{Environment.NewLine}\t\t" + Table.ToString(Stack.PeekState(), null, "\t\t"),
							errorToken.Line,
							errorToken.Column
						));

						break;
					}
				}
				/// Знаем, что предпринять, если действие однозначно
				/// или существует выбор между shift и reduce (тогда выбираем shift)
				else if (Table[currentState, token.Name].Count == 1
					|| Table[currentState, token.Name].Count == 2 && Table[currentState, token.Name].Any(a => a is ShiftAction))
				{
					var action = GetAction(currentState, token.Name);

					/// Не совершаем переход, который уже делали для того же самого токена
					var isError = token.Name != Grammar.ANY_TOKEN_NAME 
						&& Table[currentState, Grammar.ANY_TOKEN_NAME].Count > 0 
						&& !Decisions.ChooseTransition();

					if (!isError)
					{
						/// Если нужно произвести перенос
						if (action is ShiftAction)
						{
							var tokenNode = new Node(token.Name);
							tokenNode.SetAnchor(token.StartOffset, token.EndOffset);

							var shift = (ShiftAction)action;
							/// Вносим в стек новое состояние
							Stack.Push(tokenNode, shift.TargetItemIndex);

							Log.Add(Message.Trace(
								$"Перенос",
								token.Line,
								token.Column
							));

							if(token.Name == Grammar.ANY_TOKEN_NAME)
								token = SkipAny(Stack.PeekSymbol());
							else
								token = LexingStream.NextToken();
						}
						/// Если нужно произвести свёртку
						else if (action is ReduceAction)
						{
							Stack.InitBatch();

							var reduce = (ReduceAction)action;
							var parentNode = new Node(reduce.ReductionAlternative.NonterminalSymbolName);

							/// Снимаем со стека символы ветки, по которой нужно произвести свёртку
							for (var i = 0; i < reduce.ReductionAlternative.Count; ++i)
							{
								parentNode.AddFirstChild(Stack.PeekSymbol());
								Stack.Pop();
							}
							currentState = Stack.PeekState();

							/// Кладём на стек состояние, в которое нужно произвести переход
							Stack.Push(
								parentNode,
								Table.Transitions[currentState][reduce.ReductionAlternative.NonterminalSymbolName]
							);

							Stack.FinBatch();

							Log.Add(Message.Trace(
								$"Свёртка по правилу {grammar.Userify(reduce.ReductionAlternative)} -> {grammar.Userify(reduce.ReductionAlternative.NonterminalSymbolName)}",
								token.Line,
								token.Column
							));

							continue;
						}
						else if (action is AcceptAction)
						{
							root = Stack.PeekSymbol();
							break;
						}
					}
					else
					{
						token = Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
					}
				}
				else
				{
					/// Если встретился неожиданный токен, но он в списке пропускаемых
					if (grammar.Options.IsSet(ParsingOption.SKIP, token.Name))
					{
						token = LexingStream.NextToken();
					}
					else
					{
						Log.Add(Message.Trace(
							$"Попытка подобрать токены как Any для состояния {Environment.NewLine}\t\t" + Table.ToString(Stack.PeekState(), null, "\t\t"),
							token.Line,
							token.Column
						));

						token = Lexer.CreateToken(Grammar.ANY_TOKEN_NAME);
					}
				}
			}

			if(root != null)
				TreePostProcessing(root);

			return root;
		}

		private Action GetAction(int currentState, string token)
		{
			if (Table[currentState, token].Count == 0)
				return null;

			return Table[currentState, token].Count == 1
				? Table[currentState, token].Single()
				: Table[currentState, token].Single(a => a is ShiftAction);
		}

		private IToken SkipAny(Node anyNode)
		{
			var token = LexingStream.CurrentToken();

			int startOffset = token.StartOffset;
			int endOffset = token.EndOffset;

			/// Пропускаем токены, пока не найдём тот, для которого
			/// в текущем состоянии нужно выполнить перенос или свёртку
			while (Table[Stack.PeekState(), token.Name].Count == 0 
				&& token.Name != Grammar.EOF_TOKEN_NAME)
			{
				endOffset = token.EndOffset;
				token = LexingStream.NextToken();
			}

			anyNode.SetAnchor(startOffset, endOffset);

			/// Если дошли до конца входной строки, и это было не по плану
			if (token.Name == Grammar.EOF_TOKEN_NAME
				&& Table[Stack.PeekState(), token.Name].Count == 0)
			{
				Log.Add(Message.Error(
					$"Ошибка при пропуске токенов: неожиданный конец файла",
					null
				));

				return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
			}

			return token;
		}

		/// <summary>
		/// Бэктрекинг в случае ошибки разбора
		/// </summary>
		private IToken Backtrack()
		{
			Statistics.BacktracingCalled += 1;

			var decision = Decisions.BacktrackToClosestDecision();

			while (decision != null)
			{
				/// Если текущее решение касается выбора альтернативы
				if (decision is ChooseTransitionDecision)
				{
					/// Больше это решение не поменять
					Decisions.Pop();

					Log.Add(Message.Warning(
						$"BACKTRACKING: успех, трактовка токена {GetTokenInfoForMessage(LexingStream.CurrentToken())} как Any",
						LexingStream.CurrentToken().Line,
						LexingStream.CurrentToken().Column
					));

					return Lexer.CreateToken(Grammar.ANY_TOKEN_NAME);
				}

				if (decision is FinishAnyDecision)
				{
					var finish = (FinishAnyDecision)decision;

					Log.Add(Message.Warning(
							$"BACKTRACKING: попытка продлить Any",
							LexingStream.CurrentToken().Line,
							LexingStream.CurrentToken().Column
						));

					/// Включаем в текст цепочку однотипных токенов,
					/// на которых ранее прекратили подбор текста
					var tokenToSkip = LexingStream.CurrentToken();
					var currentToken = tokenToSkip;

					var textStart = currentToken.StartOffset;
					var textEnd = currentToken.EndOffset;

					while (currentToken.Name == tokenToSkip.Name)
					{
						finish.AnyNode.Value.Add(currentToken.Text);
						textEnd = currentToken.EndOffset;
						currentToken = LexingStream.NextToken();
					}

					finish.AnyNode.SetAnchor(finish.AnyNode.StartOffset.HasValue ? finish.AnyNode.StartOffset.Value : textStart, textEnd);

					/// Пропускаем текст дальше
					if (SkipAny(finish.AnyNode).Name != Grammar.ERROR_TOKEN_NAME)
					{
						Log.Add(Message.Warning(
							$"BACKTRACKING: успех, разбор продолжен с токена {GetTokenInfoForMessage(LexingStream.CurrentToken())}",
							LexingStream.CurrentToken().Line,
							LexingStream.CurrentToken().Column
						));

						finish.DecisionTokenIndex = LexingStream.CurrentTokenIndex;
						finish.AttemptsCount += 1;

						return LexingStream.CurrentToken();
					}
					else
						Decisions.Pop();
				}

				decision = Decisions.BacktrackToClosestDecision();
			}

			return null;
		}
	}
}
