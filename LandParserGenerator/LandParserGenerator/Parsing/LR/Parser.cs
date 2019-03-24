using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;
using Land.Core.Parsing.Tree;

namespace Land.Core.Parsing.LR
{
	public class Parser : BaseParser
	{
		private TableLR1 Table { get; set; }

		private ParsingStack Stack { get; set; }
		private Stack<int> NestingStack { get; set; }

		private HashSet<int> PositionsWhereRecoveryStarted { get; set; }

		public Parser(Grammar g, ILexer lexer, BaseNodeGenerator nodeGen = null) : base(g, lexer, nodeGen)
		{
			Table = new TableLR1(g);
		}

		protected override Node ParsingAlgorithm(string text)
		{
			Node root = null;

			/// Множество индексов токенов, на которых запускалось восстановление
			PositionsWhereRecoveryStarted = new HashSet<int>();
			/// Создаём стек для уровней вложенности пар
			NestingStack = new Stack<int>();
			/// Готовим лексер
			LexingStream = new ComplexTokenStream(GrammarObject, Lexer, text, Log);
			/// Читаем первую лексему из входного потока
			var token = LexingStream.GetNextToken();
			/// Создаём стек
			Stack = new ParsingStack();
			Stack.Push(0);
			NestingStack.Push(0);

			while (true)
			{
				if (token.Name == Grammar.ERROR_TOKEN_NAME)
					break;

				var currentState = Stack.PeekState();

				if(EnableTracing && token.Name != Grammar.ERROR_TOKEN_NAME && token.Name != Grammar.ANY_TOKEN_NAME)
					Log.Add(Message.Trace(
						$"Текущий токен: {this.GetTokenInfoForMessage(token)} | Стек: {Stack.ToString(GrammarObject)}",
						token.Location.Start
					));

				/// Знаем, что предпринять, если действие однозначно
				/// или существует выбор между shift и reduce (тогда выбираем shift)
				if (Table[currentState, token.Name].Count == 1
					|| Table[currentState, token.Name].Count == 2 && Table[currentState, token.Name].Any(a => a is ShiftAction))
				{
					if (token.Name == Grammar.ANY_TOKEN_NAME)
					{
						token = SkipAny(NodeGenerator.Generate(Grammar.ANY_TOKEN_NAME), true);

						/// Если при пропуске текста произошла ошибка, прерываем разбор
						if (token.Name == Grammar.ERROR_TOKEN_NAME)
							break;
						else
							continue;
					}

					var action = GetAction(currentState, token.Name);

					/// Если нужно произвести перенос
					if (action is ShiftAction)
					{
						var tokenNode = NodeGenerator.Generate(token.Name);
						tokenNode.SetValue(token.Text);
						tokenNode.SetLocation(token.Location.Start, token.Location.End);

						var shift = (ShiftAction)action;
						/// Вносим в стек новое состояние
						Stack.Push(tokenNode, shift.TargetItemIndex);
						NestingStack.Push(LexingStream.GetPairsCount());

						if (EnableTracing)
						{
							Log.Add(Message.Trace(
								$"Перенос",
								token.Location.Start
							));
						}

						token = LexingStream.GetNextToken();
					}
					/// Если нужно произвести свёртку
					else if (action is ReduceAction reduce)
					{
						var parentNode = NodeGenerator.Generate(reduce.ReductionAlternative.NonterminalSymbolName);

						/// Снимаем со стека символы ветки, по которой нужно произвести свёртку
						for (var i = 0; i < reduce.ReductionAlternative.Count; ++i)
						{
							parentNode.AddFirstChild(Stack.PeekSymbol());
							Stack.Pop();
							NestingStack.Pop();
						}
						currentState = Stack.PeekState();

						/// Кладём на стек состояние, в которое нужно произвести переход
						Stack.Push(
							parentNode,
							Table.Transitions[currentState][reduce.ReductionAlternative.NonterminalSymbolName]
						);
						NestingStack.Push(LexingStream.GetPairsCount());

						if (EnableTracing)
						{
							Log.Add(Message.Trace(
								$"Свёртка по правилу {GrammarObject.Userify(reduce.ReductionAlternative)} -> {GrammarObject.Userify(reduce.ReductionAlternative.NonterminalSymbolName)}",
								token.Location.Start
							));
						}

						continue;
					}
					else if (action is AcceptAction)
					{
						root = Stack.PeekSymbol();
						break;
					}
				}
				else if (token.Name == Grammar.ANY_TOKEN_NAME)
				{
					Log.Add(Message.Warning(
						$"Неожиданный символ {this.GetTokenInfoForMessage(LexingStream.CurrentToken)} для состояния{Environment.NewLine}\t\t" + Table.ToString(Stack.PeekState(), null, "\t\t"),
						LexingStream.CurrentToken.Location.Start
					));

					token = ErrorRecovery();
				}
				else
				{
					/// Если встретился неожиданный токен, но он в списке пропускаемых
					if (GrammarObject.Options.IsSet(ParsingOption.SKIP, token.Name))
					{
						token = LexingStream.GetNextToken();
					}
					else
					{
						if (EnableTracing)
						{
							Log.Add(Message.Trace(
								$"Попытка трактовать текущий токен как начало участка, соответствующего Any",
								token.Location.Start
							));
						}

						token = Lexer.CreateToken(Grammar.ANY_TOKEN_NAME);
					}
				}
			}

			if (root != null)
			{
				TreePostProcessing(root);

				if (LexingStream.CustomBlocks?.Count > 0)
				{
					var visitor = new InsertCustomBlocksVisitor(GrammarObject, LexingStream.CustomBlocks);
					root.Accept(visitor);
					root = visitor.Root;

					foreach(var block in visitor.CustomBlocks)
					{
						Log.Add(Message.Error(
							$"Блок \"{block.Start.Value[0]}\" прорезает несколько сущностей программы или находится в области, " +
								$"не учитываемой при синтаксическом анализе",
							block.Start.Location.Start
						));
					}
				}
			}

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

		private IToken SkipAny(Node anyNode, bool enableRecovery)
		{
			var nestingCopy = LexingStream.GetPairsState();
			var stackActions = new LinkedList<Tuple<Node, int?>>();
			var token = LexingStream.CurrentToken;
			var tokenIndex = LexingStream.CurrentIndex;
			var rawActions = Table[Stack.PeekState(), Grammar.ANY_TOKEN_NAME].ToList();

			if(EnableTracing)
				Log.Add(Message.Trace(
					$"Инициирован пропуск Any | Стек: {Stack.ToString(GrammarObject)} | Состояние: {Environment.NewLine}\t\t"
						+ Table.ToString(Stack.PeekState(), null, "\t\t"),
					token.Location.Start
				));

			/// Пока по Any нужно производить свёртки (ячейка таблицы непуста и нет конфликтов)
			while (rawActions.Count == 1 && rawActions[0] is ReduceAction)
			{
				var reduce = (ReduceAction)rawActions[0];
				var parentNode = NodeGenerator.Generate(reduce.ReductionAlternative.NonterminalSymbolName);

				/// Снимаем со стека символы ветки, по которой нужно произвести свёртку
				for (var i = 0; i < reduce.ReductionAlternative.Count; ++i)
				{
					stackActions.AddFirst(new Tuple<Node, int?>(
						Stack.PeekSymbol(),
						Stack.PeekState()
					));

					parentNode.AddFirstChild(Stack.PeekSymbol());
					Stack.Pop();
					NestingStack.Pop();
				}

				/// Кладём на стек состояние, в которое нужно произвести переход
				Stack.Push(
					parentNode,
					Table.Transitions[Stack.PeekState()][reduce.ReductionAlternative.NonterminalSymbolName]
				);
				NestingStack.Push(LexingStream.GetPairsCount());
				stackActions.AddFirst(new Tuple<Node, int?>(null, null));

				rawActions = Table[Stack.PeekState(), Grammar.ANY_TOKEN_NAME].ToList();
			}

			/// Берём опции из нужного вхождения Any
			var marker = Table.Items[Stack.PeekState()].First(i => i.Next == Grammar.ANY_TOKEN_NAME);
			anyNode.Options = marker.Alternative[marker.Position].Options;

			/// Производим перенос
			var shift = (ShiftAction)rawActions.Where(a => a is ShiftAction).Single();
			/// Вносим в стек новое состояние
			Stack.Push(anyNode, shift.TargetItemIndex);
			NestingStack.Push(LexingStream.GetPairsCount());
			stackActions.AddFirst(new Tuple<Node, int?>(null, null));

			if(EnableTracing)
				Log.Add(Message.Trace(
					$"Поиск окончания последовательности, соответствующей Any | Стек: {Stack.ToString(GrammarObject)} | Состояние: {Environment.NewLine}\t\t" 
						+ Table.ToString(Stack.PeekState(), null, "\t\t"),
					token.Location.Start
				));

			var stopTokens = GetStopTokens(anyNode.Options, Stack.PeekState());
			var ignorePairs = anyNode.Options.AnyOptions.ContainsKey(AnyOption.IgnorePairs);

			var startLocation = anyNode.Location?.Start 
				?? token.Location.Start;
			var endLocation = anyNode.Location?.End;
			var anyLevel = LexingStream.GetPairsCount();

			/// Пропускаем токены, пока не найдём тот, для которого
			/// в текущем состоянии нужно выполнить перенос или свёртку
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

					if (skippedBuffer.Count > 0)
					{
						anyNode.Value.AddRange(skippedBuffer.Select(t => t.Text));
						endLocation = skippedBuffer.Last().Location.End;
					}
				}
			}

