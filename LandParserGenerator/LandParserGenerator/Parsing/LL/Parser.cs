using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;
using Land.Core.Parsing.Tree;

namespace Land.Core.Parsing.LL
{
	public class Parser: BaseParser
	{
		private const int MAX_RECOVERY_ATTEMPTS = 5;

		private TableLL1 Table { get; set; }
		private Stack<Node> Stack { get; set; }
		private TokenStream LexingStream { get; set; }

		/// <summary>
		/// Стек открытых на момент прочтения последнего токена пар
		/// </summary>
		private Stack<PairSymbol> Nesting { get; set; }
		/// <summary>
		/// Уровень вложенности относительно описанных в грамматике пар,
		/// на котором начался разбор нетерминала
		/// </summary>
		private Dictionary<Node, int> NestingLevel { get; set; }

		private HashSet<int> PositionsWhereRecoveryStarted { get; set; }

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
		protected override Node ParsingAlgorithm(string text)
		{
			/// Контроль вложенностей пар
			Nesting = new Stack<PairSymbol>();
			NestingLevel = new Dictionary<Node, int>();
			PositionsWhereRecoveryStarted = new HashSet<int>();

			/// Готовим лексер и стеки
			LexingStream = new TokenStream(Lexer, text);
			Stack = new Stack<Node>();

			/// Кладём на стек стартовый символ
			var root = new Node(GrammarObject.StartSymbol);
			Stack.Push(new Node(Grammar.EOF_TOKEN_NAME));
			Stack.Push(root);

			/// Читаем первую лексему из входного потока
			var token = GetNextToken();

			/// Пока не прошли полностью правило для стартового символа
			while (Stack.Count > 0)
			{
				if (token.Name == Grammar.ERROR_TOKEN_NAME)
					break;

				var stackTop = Stack.Peek();

				Log.Add(Message.Trace(
					$"Текущий токен: {GetTokenInfoForMessage(token)} | Символ на вершине стека: {GrammarObject.Userify(stackTop.Symbol)}",
					LexingStream.CurrentToken.Location.Start
				));

                /// Если символ на вершине стека совпадает с текущим токеном
                if(stackTop.Symbol == token.Name)
                {
					/// Снимаем узел со стека и устанавливаем координаты в координаты токена
					var node = Stack.Pop();

					/// Если текущий токен - признак пропуска символов, запускаем алгоритм
					if (token.Name == Grammar.ANY_TOKEN_NAME)
					{
						token = SkipAny(node, true);
					}
					/// иначе читаем следующий токен
					else
					{
						node.SetAnchor(token.Location.Start, token.Location.End);
						node.SetValue(token.Text);

						token = GetNextToken();
					}

					continue;
				}

				/// Если на вершине стека нетерминал, выбираем альтернативу по таблице
				if (GrammarObject[stackTop.Symbol] is NonterminalSymbol)
				{
					var alternatives = Table[stackTop.Symbol, token.Name];
					Alternative alternativeToApply = null;

					/// Сообщаем об ошибке в случае неоднозначной грамматики
					if (alternatives.Count > 1)
					{
						Log.Add(Message.Error(
							$"Неоднозначная грамматика: для нетерминала {GrammarObject.Userify(stackTop.Symbol)} и входного символа {GrammarObject.Userify(token.Name)} допустимо несколько альтернатив",
							token.Location.Start
						));
						break;
					}
					/// Если же в ячейке ровно одна альтернатива
					else if (alternatives.Count == 1)
					{
						alternativeToApply = alternatives.Single();
						Stack.Pop();

						if (!String.IsNullOrEmpty(alternativeToApply.Alias))
							stackTop.Alias = alternativeToApply.Alias;

						NestingLevel[stackTop] = Nesting.Count;

						for (var i = alternativeToApply.Count - 1; i >= 0; --i)
						{
							var newNode = new Node(alternativeToApply[i].Symbol, alternativeToApply[i].Options.Clone());

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
					Log.Add(Message.Warning(
						GrammarObject.Tokens.ContainsKey(stackTop.Symbol) ?
							$"Неожиданный символ {GetTokenInfoForMessage(LexingStream.CurrentToken)}, ожидался символ {GrammarObject.Userify(stackTop.Symbol)}" :
							$"Неожиданный символ {GetTokenInfoForMessage(LexingStream.CurrentToken)}, ожидался один из следующих символов: {String.Join(", ", Table[stackTop.Symbol].Where(t => t.Value.Count > 0).Select(t => GrammarObject.Userify(t.Key)))}",
						LexingStream.CurrentToken.Location.Start
					));

					token = ErrorRecovery();
				}
				/// Если непонятно, что делать с текущим токеном, и он конкретный
				/// (не Any), заменяем его на Any
				else
				{
					/// Если встретился неожиданный токен, но он в списке пропускаемых
					if (GrammarObject.Options.IsSet(ParsingOption.SKIP, token.Name))
					{
						token = GetNextToken();
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

		private IToken GetNextToken()
		{
			if (LexingStream.CurrentToken != null)
			{
				var token = LexingStream.CurrentToken;
				var closed = GrammarObject.Pairs.FirstOrDefault(p => p.Value.Right.Contains(token.Name));

				if (closed.Value != null)
				{
					if (Nesting.Count == 0)
					{
						Log.Add(Message.Error(
							$"Отсутствует открывающая конструкция для парной закрывающей {GetTokenInfoForMessage(token)}",
							token.Location.Start
						));

						return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
					}
					else if(Nesting.Peek() != closed.Value)
					{
						Log.Add(Message.Error(
							$"Неожиданная закрывающая конструкция {GetTokenInfoForMessage(token)}, ожидается {String.Join("или ", Nesting.Peek().Right.Select(e => GrammarObject.Userify(e)))} для открывающей конструкции {String.Join("или ", Nesting.Peek().Left.Select(e=> GrammarObject.Userify(e)))}",
							token.Location.Start
						));

						return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
					}
					else
					{
						Nesting.Pop();
					}
				}

				var opened = GrammarObject.Pairs.FirstOrDefault(p => p.Value.Left.Contains(token.Name));

				if (opened.Value != null)
					Nesting.Push(opened.Value);
			}

			return LexingStream.NextToken();
		}

		private IToken GetNextToken(int level, out List<IToken> skipped)
		{
			skipped = new List<IToken>();

			while(true)
			{
				var next = GetNextToken();

				if (Nesting.Count == level 
					|| next.Name == Grammar.EOF_TOKEN_NAME 
					|| next.Name == Grammar.ERROR_TOKEN_NAME)
					return next;
				else
					skipped.Add(next);
			}
		}

		/// <summary>
		/// Пропуск токенов в позиции, задаваемой символом Any
		/// </summary>
		/// <returns>
		/// Токен, найденный сразу после символа Any
		/// </returns>
		private IToken SkipAny(Node anyNode, bool enableRecovery)
		{
			var nestingCopy = new Stack<PairSymbol>(Nesting.Reverse());
			var tokenIndex = LexingStream.CurrentIndex;

			IToken token = LexingStream.CurrentToken;
			HashSet<string> tokensAfterText;

			/// Если с Any не связана последовательность стоп-символов
			if (!anyNode.Options.AnyOptions.ContainsKey(AnyOption.Except))
			{
				/// Создаём последовательность символов, идущих в стеке после Any
				var alt = new Alternative();
				foreach (var elem in Stack)
					alt.Add(elem.Symbol);

				/// Определяем множество токенов, которые могут идти после Any
				tokensAfterText = GrammarObject.First(alt);
				/// Само Any во входном потоке нам и так не встретится, а вывод сообщения об ошибке будет красивее
				tokensAfterText.Remove(Grammar.ANY_TOKEN_NAME);

				/// Если указаны токены, которые нужно однозначно включать в Any
				if (anyNode.Options.AnyOptions.ContainsKey(AnyOption.Include))
				{
					tokensAfterText.ExceptWith(anyNode.Options.AnyOptions[AnyOption.Include]);
				}
			}
			else
			{
				tokensAfterText = anyNode.Options.AnyOptions[AnyOption.Except];
			}		

			/// Если Any непустой (текущий токен - это не токен,
			/// который может идти после Any)
			if (!tokensAfterText.Contains(token.Name))
			{
				/// Проверка на случай, если допропускаем текст в процессе восстановления
				if (anyNode.Anchor == null)
					anyNode.SetAnchor(token.Location.Start, token.Location.End);

				/// Смещение для участка, подобранного как текст
				var endLocation = token.Location.End;
				var anyLevel = Nesting.Count;

				while (!tokensAfterText.Contains(token.Name)
					&& !anyNode.Options.Contains(AnyOption.Avoid, token.Name)
					&& token.Name != Grammar.EOF_TOKEN_NAME
					&& token.Name != Grammar.ERROR_TOKEN_NAME)
				{
					anyNode.Value.Add(token.Text);
					endLocation = token.Location.End;

					if (anyNode.Options.AnyOptions.ContainsKey(AnyOption.IgnorePairs))
					{
						token = GetNextToken();
					}
					else
					{
						token = GetNextToken(anyLevel, out List<IToken> skippedBuffer);

						/// Если при пропуске до токена на том же уровне
						/// пропустили токены с более глубокой вложенностью
						if (skippedBuffer.Count > 0)
						{
							anyNode.Value.AddRange(skippedBuffer.Select(t => t.Text));
							endLocation = skippedBuffer.Last().Location.End;
						}
					}
				}

				anyNode.SetAnchor(anyNode.Anchor.Start, endLocation);

				if (token.Name == Grammar.ERROR_TOKEN_NAME)
					return token;

				/// Если дошли до конца входной строки, и это было не по плану
				if (token.Name == Grammar.EOF_TOKEN_NAME && !tokensAfterText.Contains(token.Name)
					|| anyNode.Options.Contains(AnyOption.Avoid, token.Name))
				{
					var message = Message.Trace(
						$"Ошибка при пропуске {Grammar.ANY_TOKEN_NAME}: неожиданный токен {GrammarObject.Userify(token.Name)}, ожидался один из следующих символов: { String.Join(", ", tokensAfterText.Select(t => GrammarObject.Userify(t))) }",
						token.Location.Start
					);

					if (enableRecovery)
					{
						message.Type = MessageType.Warning;
						Log.Add(message);

						Nesting = nestingCopy;
						LexingStream.MoveTo(tokenIndex);
						anyNode.Reset();
						///	Возвращаем узел обратно на стек
						Stack.Push(anyNode);

						return ErrorRecovery();
					}
					else
					{
						message.Type = MessageType.Error;
						Log.Add(message);
						return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
					}
				}
			}

			return token;
		}

		private IToken ErrorRecovery()
		{
			if (!PositionsWhereRecoveryStarted.Add(LexingStream.CurrentIndex))
			{
				Log.Add(Message.Error(
					$"Возобновление разбора невозможно: восстановление в позиции токена {GetTokenInfoForMessage(LexingStream.CurrentToken)} уже проводилось",
					LexingStream.CurrentToken.Location.Start
				));

				return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
			}

			Log.Add(Message.Warning(
				$"Процесс восстановления запущен в позиции токена {GetTokenInfoForMessage(LexingStream.CurrentToken)}",
				LexingStream.CurrentToken.Location.Start
			));

			/// То, что мы хотели разобрать, и не смогли
			var currentNode = Stack.Peek();
			Stack.Pop();

			/// Если произошла ошибка при пропуске Any и конфигурация ветки
			/// может заставить нас повторно перейти на неё же при восстановлении,
			/// переходим сразу к родителю родителя этого Any
			if(currentNode.Symbol == Grammar.ANY_TOKEN_NAME 
				&& currentNode.Parent.Children.Count == 1)
			{
				/// Сместились к узлу, потомком которого было Any, 
				/// в нём точно не восстановимся, нужно подниматься дальше
				currentNode = currentNode.Parent;

				if (currentNode.Parent != null)
				{
					var childIndex = currentNode.Parent.Children.IndexOf(currentNode);

					/// Снимаем со стека всех неразобранных потомков родителя этого узла,
					/// для текущего узла они являются правыми братьями
					for (var i = 0; i < currentNode.Parent.Children.Count - childIndex - 1; ++i)
						Stack.Pop();
				}

				/// Поднимаемся к родителю
				currentNode = currentNode.Parent;
			}

			/// Поднимаемся по уже построенной части дерева, пока не встретим узел нетерминала,
			/// для которого допустима альтернатива из одного Any
			while (currentNode != null
				&& (!GrammarObject.Rules.ContainsKey(currentNode.Symbol)
				|| !GrammarObject.Rules[currentNode.Symbol].Alternatives.Any(a => a.Count == 1 && a[0].Symbol == Grammar.ANY_TOKEN_NAME)))
			{
				if (currentNode.Parent != null)
				{
					var childIndex = currentNode.Parent.Children.IndexOf(currentNode);

					for (var i = 0; i < currentNode.Parent.Children.Count - childIndex - 1; ++i)
						Stack.Pop();
				}

				/// Переходим к родителю
				currentNode = currentNode.Parent;
			}

			if(currentNode != null)
			{
				List<IToken> skippedBuffer;

				if (Nesting.Count != NestingLevel[currentNode])
				{
					/// Запоминаем токен, на котором произошла ошибка
					var errorToken = LexingStream.CurrentToken;
					/// Пропускаем токены, пока не поднимемся на тот же уровень вложенности,
					/// на котором раскрывали нетерминал
					GetNextToken(NestingLevel[currentNode], out skippedBuffer);
					skippedBuffer.Insert(0, errorToken);
				}
				else
				{
					skippedBuffer = new List<IToken>();
				}

				var alternativeToApply = Table[currentNode.Symbol, Grammar.ANY_TOKEN_NAME][0];
				var anyNode = new Node(alternativeToApply[0].Symbol, alternativeToApply[0].Options.Clone());

				anyNode.Value = currentNode.GetValue();
				anyNode.Value.AddRange(skippedBuffer.Select(t => t.Text));

				if (currentNode.Anchor != null)
					anyNode.SetAnchor(currentNode.Anchor.Start, currentNode.Anchor.End);
				if (skippedBuffer.Count > 0)
				{
					anyNode.SetAnchor(
						anyNode.Anchor?.Start ?? skippedBuffer.First().Location.Start,
						skippedBuffer.Last().Location.End
					);
				}

				/// Пытаемся пропустить Any в этом месте
				var token = SkipAny(anyNode, false);

				/// Если Any успешно пропустили и возобновили разбор,
				/// возвращаем токен, с которого разбор продолжается
				if (token.Name != Grammar.ERROR_TOKEN_NAME)
				{
					currentNode.ResetChildren();
					currentNode.AddFirstChild(anyNode);

					if (!String.IsNullOrEmpty(alternativeToApply.Alias))
						currentNode.Alias = alternativeToApply.Alias;

					Log.Add(Message.Warning(
						$"Произведено восстановление на уровне {GrammarObject.Userify(currentNode.Symbol)}, разбор продолжен с токена {GetTokenInfoForMessage(token)}",
						token.Location.Start
					));

					Statistics.RecoveryTimes += 1;

					return token;
				}
			}

			Log.Add(Message.Error(
				$"Не удалось продолжить разбор",
				null
			));

			return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
		}
	}
}
