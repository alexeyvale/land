using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;
using Land.Core.Parsing.Tree;

namespace Land.Core.Parsing.LR
{
	public class Parser: BaseParser
	{
		private TableLR1 Table { get; set; }

		private ParsingStack Stack { get; set; }
		private TokenStream LexingStream { get; set; }

		public Parser(Grammar g, ILexer lexer, BaseNodeGenerator nodeGen = null) : base(g, lexer, nodeGen)
		{
			Table = new TableLR1(g);
		}

		protected override Node ParsingAlgorithm(string text, bool enableTracing)
		{
			Node root = null;	

			/// Готовим лексер
			LexingStream = new TokenStream(Lexer, text);
			/// Читаем первую лексему из входного потока
			var token = LexingStream.GetNextToken();
			/// Создаём стек
			Stack = new ParsingStack(LexingStream);
			Stack.Push(0);

			while (true)
			{
				var currentState = Stack.PeekState();

				if(enableTracing && token.Name != Grammar.ERROR_TOKEN_NAME && token.Name != Grammar.ANY_TOKEN_NAME)
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
						token = SkipAny(NodeGenerator.Generate(Grammar.ANY_TOKEN_NAME));

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
						tokenNode.SetAnchor(token.Location.Start, token.Location.End);

						var shift = (ShiftAction)action;
						/// Вносим в стек новое состояние
						Stack.Push(tokenNode, shift.TargetItemIndex);

						if (enableTracing)
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
						}
						currentState = Stack.PeekState();

						/// Кладём на стек состояние, в которое нужно произвести переход
						Stack.Push(
							parentNode,
							Table.Transitions[currentState][reduce.ReductionAlternative.NonterminalSymbolName]
						);

						if (enableTracing)
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
					var errorToken = LexingStream.CurrentToken;
					var message = Message.Error(
						$"Неожиданный символ {this.GetTokenInfoForMessage(errorToken)} для состояния{Environment.NewLine}\t\t" + Table.ToString(Stack.PeekState(), null, "\t\t"),
						token.Location.Start
					);

					token = ErrorRecovery();

					if (token == null)
					{
						Log.Add(message);
						break;
					}
					else
					{
						message.Type = MessageType.Warning;
						Log.Add(message);
					}
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
						if (enableTracing)
						{
							Log.Add(Message.Trace(
								$"Попытка подобрать токены как Any для состояния {Environment.NewLine}\t\t" + Table.ToString(Stack.PeekState(), null, "\t\t"),
								token.Location.Start
							));
						}

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
			var token = LexingStream.CurrentToken;
			var rawActions = Table[Stack.PeekState(), Grammar.ANY_TOKEN_NAME].ToList();

			/// Пока по Any нужно производить свёртки (ячейка таблицы непуста и нет конфликтов)
			while (rawActions.Count == 1 && rawActions[0] is ReduceAction)
			{
				var reduce = (ReduceAction)rawActions[0];
				var parentNode = NodeGenerator.Generate(reduce.ReductionAlternative.NonterminalSymbolName);

				/// Снимаем со стека символы ветки, по которой нужно произвести свёртку
				for (var i = 0; i < reduce.ReductionAlternative.Count; ++i)
				{
					parentNode.AddFirstChild(Stack.PeekSymbol());
					Stack.Pop();
				}
				var currentState = Stack.PeekState();

				/// Кладём на стек состояние, в которое нужно произвести переход
				Stack.Push(
					parentNode,
					Table.Transitions[currentState][reduce.ReductionAlternative.NonterminalSymbolName]
				);

				rawActions = Table[Stack.PeekState(), Grammar.ANY_TOKEN_NAME].ToList();
			}

			/// Производим перенос
			var shift = (ShiftAction)rawActions.Where(a => a is ShiftAction).Single();
			/// Вносим в стек новое состояние
			Stack.Push(anyNode, shift.TargetItemIndex);

			PointLocation startLocation = anyNode.Anchor != null
				? anyNode.Anchor.Start 
				: token.Location.Start;
			PointLocation endLocation = anyNode.Anchor != null
				? anyNode.Anchor.End
				: null;

			/// Пропускаем токены, пока не найдём тот, для которого
			/// в текущем состоянии нужно выполнить перенос или свёртку
			while (Table[Stack.PeekState(), token.Name].Count == 0
				&& token.Name != Grammar.EOF_TOKEN_NAME)
			{
				endLocation = token.Location.End;
				token = LexingStream.GetNextToken();
			}

			if(endLocation != null)
				anyNode.SetAnchor(startLocation, endLocation);

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

		private IToken ErrorRecovery()
		{
			PointLocation startLocation = null;
			PointLocation endLocation = null;

			/// Снимаем со стека состояния до тех пор, пока не находим состояние,
			/// в котором есть пункт A -> * Any
			while (Stack.CountStates > 0 && Table.Items[Stack.PeekState()].FirstOrDefault(m => m.Alternative.Count == 1
				&& m.Alternative[0] == Grammar.ANY_TOKEN_NAME && m.Position == 0) == null)
			{
				if (Stack.CountSymbols > 0 && Stack.PeekSymbol().Anchor != null)
				{
					startLocation = Stack.PeekSymbol().Anchor.Start;
					if(endLocation == null)
					{
						endLocation = Stack.PeekSymbol().Anchor.End;
					}
				}

				Stack.Pop();
			}

			if (Stack.CountStates > 0)
			{
				/// Пытаемся пропустить Any в этом месте,
				/// Any захватывает участок с начала последнего 
				/// снятого со стека символа до места восстановления
				var anyNode = NodeGenerator.Generate(Grammar.ANY_TOKEN_NAME);
				if(startLocation != null)
					anyNode.SetAnchor(startLocation, startLocation);

				var token = SkipAny(anyNode);

				/// Если Any успешно пропустили и возобновили разбор,
				/// возвращаем токен, с которого разбор продолжается
				return token.Name != Grammar.ERROR_TOKEN_NAME ? token : null;
			}
			else
			{
				return null;
			}
		}
	}
}
