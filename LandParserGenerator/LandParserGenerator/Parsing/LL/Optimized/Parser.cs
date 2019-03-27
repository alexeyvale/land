using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;
using Land.Core.Parsing.Tree;
using Land.Core.Parsing;

namespace Land.Core.Parsing.LL.Optimized
{
	public class Parser: BaseParser
	{
		private TableLL1 Table { get; set; }

		private Stack<Node> Stack { get; set; }
		private string StackString => String.Join(" ", Stack.Select(s => GrammarObject.Userify(s.Symbol)));

		/// <summary>
		/// Уровень вложенности относительно описанных в грамматике пар,
		/// на котором начался разбор нетерминала
		/// </summary>
		private Dictionary<Node, int> NestingLevel { get; set; }

		private Dictionary<int, Tuple<LocalOptions, Stack<int>>> RecoveryCache { get; set; }
		private HashSet<int> PositionsWhereRecoveryStarted { get; set; }

		public Parser(Grammar g, ILexer lexer, BaseNodeGenerator nodeGen = null) : base(g, lexer, nodeGen)
		{
			Table = new TableLL1(g);

			RecoveryCache = new Dictionary<int, Tuple<LocalOptions, Stack<int>>>();

			/// Для каждого из возможных символов для восстановления кешируем дополнительную информацию
			foreach (var idx in GrammarObject.Options.GetSymbols(ParsingOption.RECOVERY).Select(s=>GrammarObject.SymbolToIndex[s]))
			{
				var stack = new Stack<int>();
				stack.Push(idx);
				LocalOptions anyOptions = null;

				/// Накапливаем символы, которые придётся положить на стек при восстановлении на smb
				while (true)
				{
					var alternative = Table[stack.Peek(), Grammar.ANY_TOKEN_INDEX][0];
					stack.Pop();

					for (var i = alternative.Count - 1; i >= 0; --i)
						stack.Push(alternative.Elements[i].Index);

					if(alternative[0].Index == Grammar.ANY_TOKEN_INDEX)
					{
						anyOptions = alternative[0].Options;
						stack.Pop();
						break;
					}
				}

				/// В кеш помещаем цепочку символов и опции Any
				RecoveryCache[idx] = new Tuple<LocalOptions, Stack<int>>(anyOptions, stack);
			}
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
			NestingLevel = new Dictionary<Node, int>();
			PositionsWhereRecoveryStarted = new HashSet<int>();

			/// Готовим лексер и стеки
			LexingStream = new ComplexTokenStream(GrammarObject, Lexer, text, Log);
			Stack = new Stack<Node>();

			/// Кладём на стек стартовый символ
			var root = NodeGenerator.Generate(GrammarObject.StartSymbol);
			root.Index = GrammarObject.StartSymbolIdx;

			Stack.Push(NodeGenerator.Generate(Grammar.EOF_TOKEN_NAME));
			Stack.Peek().Index = Grammar.EOF_TOKEN_INDEX;

			Stack.Push(root);

			/// Читаем первую лексему из входного потока
			var token = LexingStream.GetNextToken();

			/// Пока не прошли полностью правило для стартового символа
			while (Stack.Count > 0)
			{
				if (token.Index == Grammar.ERROR_TOKEN_INDEX)
					break;

				var stackTop = Stack.Peek();

				if (EnableTracing)
				{
					Log.Add(Message.Trace(
						$"Текущий токен: {this.GetTokenInfoForMessage(token)}\t |\t Стек: {StackString}",
						LexingStream.CurrentToken.Location.Start
					));
				}

				/// Если символ на вершине стека совпадает с текущим токеном
				if (stackTop.Index == token.Index)
                {
					if (token.Index == Grammar.ANY_TOKEN_INDEX)
					{
						var anyNode = NodeGenerator.Generate(Grammar.ANY_TOKEN_NAME);
						anyNode.Index = Grammar.ANY_TOKEN_INDEX;

						token = SkipAny(anyNode, true);
					}
					else
					{
						var node = Stack.Pop();
						node.SetLocation(token.Location.Start, token.Location.End);
						node.SetValue(token.Text);

						token = LexingStream.GetNextToken();
					}

					continue;
				}

				/// Если на вершине стека нетерминал, выбираем альтернативу по таблице
				if (GrammarObject.RulesIdx.ContainsKey(stackTop.Index))
				{
					var alternatives = Table[stackTop.Index, token.Index];

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
						if (token.Name == Grammar.ANY_TOKEN_NAME)
						{
							var anyNode = NodeGenerator.Generate(Grammar.ANY_TOKEN_NAME);
							anyNode.Index = Grammar.ANY_TOKEN_INDEX;

							token = SkipAny(anyNode, true);
						}
						else
						{
							ApplyAlternative(alternatives.Single());
						}

						continue;
					}
				}

				/// Если не смогли ни сопоставить текущий токен с терминалом на вершине стека,
				/// ни найти ветку правила для нетерминала на вершине стека
				if (token.Index == Grammar.ANY_TOKEN_INDEX)
				{
					Log.Add(Message.Warning(
						GrammarObject.Tokens.ContainsKey(stackTop.Symbol) ?
							$"Неожиданный символ {this.GetTokenInfoForMessage(LexingStream.CurrentToken)}, ожидался символ {GrammarObject.Userify(stackTop.Symbol)}" :
							$"Неожиданный символ {this.GetTokenInfoForMessage(LexingStream.CurrentToken)}, ожидался один из следующих символов: {String.Join(", ", Table[stackTop.Index].Where(t => t.Value.Count > 0).Select(t => GrammarObject.Userify(t.Key)))}",
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
						Log.Add(Message.Trace(
							$"Попытка трактовать текущий токен как начало участка, соответствующего Any",
							token.Location.Start
						));

						token = Lexer.CreateToken(Grammar.ANY_TOKEN_NAME);
						token.Index = Grammar.ANY_TOKEN_INDEX;
					}
				}
			}

			TreePostProcessing(root);

			if (LexingStream.CustomBlocks?.Count > 0)
			{
				var visitor = new InsertCustomBlocksVisitor(GrammarObject, LexingStream.CustomBlocks);
				root.Accept(visitor);
				root = visitor.Root;

				foreach (var block in visitor.CustomBlocks)
				{
					Log.Add(Message.Error(
						$"Блок \"{block.Start.Value[0]}\" прорезает несколько сущностей программы или находится в области, " +
							$"не учитываемой при синтаксическом анализе",
						block.Start.Location.Start
					));
				}
			}

			return root;
		}

