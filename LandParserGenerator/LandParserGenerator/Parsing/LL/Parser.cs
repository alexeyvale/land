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
		private PairAwareTokenStream LexingStream { get; set; }

		/// <summary>
		/// Уровень вложенности относительно описанных в грамматике пар,
		/// на котором начался разбор нетерминала
		/// </summary>
		private Dictionary<Node, int> NestingLevel { get; set; }

		private HashSet<int> PositionsWhereRecoveryStarted { get; set; }

		public Parser(Grammar g, ILexer lexer, BaseNodeGenerator nodeGen = null) : base(g, lexer, nodeGen)
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
		protected override Node ParsingAlgorithm(string text, bool enableTracing)
		{
			/// Контроль вложенностей пар
			NestingLevel = new Dictionary<Node, int>();
			PositionsWhereRecoveryStarted = new HashSet<int>();

			/// Готовим лексер и стеки
			LexingStream = new PairAwareTokenStream(GrammarObject, Lexer, text, Log);
			Stack = new Stack<Node>();
			/// Кладём на стек стартовый символ
			var root = NodeGenerator.Generate(GrammarObject.StartSymbol);
			Stack.Push(NodeGenerator.Generate(Grammar.EOF_TOKEN_NAME));
			Stack.Push(root);

			/// Читаем первую лексему из входного потока
			var token = LexingStream.GetNextToken();

			/// Пока не прошли полностью правило для стартового символа
			while (Stack.Count > 0)
			{
				if (token.Name == Grammar.ERROR_TOKEN_NAME)
					break;

				var stackTop = Stack.Peek();

				if (enableTracing)
				{
					Log.Add(Message.Trace(
						$"Текущий токен: {this.GetTokenInfoForMessage(token)} | Символ на вершине стека: {GrammarObject.Userify(stackTop.Symbol)}",
						LexingStream.CurrentToken.Location.Start
					));
				}

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

						token = LexingStream.GetNextToken();
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

						NestingLevel[stackTop] = LexingStream.GetPairsCount();

						for (var i = alternativeToApply.Count - 1; i >= 0; --i)
						{
							var newNode = NodeGenerator.Generate(alternativeToApply[i].Symbol, alternativeToApply[i].Options.Clone());

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
							$"Неожиданный символ {this.GetTokenInfoForMessage(LexingStream.CurrentToken)}, ожидался символ {GrammarObject.Userify(stackTop.Symbol)}" :
							$"Неожиданный символ {this.GetTokenInfoForMessage(LexingStream.CurrentToken)}, ожидался один из следующих символов: {String.Join(", ", Table[stackTop.Symbol].Where(t => t.Value.Count > 0).Select(t => GrammarObject.Userify(t.Key)))}",
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
						token = LexingStream.GetNextToken();
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

		private HashSet<string> GetStopTokens(LocalOptions options, IEnumerable<string> followSequence)
		{
			/// Если с Any не связана последовательность стоп-символов
			if (!options.AnyOptions.ContainsKey(AnyOption.Except))
			{
				/// Создаём последовательность символов, идущих в стеке после Any
				var alt = new Alternative();
				foreach (var elem in followSequence)
					alt.Add(elem);

				/// Определяем множество токенов, которые могут идти после Any
				var tokensAfterText = GrammarObject.First(alt);
				/// Само Any во входном потоке нам и так не встретится, а вывод сообщения об ошибке будет красивее
				tokensAfterText.Remove(Grammar.ANY_TOKEN_NAME);

				/// Если указаны токены, которые нужно однозначно включать в Any
				if (options.AnyOptions.ContainsKey(AnyOption.Include))
				{
					tokensAfterText.ExceptWith(options.AnyOptions[AnyOption.Include]);
				}

				return tokensAfterText;
			}
			else
			{
				return options.AnyOptions[AnyOption.Except];
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
			var nestingCopy = LexingStream.GetPairsState();
			var tokenIndex = LexingStream.CurrentIndex;
			var token = LexingStream.CurrentToken;
			var stopTokens = GetStopTokens(anyNode.Options, Stack.Select(n=>n.Symbol));
			var ignorePairs = anyNode.Options.AnyOptions.ContainsKey(AnyOption.IgnorePairs);

			/// Если Any непустой (текущий токен - это не токен,
			/// который может идти после Any)
			if (!stopTokens.Contains(token.Name))
			{
				/// Проверка на случай, если допропускаем текст в процессе восстановления
				if (anyNode.Location == null)
					anyNode.SetAnchor(token.Location.Start, token.Location.End);

				/// Смещение для участка, подобранного как текст
				var endLocation = token.Location.End;
				var anyLevel = LexingStream.GetPairsCount();

				while (!stopTokens.Contains(token.Name)
					&& (ignorePairs || LexingStream.CurrentTokenDirection != Direction.Up)
					&& !anyNode.Options.Contains(AnyOption.Avoid, token.Name)
					&& token.Name != Grammar.EOF_TOKEN_NAME
					&& token.Name != Grammar.ERROR_TOKEN_NAME)
				{
					anyNode.Value.Add(token.Text);
					endLocation = token.Location.End;

					if (ignorePairs)
					{
						token = LexingStream.GetNextToken();
					}
					else
					{
						token = LexingStream.GetNextToken(anyLevel, out List<IToken> skippedBuffer);

						/// Если при пропуске до токена на том же уровне
						/// пропустили токены с более глубокой вложенностью
						if (skippedBuffer.Count > 0)
						{
							anyNode.Value.AddRange(skippedBuffer.Select(t => t.Text));
							endLocation = skippedBuffer.Last().Location.End;
						}
					}
				}

				anyNode.SetAnchor(anyNode.Location.Start, endLocation);

				if (token.Name == Grammar.ERROR_TOKEN_NAME)
					return token;

				if (!stopTokens.Contains(token.Name))
				{
					var message = Message.Trace(
						$"Ошибка при пропуске {Grammar.ANY_TOKEN_NAME}: неожиданный токен {GrammarObject.Userify(token.Name)}, ожидался один из следующих символов: { String.Join(", ", stopTokens.Select(t => GrammarObject.Userify(t))) }",
						token.Location.Start
					);

					if (enableRecovery)
					{
						++Statistics.RecoveryTimesAny;
						Statistics.LongestRollback =
							Math.Max(Statistics.LongestRollback, LexingStream.CurrentIndex - tokenIndex);

						message.Type = MessageType.Warning;
						Log.Add(message);

						LexingStream.MoveTo(tokenIndex, nestingCopy);
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
			if (!GrammarObject.Options.IsSet(ParsingOption.RECOVERY))
			{
				Log.Add(Message.Error(
					$"Возобновление разбора в случае ошибки отключено",
					LexingStream.CurrentToken.Location.Start
				));

				return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
			}

			if (!PositionsWhereRecoveryStarted.Add(LexingStream.CurrentIndex))
			{
				Log.Add(Message.Error(
					$"Возобновление разбора невозможно: восстановление в позиции токена {this.GetTokenInfoForMessage(LexingStream.CurrentToken)} уже проводилось",
					LexingStream.CurrentToken.Location.Start
				));

				return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
			}

			Log.Add(Message.Warning(
				$"Процесс восстановления запущен в позиции токена {this.GetTokenInfoForMessage(LexingStream.CurrentToken)}",
				LexingStream.CurrentToken.Location.Start
			));

			var recoveryStartTime = DateTime.Now;

			/// То, что мы хотели разобрать, и не смогли
			var currentNode = Stack.Pop();
			var currentStopTokens = currentNode.Symbol == Grammar.ANY_TOKEN_NAME
				? GetStopTokens(currentNode.Options, Stack.Select(n => n.Symbol))
				: (HashSet<string>)null;

			/// Поднимаемся по уже построенной части дерева, пока не встретим пригодный для восстановления нетерминал
			while
				(
					currentNode != null &&
					(
						/// Отсекаем узел, при попытке разбора которого возникла ошибка
						!GrammarObject.Rules.ContainsKey(currentNode.Symbol) ||
						currentNode.Children.Count == 0 ||
						/// Не восстанавливаемся на символе, если для него и так пошли по Any-альтернативе
						currentNode.Children[0].Symbol == Grammar.ANY_TOKEN_NAME ||
						/// Для восстановления подходит символ, у которого есть начинающаяся с Any альтернатива,
						/// и для которого, в случае, если ошибка произошла при пропуске Any,
						/// множество стоп-символов не совпадает со множеством стоп-символов для Any, которое не смогли пропустить
						!GrammarObject.Rules[currentNode.Symbol].Alternatives.Any(a =>
							a.Elements.FirstOrDefault()?.Symbol == Grammar.ANY_TOKEN_NAME &&
							(
								currentStopTokens == null ||
								!GetStopTokens(a[0].Options, a.Elements.Select(e => e.Symbol).Concat(Stack.Select(s => s.Symbol))).SetEquals(currentStopTokens)
							)
						)
					)
				)
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

				if (LexingStream.GetPairsCount() != NestingLevel[currentNode])
				{
					/// Запоминаем токен, на котором произошла ошибка
					var errorToken = LexingStream.CurrentToken;
					/// Пропускаем токены, пока не поднимемся на тот же уровень вложенности,
					/// на котором раскрывали нетерминал
					LexingStream.GetNextToken(NestingLevel[currentNode], out skippedBuffer);
					skippedBuffer.Insert(0, errorToken);
				}
				else
				{
					skippedBuffer = new List<IToken>();
				}

				var alternativeToApply = Table[currentNode.Symbol, Grammar.ANY_TOKEN_NAME][0];
				var anyNode = NodeGenerator.Generate(alternativeToApply[0].Symbol, alternativeToApply[0].Options.Clone());
				var newChildren = new LinkedList<Node>();

				for (var i = alternativeToApply.Count - 1; i > 0; --i)
				{
					var newNode = 
						NodeGenerator.Generate(alternativeToApply[i].Symbol, alternativeToApply[i].Options.Clone());

					newChildren.AddFirst(newNode);
					Stack.Push(newNode);
				}

				newChildren.AddFirst(anyNode);

				anyNode.Value = currentNode.GetValue();
				anyNode.Value.AddRange(skippedBuffer.Select(t => t.Text));

				if (currentNode.Location != null)
					anyNode.SetAnchor(currentNode.Location.Start, currentNode.Location.End);
				if (skippedBuffer.Count > 0)
				{
					anyNode.SetAnchor(
						anyNode.Location?.Start ?? skippedBuffer.First().Location.Start,
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

					foreach(var child in newChildren)
						currentNode.AddLastChild(child);

					if (!String.IsNullOrEmpty(alternativeToApply.Alias))
						currentNode.Alias = alternativeToApply.Alias;

					Log.Add(Message.Warning(
						$"Произведено восстановление на уровне {GrammarObject.Userify(currentNode.Symbol)}, разбор продолжен с токена {this.GetTokenInfoForMessage(token)}",
						token.Location.Start
					));

					Statistics.RecoveryTimes += 1;
					Statistics.RecoveryTimeSpent += DateTime.Now - recoveryStartTime;

					return token;
				}
				else
				{
					for (var i = 0; i < alternativeToApply.Count - 1; ++i)
						Stack.Pop();
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
