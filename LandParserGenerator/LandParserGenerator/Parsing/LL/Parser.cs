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

        public int ErrorRecoveriesCounter { get; private set; }

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

            LastRecoveryAction = null;
            ErrorRecoveriesCounter = 0;

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

				Log.Add(Message.Trace(
					$"Текущий токен: {GetTokenInfoForMessage(token)} | Символ на вершине стека: {grammar.Userify(stackTop.Symbol)}",
					LexingStream.CurrentToken().Line, 
					LexingStream.CurrentToken().Column
				));

                /// Если символ на вершине стека совпадает с текущим токеном
                if(stackTop.Symbol == token.Name)
                {
                    /// Снимаем узел со стека и устанавливаем координаты в координаты токена
                    var node = Stack.Pop();

                    /// Если текущий токен - признак пропуска символов, запускаем алгоритм
                    if (token.Name == Grammar.TEXT_TOKEN_NAME)
                    {
                        token = SkipAny(node);
						/// Если при пропуске текста произошла ошибка, прерываем разбор
                        if(token.Name == Grammar.ERROR_TOKEN_NAME)
							break;
                    }
                    /// иначе читаем следующий токен
                    else
                    {
                        node.SetAnchor(token.StartOffset, token.EndOffset);
						node.SetValue(token.Text);

                        token = LexingStream.NextToken();
                    }

                    continue;
                }

                /// Если на вершине стека нетерминал, выбираем альтернативу по таблице
                if (grammar[stackTop.Symbol] is NonterminalSymbol)
                {
                    var alternatives = Table[stackTop.Symbol, token.Name];
                    Alternative alternativeToApply = null;

                    /// Сообщаем об ошибке в случае неоднозначной грамматики
                    if (alternatives.Count > 1)
                    {
						Errors.Add(Message.Error(
							$"Неоднозначная грамматика: для нетерминала {grammar.Userify(stackTop.Symbol)} и входного символа {grammar.Userify(token.Name)} допустимо несколько альтернатив",
							token.Line,
							token.Column
						));
						break;
                    }
                    /// Если же в ячейке ровно одна альтернатива
                    else if (alternatives.Count == 1)
                    {
                        alternativeToApply = alternatives.Single();
                        /// снимаем со стека нетерминал и кладём её на стек
                        Stack.InitBatch();
                        Stack.Pop();

                        for (var i = alternativeToApply.Count - 1; i >= 0; --i)
                        {
                            var newNode = new Node(alternativeToApply[i].Symbol, alternativeToApply[i].Options);

                            stackTop.AddFirstChild(newNode);
                            Stack.Push(newNode);
                        }

                        Stack.FinBatch();

                        continue;
                    }
                }

                /// Если не смогли ни сопоставить текущий токен с терминалом на вершине стека,
                /// ни найти ветку правила для нетерминала на вершине стека
                if(token.Name == Grammar.TEXT_TOKEN_NAME)
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

					var recovered = false;

					try
					{
						var message = Message.Warning(
							$"Запущен алгоритм восстановления...",
							errorToken.Line,
							errorToken.Column
						);

						Errors.Add(message);
						Log.Add(message);

						recovered = ErrorRecovery();
					}
					catch { }

					if(!recovered)
					{
						var message = Message.Warning(
							$"Не удалось продолжить разбор",
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
                        token = Lexer.CreateToken(Grammar.TEXT_TOKEN_NAME);
                    }
                }
			}

			TreePostProcessing(root);

			return root;
		}

		private void TreePostProcessing(Node root)
		{
			root.Accept(new GhostListOptionProcessingVisitor(grammar));
			root.Accept(new LeafOptionProcessingVisitor(grammar));
			root.Accept(new MappingOptionsProcessingVisitor(grammar));
			root.Accept(new UserifyVisitor(grammar));
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

			/// Создаём последовательность символов, идущих в стеке после Any
			var alt = new Alternative();
			foreach (var elem in Stack)
				alt.Add(elem.Symbol);

			/// Определяем множество токенов, которые могут идти после Any
			var tokensAfterText = grammar.First(alt);
			/// Само Any во входном потоке нам и так не встретится, а вывод сообщения об ошибке будет красивее
			tokensAfterText.Remove(Grammar.TEXT_TOKEN_NAME);

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
                    && token.Name != Grammar.EOF_TOKEN_NAME)
				{
					anyNode.Value.Add(token.Text);
					endOffset = token.EndOffset;

					token = LexingStream.NextToken();
				}

				anyNode.SetAnchor(anyNode.StartOffset.Value, endOffset);

                /// Если дошли до конца входной строки, и это было не по плану
                if(token.Name == Grammar.EOF_TOKEN_NAME 
                    && !tokensAfterText.Contains(token.Name))
                {
					Errors.Add(Message.Error(
						$"Ошибка при пропуске токенов: неожиданный конец файла, ожидался один из следующих символов: { String.Join(", ", tokensAfterText.Select(t => grammar.Userify(t))) }",
						null
					));

                    return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
                }
			}		

			return token;
		}

		private string GetTokenInfoForMessage(IToken token)
		{
			var userified = grammar.Userify(token.Name);
			if (userified == token.Name && token.Name != Grammar.TEXT_TOKEN_NAME && token.Name != Grammar.EOF_TOKEN_NAME)
				return $"{token.Name}: '{token.Text}'";
			else
				return userified;
		}

		/// <summary>
		/// Восстановление в случае ошибки разбора - 
		/// </summary>
		private bool ErrorRecovery()
		{
            ErrorRecoveriesCounter += 1;
			Message message = null;

			var errorTokenIndex = LexingStream.CurrentTokenIndex;

			while (Stack.Count > 0)
			{
				var prevActionTokenIndex = LexingStream.CurrentTokenIndex;
				Stack.Undo();

				/// Если на вершине стека оказался нетерминал
				if (grammar[Stack.Peek().Symbol] is NonterminalSymbol)
				{
					/// Проверяем, есть ли у него ветка для Any
					/// и является ли она приоритетной для текущего lookahead-а
					if (Table[Stack.Peek().Symbol, LexingStream.CurrentToken().Name].Count == 1 
						&& Table[Stack.Peek().Symbol, Grammar.TEXT_TOKEN_NAME].Count == 1)
					{
						/// Если уже восстанавливались в том же месте тем же образом
						if (LastRecoveryAction != null
							&& LastRecoveryAction.ActionType == RecoveryActionType.UseSecondaryTextAlt
							&& LexingStream.CurrentTokenIndex == LastRecoveryAction.RecoveryStartTokenIndex)
							return false;

						var alternativeToApply = Table[Stack.Peek().Symbol, Grammar.TEXT_TOKEN_NAME].Single();
						var stackTop = Stack.Peek();

						/// Определившись с альтернативой, снимаем со стека нетерминал и кладём её на стек
						Stack.InitBatch();
						/// снимаем со стека нетерминал и кладём содержимое его альтернативы
						Stack.Pop();

						for (var i = alternativeToApply.Count - 1; i >= 0; --i)
						{
							var newNode = new Node(alternativeToApply[i].Symbol, alternativeToApply[i].Options);
							stackTop.AddFirstChild(newNode);
							Stack.Push(newNode);
						}

						Stack.FinBatch();

						/// Выполнили восстановление "переход на неприоритетную альтернативу,
						/// начинающуюся с символа Any"
						LastRecoveryAction = new RecoveryAction()
						{
							ActionType = RecoveryActionType.UseSecondaryTextAlt,
							RecoveryEndTokenIndex = LexingStream.CurrentTokenIndex,
							RecoveryStartTokenIndex = LexingStream.CurrentTokenIndex,
							ErrorTokenIndex = errorTokenIndex,
							AttemptsCount = 1
						};

						message = Message.Warning(
							$"Восстановление: переход на неприоритетную ветку для нетерминала {grammar.Userify(alternativeToApply.NonterminalSymbolName)}, разбор продолжен с токена {GetTokenInfoForMessage(LexingStream.CurrentToken())}",
							LexingStream.CurrentToken().Line, 
							LexingStream.CurrentToken().Column
						);

						Errors.Add(message);
						Log.Add(message);

						return true;
					}
				}

				/// Если на вершине стека оказался символ Any
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

                    var textStart = currentToken.StartOffset;
					var textEnd = currentToken.EndOffset;

					while (currentToken.Name == tokenToSkip.Name)
					{
						textEnd = currentToken.EndOffset;
						currentToken = LexingStream.NextToken();
						break; /// Пока попробуем пропускать не цепочку токенов, а один токен
					}

					Stack.Peek().SetAnchor(Stack.Peek().StartOffset.HasValue ? Stack.Peek().StartOffset.Value : textStart, textEnd);

					/// Пропускаем текст дальше
					if(SkipAny(Stack.Pop()).Name == Grammar.ERROR_TOKEN_NAME)
                    {
                        return false;
                    }

					LastRecoveryAction = new RecoveryAction()
					{
						ActionType = RecoveryActionType.SkipMoreText,
						RecoveryEndTokenIndex = LexingStream.CurrentTokenIndex,
						RecoveryStartTokenIndex = prevActionTokenIndex,
						ErrorTokenIndex = errorTokenIndex,
						AttemptsCount = prevAttemptsCount + 1
					};

					message = Message.Warning(
						$"Восстановление: пропуск большего количества токенов в качестве Any, разбор продолжен с токена {GetTokenInfoForMessage(LexingStream.CurrentToken())}",
						LexingStream.CurrentToken().Line, 
						LexingStream.CurrentToken().Column
					);

					Errors.Add(message);
					Log.Add(message);

					return true;
				}
			}

			return false;
		}
	}
}