			if(endLocation != null)
				anyNode.SetLocation(startLocation, endLocation);

			if (token.Name == Grammar.ERROR_TOKEN_NAME)
				return token;

			/// Если дошли до конца входной строки, и это было не по плану
			if (!stopTokens.Contains(token.Name))
			{
				if (enableRecovery)
				{
					var message = Message.Trace(
						$"Ошибка при пропуске {Grammar.ANY_TOKEN_NAME}: неожиданный токен {GrammarObject.Userify(token.Name)}, ожидался один из токенов {String.Join(", ", stopTokens.Select(t => GrammarObject.Userify(t)))}",
						token.Location.Start
					);

					if (GrammarObject.Options.IsSet(ParsingOption.RECOVERY))
					{
						++Statistics.RecoveryTimesAny;
						Statistics.LongestRollback = 
							Math.Max(Statistics.LongestRollback, LexingStream.CurrentIndex - tokenIndex);

						message.Type = MessageType.Warning;
						Log.Add(message);

						LexingStream.MoveTo(tokenIndex, nestingCopy);

						/// Приводим стек в состояние, 
						/// в котором он был до начала пропуска Any
						foreach (var action in stackActions)
						{
							if (action.Item1 == null)
							{
								Stack.Pop();
								NestingStack.Pop();
							}
							else
							{
								Stack.Push(action.Item1, action.Item2);
								NestingStack.Push(LexingStream.GetPairsCount());
							}
						}

						return ErrorRecovery();
					}
					else
					{
						message.Type = MessageType.Error;
						Log.Add(message);
						return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
					}
				}
				else
				{
					Log.Add(Message.Error(
						$"Ошибка при пропуске {Grammar.ANY_TOKEN_NAME} в процессе восстановления: неожиданный токен {GrammarObject.Userify(token.Name)}, ожидался один из токенов {String.Join(", ", stopTokens.Select(t => GrammarObject.Userify(t)))}",
						token.Location.Start
					));

					return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
				}
			}

