using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing.LL
{
	public class Parser: BaseParser
	{
		private const int MAX_RECOVERY_ATTEMPTS = 5;

		private TableLL1 Table { get; set; }
		private ParsingStack Stack { get; set; }
		private TokenStream LexingStream { get; set; }

		private Stack<DecisionPoint> Decisions { get; set; }
		private int BacktrackingCounter { get; set; }

		public Parser(Grammar g, ILexer lexer): base(g, lexer)
		{
			Table = new TableLL1(g);

            /// В ходе парсинга потребуется First,
            /// учитывающее возможную пустоту ANY
            g.UseModifiedFirst = true;
		}

		/// <summary>
		/// LL(1) разбор
		/// </summary>
		/// <returns>
		/// Корень дерева разбора
		/// </returns>
		public override Node Parse(string text)
		{
			Log = new List<Message>();
			Errors = new List<Message>();

			Decisions = new Stack<DecisionPoint>();
			BacktrackingCounter = 0;

            /// Готовим лексер
            LexingStream = new TokenStream(Lexer, text);

			/// Кладём на стек стартовый символ
			Stack = new ParsingStack(LexingStream);
			Stack.InitBatch();
			var root = new Node(grammar.StartSymbol);
			Stack.Push(new Node(Grammar.EOF_TOKEN_NAME));
			Stack.Push(root);

			/// Читаем первую лексему из входного потока
			var token = LexingStream.NextToken();

			/// Пока не прошли полностью правило для стартового символа
			while (Stack.Count > 0)
			{
				var test = false;

				if (test)
					break;

				var stackTop = Stack.Peek();

				Log.Add(Message.Trace(
					$"Текущий токен: {GetTokenInfoForMessage(token)} | Символ на вершине стека: {grammar.Userify(stackTop.Symbol)}",
					LexingStream.CurrentToken().Line, 
					LexingStream.CurrentToken().Column
				));

                /// Если символ на вершине стека совпадает с текущим токеном
                if(stackTop.Symbol == token.Name)
                {
                    /// Если текущий токен - признак пропуска символов, запускаем алгоритм
                    if (token.Name == Grammar.ANY_TOKEN_NAME)
                    {
						Decisions.Push(new FinishAnyDecision()
						{
							AttemptsCount = 0,
							DecisionTokenIndex = LexingStream.CurrentTokenIndex
						});

						Stack.FlushBatch();

						var node = Stack.Pop();
						token = SkipAny(node);

						if (token.Name != Grammar.ERROR_TOKEN_NAME)
							continue;
					}
                    /// иначе читаем следующий токен
                    else
                    {
						var node = Stack.Pop();

						node.SetAnchor(token.StartOffset, token.EndOffset);
						node.SetValue(token.Text);

                        token = LexingStream.NextToken();

						continue;
                    }
                }

                /// Если на вершине стека нетерминал, выбираем альтернативу по таблице
                if (grammar[stackTop.Symbol] is NonterminalSymbol)
                {
                    var alternatives = Table[stackTop.Symbol, token.Name];
					var anyAlternatives = Table[stackTop.Symbol, Grammar.ANY_TOKEN_NAME]
						.Where(alt=>!alternatives.Contains(alt)).ToList();

					Alternative alternativeToApply = null;

					/// Сообщаем об ошибке в случае неоднозначной грамматики,
					/// либо запоминаем точку для возможного бэктрекинга
					if (alternatives.Count > 1)
					{
						if (grammar.Options.IsSet(ParsingOption.BACKTRACKING) 
							&& !Decisions.Any(d => d is ChooseAlternativeDecision
								 && d.DecisionTokenIndex == LexingStream.CurrentTokenIndex
								 && ((ChooseAlternativeDecision)d).Alternatives[0].NonterminalSymbolName == stackTop.Symbol))
						{
							Decisions.Push(new ChooseAlternativeDecision()
							{
								DecisionTokenIndex = LexingStream.CurrentTokenIndex,
								Alternatives = token.Name != Grammar.ANY_TOKEN_NAME 
									? alternatives.Concat(anyAlternatives).ToList() 
									: alternatives,
								ChosenIndex = 0
							});

							alternativeToApply = alternatives[0];
							Stack.FlushBatch();
						}
						else
						{
							Errors.Add(Message.Error(
								$"Неоднозначная грамматика: для нетерминала {grammar.Userify(stackTop.Symbol)} и входного символа {grammar.Userify(token.Name)} допустимо несколько альтернатив",
								token.Line,
								token.Column
							));

							break;
						}
					}
					/// Если же в ячейке ровно одна альтернатива
					else if (alternatives.Count == 1)
					{
						alternativeToApply = alternatives.Single();

						if (grammar.Options.IsSet(ParsingOption.BACKTRACKING) 
							&& anyAlternatives.Count > 0 
							&& token.Name != Grammar.ANY_TOKEN_NAME
							&& !Decisions.Any(d=>d is ChooseAlternativeDecision 
								&& d.DecisionTokenIndex == LexingStream.CurrentTokenIndex 
								&& ((ChooseAlternativeDecision)d).Alternatives[0].NonterminalSymbolName == stackTop.Symbol))
						{
							Decisions.Push(new ChooseAlternativeDecision()
							{
								DecisionTokenIndex = LexingStream.CurrentTokenIndex,
								Alternatives = alternatives.Concat(anyAlternatives).ToList(),
								ChosenIndex = 0
							});

							Stack.FlushBatch();
						}
					}

					if(alternativeToApply != null)
					{
                        /// снимаем со стека нетерминал и кладём её на стек
                        Stack.Pop();

                        for (var i = alternativeToApply.Count - 1; i >= 0; --i)
                        {
                            var newNode = new Node(alternativeToApply[i].Symbol, alternativeToApply[i].Options);

                            stackTop.AddFirstChild(newNode);
                            Stack.Push(newNode);
                        }

                        continue;
                    }
                }

				/// Если не смогли ни сопоставить текущий токен с терминалом на вершине стека,
				/// ни найти ветку правила для нетерминала на вершине стека
				if (token.Name == Grammar.ANY_TOKEN_NAME)
				{
					token = LexingStream.CurrentToken();

					var errorToken = token;
					var errorStackTop = stackTop.Symbol;

					Errors.Add(Message.Error(
							grammar.Tokens.ContainsKey(errorStackTop) ?
								$"Неожиданный символ {GetTokenInfoForMessage(errorToken)}, ожидался символ {grammar.Userify(errorStackTop)}" :
								$"Неожиданный символ {GetTokenInfoForMessage(errorToken)}, ожидался один из следующих символов: {String.Join(", ", Table[errorStackTop].Where(t => t.Value.Count > 0).Select(t => grammar.Userify(t.Key)))}",
							errorToken.Line,
							errorToken.Column
						));

					if (grammar.Options.IsSet(ParsingOption.BACKTRACKING))
					{
						Stack.FlushBatch();

						var recovered = Backtrack();

						if (!recovered)
						{
							var message = Message.Error(
								$"Не удалось возобновить разбор",
								errorToken.Line,
								errorToken.Column
							);

							Errors.Add(message);
							Log.Add(message);

							break;
						}
						else
						{
							token = LexingStream.CurrentToken();
						}
					}
					else
						break;
				}
				else if (token.Name == Grammar.ERROR_TOKEN_NAME)
				{
					if (grammar.Options.IsSet(ParsingOption.BACKTRACKING))
					{
						Stack.FlushBatch();
						Stack.Undo();
						Decisions.Pop();

						var recovered = Backtrack();

						if (!recovered)
						{
							var message = Message.Error(
								$"Не удалось возобновить разбор",
								null
							);

							Errors.Add(message);
							Log.Add(message);

							break;
						}
						else
						{
							token = LexingStream.CurrentToken();
						}
					}
					else
						break;
				}
				/// Если непонятно, что делать с текущим токеном, и он конкретный
				/// (не Any), заменяем его на Any
				else
				{
					/// Если встретился неожиданный токен, но он в списке пропускаемых
					if (grammar.Options.IsSet(ParsingOption.SKIP, token.Name))
					{
						token = LexingStream.NextToken();
					}
					else
					{
						token = Lexer.CreateToken(Grammar.ANY_TOKEN_NAME);
					}
				}
			}

			TreePostProcessing(root);

			return root;
		}

		/// <summary>
		/// Пропуск токенов в позиции, задаваемой символом Any
		/// </summary>
		/// <returns>
		/// Токен, найденный сразу после символа Any
		/// </returns>
		private IToken SkipAny(Node anyNode)
		{
			IToken token = LexingStream.CurrentToken();
			HashSet<string> tokensAfterText;

			/// Если с Any не связана последовательность стоп-символов
			if (anyNode.Options.AnySyncTokens.Count == 0)
			{
				/// Создаём последовательность символов, идущих в стеке после Any
				var alt = new Alternative();
				foreach (var elem in Stack)
					alt.Add(elem.Symbol);

				/// Определяем множество токенов, которые могут идти после Any
				tokensAfterText = grammar.First(alt);
				/// Само Any во входном потоке нам и так не встретится, а вывод сообщения об ошибке будет красивее
				tokensAfterText.Remove(Grammar.ANY_TOKEN_NAME);
			}
			else
			{
				tokensAfterText = anyNode.Options.AnySyncTokens;
			}

			/// Если Any непустой (текущий токен - это не токен,
			/// который может идти после Any)
			if (!tokensAfterText.Contains(token.Name))
			{
				/// Проверка на случай, если допропускаем текст в процессе восстановления
				if (!anyNode.StartOffset.HasValue)
					anyNode.SetAnchor(token.StartOffset, token.EndOffset);

				/// Смещение для участка, подобранного как текст
				int endOffset = token.EndOffset;

				while (!tokensAfterText.Contains(token.Name)
					&& !anyNode.Options.AnyErrorTokens.Contains(token.Name)
					&& token.Name != Grammar.EOF_TOKEN_NAME)
				{
					anyNode.Value.Add(token.Text);
					endOffset = token.EndOffset;

					token = LexingStream.NextToken();
				}

				anyNode.SetAnchor(anyNode.StartOffset.Value, endOffset);

				/// Если дошли до конца входной строки, и это было не по плану
				if (token.Name == Grammar.EOF_TOKEN_NAME && !tokensAfterText.Contains(token.Name)
					|| anyNode.Options.AnyErrorTokens.Contains(token.Name))
				{
					LogError(Message.Error(
						$"Ошибка при пропуске {Grammar.ANY_TOKEN_NAME}: неожиданный токен {grammar.Userify(token.Name)}, ожидался один из следующих символов: { String.Join(", ", tokensAfterText.Select(t => grammar.Userify(t))) }",
						null
					));

					return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
				}
			}


			Decisions.Peek().DecisionTokenIndex = LexingStream.CurrentTokenIndex;
			return token;
		}

		private string GetTokenInfoForMessage(IToken token)
		{
			var userified = grammar.Userify(token.Name);
			if (userified == token.Name && token.Name != Grammar.ANY_TOKEN_NAME && token.Name != Grammar.EOF_TOKEN_NAME)
				return $"{token.Name}: '{token.Text}'";
			else
				return userified;
		}

		/// <summary>
		/// Восстановление в случае ошибки разбора - 
		/// </summary>
		private bool Backtrack()
		{
            BacktrackingCounter += 1;

			var errorTokenIndex = LexingStream.CurrentTokenIndex;

			while (Decisions.Count > 0)
			{
				var prevActionTokenIndex = LexingStream.CurrentTokenIndex;
				var decision = Decisions.Peek();
				Stack.Undo();			

				/// Если текущее решение касается выбора альтернативы
				if (decision is ChooseAlternativeDecision)
				{
					var choice = (ChooseAlternativeDecision)decision;

					if (choice.ChosenIndex < choice.Alternatives.Count - 1)
					{
						var alternativeToApply = choice.Alternatives[++choice.ChosenIndex];
						var stackTop = Stack.Peek();

						Stack.Pop();

						for (var i = alternativeToApply.Count - 1; i >= 0; --i)
						{
							var newNode = new Node(alternativeToApply[i].Symbol, alternativeToApply[i].Options);
							stackTop.AddFirstChild(newNode);
							Stack.Push(newNode);
						}

						LogError(Message.Warning(
							$"BACKTRACKING: успех, смена ветки для {grammar.Userify(alternativeToApply.NonterminalSymbolName)} на {String.Join(" ", alternativeToApply.Elements.Select(e => grammar.Userify(e)))}, разбор продолжен с токена {GetTokenInfoForMessage(LexingStream.CurrentToken())}",
							LexingStream.CurrentToken().Line,
							LexingStream.CurrentToken().Column
						));

						return true;
					}
				}

				if (decision is FinishAnyDecision)
				{
					var finish = (FinishAnyDecision)decision;

					if (finish.AttemptsCount < MAX_RECOVERY_ATTEMPTS)
					{
						/// Пытаемся продлить область, которая ему соответствует
						/// Если уже пытались восстановиться в этом же месте,
						/// продлеваем текст до и дальше того символа,
						/// на котором восстанавливались в прошлый раз
						LexingStream.BackToToken(finish.DecisionTokenIndex);

						LogError(Message.Warning(
								$"BACKTRACKING: попытка продлить Any с токена {GetTokenInfoForMessage(LexingStream.CurrentToken())}",
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
							Stack.Peek().Value.Add(currentToken.Text);
							textEnd = currentToken.EndOffset;
							currentToken = LexingStream.NextToken();
							break; /// Пока попробуем пропускать не цепочку токенов, а один токен
						}

						Stack.Peek().SetAnchor(Stack.Peek().StartOffset.HasValue ? Stack.Peek().StartOffset.Value : textStart, textEnd);

						/// Пропускаем текст дальше
						if (SkipAny(Stack.Pop()).Name != Grammar.ERROR_TOKEN_NAME)
						{
							LogError(Message.Warning(
								$"BACKTRACKING: успех, разбор продолжен с токена {GetTokenInfoForMessage(LexingStream.CurrentToken())}",
								LexingStream.CurrentToken().Line,
								LexingStream.CurrentToken().Column
							));

							finish.DecisionTokenIndex = LexingStream.CurrentTokenIndex;
							finish.AttemptsCount += 1;

							return true;
						}
						else
						{
							Stack.FlushBatch();
							Stack.Undo();
						}
					}
				}

				Decisions.Pop();
			}

			return false;
		}

		private void LogError(Message message)
		{
			Errors.Add(message);
			Log.Add(message);
		}
	}
}