		private HashSet<int> GetStopTokens(LocalOptions options, IEnumerable<int> followSequence)
		{
			/// Если с Any не связана последовательность стоп-символов
			if (!options.AnyOptionsIdx.ContainsKey(AnyOption.Except))
			{
				/// Определяем множество токенов, которые могут идти после Any
				var tokensAfterText = GrammarObject.First(followSequence.ToList());
				/// Само Any во входном потоке нам и так не встретится, а вывод сообщения об ошибке будет красивее
				tokensAfterText.Remove(Grammar.ANY_TOKEN_INDEX);

				return options.AnyOptions.ContainsKey(AnyOption.Include) 
					? new HashSet<int>(tokensAfterText.Cast<int>().Except(options.AnyOptionsIdx[AnyOption.Include]))
					: new HashSet<int>(tokensAfterText.Cast<int>());
			}
			else
			{
				return options.AnyOptionsIdx[AnyOption.Except];
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

			var stackTop = Stack.Peek();

			if (EnableTracing)
				Log.Add(Message.Trace(
					$"Инициирован пропуск Any\t |\t Стек: {StackString}",
					token.Location.Start
				));

			/// Пока по Any нужно раскрывать очередной нетерминал
			while (GrammarObject[stackTop.Symbol] is NonterminalSymbol)
			{
				var alt = Table[stackTop.Index, Grammar.ANY_TOKEN_INDEX].FirstOrDefault();

				if (alt != null)
				{
					ApplyAlternative(alt);
					stackTop = Stack.Peek();
				}
				else
				{
					var message = Message.Trace(
						$"Ошибка при пропуске {Grammar.ANY_TOKEN_NAME}: неожиданный токен {GrammarObject.Userify(token.Name)}",
						token.Location.Start
					);

					message.Type = enableRecovery ? MessageType.Warning : MessageType.Error;
					Log.Add(message);

					if (enableRecovery)
					{
						++Statistics.RecoveryTimesAny;
						return ErrorRecovery();
					}
					else
					{
						return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
					}
				}
			}

			/// В итоге первым терминалом, который окажется на стеке, должен быть Any
			if (stackTop.Index == Grammar.ANY_TOKEN_INDEX)
			{
				/// Подменяем свежесгенерированный узел для Any на переданный извне
				anyNode.Options = stackTop.Options.Clone();
				var anyIndex = stackTop.Parent.Children.IndexOf(stackTop);
				stackTop.Parent.Children.RemoveAt(anyIndex);
				stackTop.Parent.InsertChild(anyNode, anyIndex);
				Stack.Pop();
			}
			else
			{
				var message = Message.Trace(
					$"Ошибка при пропуске {Grammar.ANY_TOKEN_NAME}: неожиданный токен {GrammarObject.Userify(token.Name)}",
					token.Location.Start
				);

				message.Type = enableRecovery ? MessageType.Warning : MessageType.Error;
				Log.Add(message);

				if (enableRecovery)
				{
					++Statistics.RecoveryTimesAny;
					return ErrorRecovery();
				}
				else
				{
					return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
				}
			}

			if (EnableTracing)
				Log.Add(Message.Trace(
					$"Поиск окончания последовательности, соответствующей Any\t |\t Стек: {StackString}",
					token.Location.Start
				));

			var stopTokens = GetStopTokens(anyNode.Options, Stack.Select(n => n.Index));
			var ignorePairs = anyNode.Options.AnyOptions.ContainsKey(AnyOption.IgnorePairs);

			/// Если Any непустой (текущий токен - это не токен,
			/// который может идти после Any)
			if (!stopTokens.Contains(token.Index))
			{
				/// Проверка на случай, если допропускаем текст в процессе восстановления
				if (anyNode.Location == null)
					anyNode.SetLocation(token.Location.Start, token.Location.End);

				/// Смещение для участка, подобранного как текст
				var endLocation = token.Location.End;
				var anyLevel = LexingStream.GetPairsCount();

				while (!stopTokens.Contains(token.Index)
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

				anyNode.SetLocation(anyNode.Location.Start, endLocation);

				if (token.Name == Grammar.ERROR_TOKEN_NAME)
					return token;

				if (!stopTokens.Contains(token.Index))
				{
					var message = Message.Trace(
						$"Ошибка при пропуске {Grammar.ANY_TOKEN_NAME}: неожиданный токен {GrammarObject.Userify(token.Name)}, ожидался один из следующих символов: { String.Join(", ", stopTokens.Select(t => GrammarObject.Userify(t))) }",
						token.Location.Start
					);

					message.Type = enableRecovery ? MessageType.Warning : MessageType.Error;
					Log.Add(message);

					if (enableRecovery)
					{
						++Statistics.RecoveryTimesAny;
						Statistics.LongestRollback =
							Math.Max(Statistics.LongestRollback, LexingStream.CurrentIndex - tokenIndex);

						LexingStream.MoveTo(tokenIndex, nestingCopy);
						anyNode.Reset();
						///	Возвращаем узел обратно на стек
						Stack.Push(anyNode);

						return ErrorRecovery(stopTokens, 
							token.Index != Grammar.EOF_TOKEN_INDEX ? token.Index : (int?)null);
					}
					else
					{
						return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
					}
				}
			}

			return token;
		}

		private void ApplyAlternative(Alternative alternativeToApply)
		{
			var stackTop = Stack.Pop();

			if (!String.IsNullOrEmpty(alternativeToApply.Alias))
				stackTop.Alias = alternativeToApply.Alias;

			NestingLevel[stackTop] = LexingStream.GetPairsCount();

			for (var i = alternativeToApply.Count - 1; i >= 0; --i)
			{
				var newNode = NodeGenerator.Generate(alternativeToApply[i].Symbol, alternativeToApply[i].Options.Clone());
				newNode.Index = alternativeToApply[i].Index;

				stackTop.AddFirstChild(newNode);
				Stack.Push(newNode);
			}
		}

		private IToken ErrorRecovery(HashSet<int> stopTokens = null, int? errorToken = null)
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

			/// Поднимаемся по уже построенной части дерева, пока не встретим 
			/// пригодный для восстановления нетерминал. Ищем дальше, если
			while (currentNode != null && (
				/// текущий узел - тот, непосредственно в котором произошла ошибка, или
				!GrammarObject.RulesIdx.ContainsKey(currentNode.Index) || currentNode.Children.Count == 0 ||
				/// текущий символ не входит в список тех, на которых можно восстановиться, или
				!GrammarObject.Options.IsSet(ParsingOption.RECOVERY, currentNode.Symbol) ||
				/// при разборе соответствующей сущности уже пошли по Any-ветке
				ParsedStartsWithAny(currentNode) ||
				/// ошибка произошла на таком же Any
				IsUnsafeAny(stopTokens, errorToken, currentNode))
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
					var currentToken = LexingStream.CurrentToken;
					/// Пропускаем токены, пока не поднимемся на тот же уровень вложенности,
					/// на котором раскрывали нетерминал
					LexingStream.GetNextToken(NestingLevel[currentNode], out skippedBuffer);
					skippedBuffer.Insert(0, currentToken);
				}
				else
				{
					skippedBuffer = new List<IToken>();
				}

				var anyNode = NodeGenerator.Generate(Grammar.ANY_TOKEN_NAME);
				anyNode.Index = Grammar.ANY_TOKEN_INDEX;

				anyNode.Value = currentNode.GetValue();
				anyNode.Value.AddRange(skippedBuffer.Select(t => t.Text));

				if (currentNode.Location != null)
					anyNode.SetLocation(currentNode.Location.Start, currentNode.Location.End);

				currentNode.ResetChildren();
				Stack.Push(currentNode);

				if (skippedBuffer.Count > 0)
				{
					anyNode.SetLocation(
						anyNode.Location?.Start ?? skippedBuffer.First().Location.Start,
						skippedBuffer.Last().Location.End
					);
				}

				Log.Add(Message.Warning(
					$"Найдено предполагаемое начало {Grammar.ANY_TOKEN_NAME}",
					anyNode.Location?.Start ?? LexingStream.CurrentToken.Location.Start
				));

				Log.Add(Message.Warning(
					$"Попытка продолжить разбор на нетерминале {GrammarObject.Userify(currentNode.Symbol)} в позиции токена {this.GetTokenInfoForMessage(LexingStream.CurrentToken)}",
					LexingStream.CurrentToken.Location.Start
				));

				/// Пытаемся пропустить Any в этом месте
				var token = SkipAny(anyNode, false);

				/// Если Any успешно пропустили и возобновили разбор,
				/// возвращаем токен, с которого разбор продолжается
				if (token.Name != Grammar.ERROR_TOKEN_NAME)
				{
					Log.Add(Message.Warning(
						$"Произведено восстановление на уровне {GrammarObject.Userify(currentNode.Symbol)}, разбор продолжен с токена {this.GetTokenInfoForMessage(token)}",
						token.Location.Start
					));

					Statistics.RecoveryTimes += 1;
					Statistics.RecoveryTimeSpent += DateTime.Now - recoveryStartTime;

					return token;
				}
			}

			Log.Add(Message.Error(
				$"Не удалось продолжить разбор",
				null
			));

			return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
		}