			return token;
		}

		public HashSet<string> GetStopTokens(LocalOptions options, int state)
		{
			return options.AnyOptions.ContainsKey(AnyOption.Except)
				? options.AnyOptions[AnyOption.Except]
				: new HashSet<string>(
					Table.GetExpectedTokens(state).Except(options.AnyOptions.ContainsKey(AnyOption.Include) 
						? options.AnyOptions[AnyOption.Include] : new HashSet<string>())
				);
		}

		public class PathFragment
		{
			public Alternative Alt { get; set; }
			public int Pos { get; set; }

			public override bool Equals(object obj)
			{
				return obj is PathFragment pf 
					&& Alt.Equals(pf.Alt) && Pos == pf.Pos;
			}

			public override int GetHashCode()
			{
				return Alt.GetHashCode();
			}
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

			PointLocation startLocation = null;
			PointLocation endLocation = null;
			var value = new List<string>();

			var previouslyMatchedSymbol = String.Empty;
			var derivationProds = new HashSet<PathFragment>();
			var initialDerivationProds = new HashSet<PathFragment>();

			/// Снимаем со стека состояния до тех пор, пока не находим состояние,
			/// в котором есть пункт A -> * Any ...
			do
			{
				if (Stack.CountSymbols > 0)
				{
					if (Stack.PeekSymbol().Location != null)
					{
						startLocation = Stack.PeekSymbol().Location.Start;
						if (endLocation == null)
						{
							endLocation = Stack.PeekSymbol().Location.End;
						}
					}

					value = Stack.PeekSymbol().GetValue()
						.Concat(value).ToList();

					/// Запоминаем снятый со стека символ - это то, что было успешно распознано
					previouslyMatchedSymbol = Stack.PeekSymbol().Symbol;
				}

				Stack.Pop();
				NestingStack.Pop();

				if (Stack.CountStates > 0)
				{
					/// Выбираем пункты, продукции которых потенциально могут участвовать
					/// в выводе текущего префикса из стартового символа
					initialDerivationProds = new HashSet<PathFragment>(
						Table.Items[Stack.PeekState()]
							.Where
							(i =>
								/// Точка должна стоять перед символом, только что снятым со стека
								i.Next == previouslyMatchedSymbol &&
								/// Если это не первая выборка, на предыдущем шаге в выборке должен был быть пункт
								/// с той же альтернативой, но точкой на один символ дальше
								(derivationProds.Count == 0 || derivationProds.Any(p => p.Alt.Equals(i.Alternative) && p.Pos == i.Position + 1))
							)
							.Select(i => new PathFragment { Alt = i.Alternative, Pos = i.Position })
					);

					derivationProds = new HashSet<PathFragment>(initialDerivationProds);

					var oldCount = 0;

					while (oldCount != derivationProds.Count)
					{
						oldCount = derivationProds.Count;

						/// Добавляем к списку пункты, порождающие уже добавленные пункты
						derivationProds.UnionWith(Table.Items[Stack.PeekState()]
							.Where(i => derivationProds.Any(p => p.Pos == 0 && p.Alt.NonterminalSymbolName == i.Next))
							.Select(i => new PathFragment { Alt = i.Alternative, Pos = i.Position })
						);
					}
				}
			}
			/// Работаем, пока 1) последний снятый со стека символ - это Any,
			/// или 2) пока нет пересечения вероятного пути вывода и возможного пути восстановления,
			/// или 3) пока все продукции в изначальном множестве возможных продукций вывода базисные, и продукции в пересечении 
			/// содержат точку перед снятым со стека символом, то есть, мы уже успешно разобрали то, 
			/// на чём могли бы восстановиться, и значит, это не родитель места ошибки,
			while (Stack.CountStates > 0 && (previouslyMatchedSymbol == Grammar.ANY_TOKEN_NAME
				|| derivationProds.Count == initialDerivationProds.Count
				|| derivationProds.Except(initialDerivationProds).All(p => !GrammarObject.Options.IsSet(ParsingOption.RECOVERY, p.Alt[p.Pos])))
			);

