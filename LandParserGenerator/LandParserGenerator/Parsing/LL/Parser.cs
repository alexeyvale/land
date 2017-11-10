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

		private RecoveryAction LastRecoveryAction { get; set; }

		public Parser(Grammar g, ILexer lexer): base(g, lexer)
		{
			Table = new TableLL1(g);
		}

		/// <summary>
		/// LL(1) разбор
		/// </summary>
		/// <returns>
		/// Корень дерева разбора
		/// </returns>
		public override Node Parse(string text, out string errorMessage)
		{
			Log = new List<string>();
			errorMessage = String.Empty;

			/// Готовим лексер
			LexingStream = new TokenStream(Lexer, text);

			/// Кладём на стек стартовый символ
			Stack = new ParsingStack(LexingStream);
			var root = new Node(grammar.StartSymbol);
			Stack.Push(new Node(Grammar.EOF_TOKEN_NAME));
			Stack.Push(root);

			/// Читаем первую лексему из входного потока
			var token = LexingStream.NextToken();

			/// Пока не прошли полностью правило для стартового символа
			while (Stack.Count > 0)
			{
				var stackTop = Stack.Peek();

				Log.Add($"Текущий токен: {token.Name}: '{token.Text}'; символ на вершине стека: {stackTop.Symbol}");

                /// Если в текущем месте должен быть пропуск текста
			    if (stackTop.Symbol == "TEXT")
                {
                    /// Пропускаем и переходим к новой итерации
                    token = SkipText();
                    continue;
                }

                /// Если на вершине стека терминал, сопоставляем его с текущей лексемой
                if (grammar[stackTop.Symbol] is TerminalSymbol)
				{
					/// Если текущий токен совпадает с ожидаемым
					if (stackTop.Symbol == token.Name)
					{
						/// Снимаем его со стека
						stackTop.SetAnchor(token.StartOffset, token.EndOffset);
						Stack.Pop();
						/// Читаем следующий токен
						token = LexingStream.NextToken();
						continue;
					}
					/// Если встретился неожиданный токен, но он в списке пропускаемых
					else if (grammar.SkipTokens.Contains(token.Name))
					{
						token = LexingStream.NextToken();
						continue;
					}
					else if (token.Name == Grammar.EOF_TOKEN_NAME)
					{
						errorMessage = String.Format($"Неожиданный конец файла");
						return root;
					}
					else
					{
						var errorToken = token;
						var errorStackTop = stackTop.Symbol;

						if (!ErrorRecovery())
						{
							errorMessage = 
								String.Format($"Неожиданный символ {errorToken.Name}: '{errorToken.Text}', ожидалось {errorStackTop}");
							return root;
						}
						else
						{
							token = LexingStream.CurrentToken();
							continue;
						}				
					}
				}

				/// Если на вершине стека нетерминал, выбираем альтернативу по таблице
				if (grammar[stackTop.Symbol] is NonterminalSymbol)
				{
					var alternatives = Table[stackTop.Symbol, token.Name];
					Alternative alternativeToApply = null;

					/// Сообщаем об ошибке в случае неоднозначной грамматики
					if (alternatives.Count > 1)
					{
						errorMessage = 
							String.Format(
								$"Неоднозначная грамматика: для нетерминала {stackTop.Symbol} и входного символа {token.Name} допустимо несколько альтернатив"
							);
						return root;
					}
					/// Отдельно обрабатываем случай, когда нет записи в таблице
					else if (alternatives.Count == 0)
					{
						/// Если встретился неожиданный токен, но он в списке пропускаемых
						if (grammar.SkipTokens.Contains(token.Name))
						{
							token = LexingStream.NextToken();
							continue;
						}
						else
						{
							var errorToken = token;

							if (!ErrorRecovery())
							{
								errorMessage = String.Format(
									$"Неожиданный символ {errorToken.Name}: '{errorToken.Text}'");
								return root;
							}
							else
							{
								token = LexingStream.CurrentToken();
								continue;
							}
						}
					}
					/// Самый простой случай - в ячейке ровно одна альтернатива
					else
					{
						alternativeToApply = alternatives.Single();
					}

					/// Определившись с альтернативой, снимаем со стека нетерминал и кладём её на стек
					Stack.InitBatch();

					/// снимаем со стека нетерминал и кладём содержимое его альтернативы
					Stack.Pop();

					for (var i = alternativeToApply.Count - 1; i >= 0; --i)
					{
						var newNode = new Node(alternativeToApply[i]);

						stackTop.AddFirstChild(newNode);
						Stack.Push(newNode);
					}

					Stack.FinBatch();

					continue;
				}
			}

			TreePostproc(root);

			return root;
		}

		private void TreePostproc(Node root)
		{
			ListVisitor visitor = new ListVisitor(grammar);
			root.Accept(visitor);
		}

		/// <summary>
		/// Пропуск токенов в позиции, задаваемой символом TEXT
		/// </summary>
		/// <returns>
		/// Токен, найденный сразу после символа TEXT
		/// </returns>
		private IToken SkipText()
		{
			IToken token = LexingStream.CurrentToken();

			/// В ситуации, когда на стеке друг за другом идёт несколько TEXT,
			/// доходим до самого последнего
			Node textNode;
			do
			{
				textNode = Stack.Pop();
			}
			while (Stack.Peek().Symbol == Grammar.TEXT_TOKEN_NAME);

			/// Проверка на случай, если допропускаем текст в процессе восстановления
			if (!textNode.StartOffset.HasValue)
				textNode.SetAnchor(token.StartOffset, token.EndOffset);

			/// Создаём последовательность символов, идущих в стеке после TEXT
			var alt = new Alternative();
			foreach (var elem in Stack)
				alt.Add(elem.Symbol);

			/// Определяем множество токенов, которые могут идти после TEXT
			var tokensAfterText = grammar.First(alt);

			/// Если TEXT непустой (текущий токен - это не токен,
			/// который может идти после TEXT)
			if (!tokensAfterText.Contains(token.Name))
			{
				/// Смещения для участка, подобранного как текст
				int startOffset = token.StartOffset;
				int endOffset = token.EndOffset;

				while (!tokensAfterText.Contains(token.Name) 
                    && token.Name != Grammar.EOF_TOKEN_NAME)
				{
					endOffset = token.EndOffset;
					token = LexingStream.NextToken();
				}

				textNode.SetAnchor(textNode.StartOffset.Value, endOffset);
			}		

			return token;
		}

		/// <summary>
		/// Восстановление в случае ошибки разбора - 
		/// </summary>
		private bool ErrorRecovery()
		{
			Log.Add($"Запущен алгоритм восстановления...");

			var errorTokenIndex = LexingStream.CurrentTokenIndex;

			while (Stack.Count > 0)
			{
				var prevActionTokenIndex = LexingStream.CurrentTokenIndex;
				Stack.Undo();

				/// Если на вершине стека оказался нетерминал
				if (grammar[Stack.Peek().Symbol] is NonterminalSymbol)
				{
					var alternativesStartsWithText = Table[Stack.Peek().Symbol].Values
						.SelectMany(e => e)
						.Distinct()
						.Where(alt => alt.Count > 0 && alt[0] == Grammar.TEXT_TOKEN_NAME)
						.ToList();

					/// Проверяем, есть ли у него ветка для TEXT
					/// и является ли она приоритетной для текущего lookahead-а
					if (alternativesStartsWithText.Count == 1 && 
						!Table[Stack.Peek().Symbol, LexingStream.CurrentToken().Name].Contains(alternativesStartsWithText[0]))
					{
						/// Если уже восстанавливались в том же месте тем же образом
						if (LastRecoveryAction != null
							&& LastRecoveryAction.ActionType == RecoveryActionType.UseSecondaryTextAlt
							&& LexingStream.CurrentTokenIndex == LastRecoveryAction.RecoveryStartTokenIndex)
							return false;

						var alternativeToApply = alternativesStartsWithText[0];
						var stackTop = Stack.Peek();

						/// Определившись с альтернативой, снимаем со стека нетерминал и кладём её на стек
						Stack.InitBatch();
						/// снимаем со стека нетерминал и кладём содержимое его альтернативы
						Stack.Pop();

						for (var i = alternativeToApply.Count - 1; i >= 0; --i)
						{
							var newNode = new Node(alternativeToApply[i]);
							stackTop.AddFirstChild(newNode);
							Stack.Push(newNode);
						}

						Stack.FinBatch();

						/// Выполнили восстановление "переход на неприоритетную альтернативу,
						/// начинающуюся с символа TEXT"
						LastRecoveryAction = new RecoveryAction()
						{
							ActionType = RecoveryActionType.UseSecondaryTextAlt,
							RecoveryEndTokenIndex = LexingStream.CurrentTokenIndex,
							RecoveryStartTokenIndex = LexingStream.CurrentTokenIndex,
							ErrorTokenIndex = errorTokenIndex,
							AttemptsCount = 1
						};

						Log.Add($"Восстановление: переход на неприоритетную ветку для {alternativeToApply.NonterminalSymbolName}");
						Log.Add($"Разбор продолжен с символа {LexingStream.CurrentToken().Name}: '{LexingStream.CurrentToken().Text}'");

						return true;
					}

					//if (Table[Stack.Peek().Symbol, "ERROR"].Count == 1)
					//{
					//	return Table[Stack.Peek().Symbol, "ERROR"].Single();
					//}
				}

				/// Если на вершине стека оказался символ TEXT
				if (Stack.Peek().Symbol == Grammar.TEXT_TOKEN_NAME)
				{
					var prevAttemptsCount = LastRecoveryAction != null 
						&& LastRecoveryAction.ActionType == RecoveryActionType.SkipMoreText
						&& LastRecoveryAction.RecoveryStartTokenIndex == prevActionTokenIndex  ?
						LastRecoveryAction.AttemptsCount : 0;

					if (prevAttemptsCount == MAX_RECOVERY_ATTEMPTS)
						return false;

					/// Пытаемся продлить область, которая ему соответствует
					/// Если уже пытались восстановиться в этом же месте,
					/// продлеваем текст до и дальше того символа,
					/// на котором восстанавливались в прошлый раз
					LexingStream.BackToToken(prevAttemptsCount == 0 ? prevActionTokenIndex : LastRecoveryAction.RecoveryEndTokenIndex);

					/// Включаем в текст цепочку однотипных токенов,
					/// на которых ранее прекратили подбор текста
					var tokenToSkip = LexingStream.CurrentToken();
					var currentToken = tokenToSkip;
					var textEnd = currentToken.EndOffset;

					while (currentToken.Name == tokenToSkip.Name)
					{
						textEnd = currentToken.EndOffset;
						currentToken = LexingStream.NextToken();
						break; /// Пока попробуем пропускать не цепочку токенов, а один токен
					}

					Stack.Peek().SetAnchor(Stack.Peek().StartOffset.Value, textEnd);

					/// Пропускаем текст дальше
					SkipText();

					LastRecoveryAction = new RecoveryAction()
					{
						ActionType = RecoveryActionType.SkipMoreText,
						RecoveryEndTokenIndex = LexingStream.CurrentTokenIndex,
						RecoveryStartTokenIndex = prevActionTokenIndex,
						ErrorTokenIndex = errorTokenIndex,
						AttemptsCount = prevAttemptsCount + 1
					};

					Log.Add($"Восстановление: пропуск большего количества токенов в качестве TEXT");
					Log.Add($"Разбор продолжен с символа {LexingStream.CurrentToken().Name}: '{LexingStream.CurrentToken().Text}'");

					return true;
				}
			}

			return false;
		}
	}
}