		private bool ParsedStartsWithAny(Node subtree)
		{
			while (subtree.Symbol != Grammar.ANY_TOKEN_NAME
				&& subtree.Children.Count > 0)
				subtree = subtree.Children[0];

			return subtree.Symbol == Grammar.ANY_TOKEN_NAME;
		}

		private bool IsUnsafeAny(HashSet<int> oldStopTokens, int? errorToken, Node currentNode)
		{
			/// Any-кандидат не безопасен, если ошибка произошла на Any,
			/// и 
			/// при восстановлении мы будем сопоставлять Any на том же уровне, на котором произошла ошибка,
			/// и
			/// множество стоп-токенов, имевшее место в случае ошибки, совпадает со множеством, 
			/// которое будет получено при восстановлении, или
			/// токен, на котором произошла ошибка, входит в Avoid того Any, на котром будем восстанавливаться
			return oldStopTokens != null && LexingStream.GetPairsCount() == NestingLevel[currentNode]
					&& GetStopTokens(RecoveryCache[currentNode.Index].Item1, RecoveryCache[currentNode.Index].Item2.Concat(Stack.Select(n => n.Index))).SetEquals(oldStopTokens)
					&& (errorToken == null || RecoveryCache[currentNode.Index].Item1.ContainsIdx(AnyOption.Avoid, errorToken.Value));
		}
	}
}