			if (Stack.CountStates > 0)
			{
				if (LexingStream.GetPairsCount() != NestingStack.Peek())
				{
					var skippedBuffer = new List<IToken>();

					/// Запоминаем токен, на котором произошла ошибка
					var errorToken = LexingStream.CurrentToken;
					/// Пропускаем токены, пока не поднимемся на тот же уровень вложенности,
					/// на котором раскрывали нетерминал
					LexingStream.GetNextToken(NestingStack.Peek(), out skippedBuffer);
					skippedBuffer.Insert(0, errorToken);

					value.AddRange(skippedBuffer.Select(t=>t.Name));
				}

				/// Пытаемся пропустить Any в этом месте,
				/// Any захватывает участок с начала последнего 
				/// снятого со стека символа до места восстановления
				var anyNode = NodeGenerator.Generate(Grammar.ANY_TOKEN_NAME);
				if(startLocation != null)
					anyNode.SetLocation(startLocation, endLocation);
				anyNode.Value = value.ToList();

				Log.Add(Message.Warning(
					$"Найдено предполагаемое начало {Grammar.ANY_TOKEN_NAME}",
					anyNode.Location?.Start ?? LexingStream.CurrentToken.Location.Start
				));

				Log.Add(Message.Warning(
						$"Попытка продолжить разбор в состоянии {Environment.NewLine}\t\t{Table.ToString(Stack.PeekState(), null, "\t\t")}\tв позиции токена {this.GetTokenInfoForMessage(LexingStream.CurrentToken)}",
						LexingStream.CurrentToken.Location.Start
					));		

				var token = SkipAny(anyNode, false);

				/// Если Any успешно пропустили и возобновили разбор,
				/// возвращаем токен, с которого разбор продолжается

				if (token.Name != Grammar.ERROR_TOKEN_NAME)
				{
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
	}
}
