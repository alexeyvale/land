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
		private NodeGenerator Generator { get; set; }

		public Parser(Grammar g, ILexer lexer): base(g, lexer)
		{
			Table = new TableLR1(g);
		}

		public override Node Parse(string text)
		{
			Log = new List<Message>();

			Statistics = new Statistics();
			var parsingStarted = DateTime.Now;

			Node root = null;	

			/// Готовим лексер
			LexingStream = new TokenStream(Lexer, text);
			/// Читаем первую лексему из входного потока
			var token = LexingStream.NextToken();
			/// Создаём стек
			Stack = new ParsingStack(LexingStream);
			Stack.Push(0);

			Generator = new NodeGenerator();

			while (true)
			{
				var currentState = Stack.PeekState();

				if(token.Name != Grammar.ERROR_TOKEN_NAME && token.Name != Grammar.ANY_TOKEN_NAME)
					Log.Add(Message.Trace(
						$"Текущий токен: {GetTokenInfoForMessage(token)} | Стек: {Stack.ToString(GrammarObject)}",
						new Anchor(token.Line, token.Column, token.StartOffset)
					));

				/// Знаем, что предпринять, если действие однозначно
				/// или существует выбор между shift и reduce (тогда выбираем shift)
				if (Table[currentState, token.Name].Count == 1
					|| Table[currentState, token.Name].Count == 2 && Table[currentState, token.Name].Any(a => a is ShiftAction))
				{
					if (token.Name == Grammar.ANY_TOKEN_NAME)
					{
						token = SkipAny(Generator.CreateNode(Grammar.ANY_TOKEN_NAME));

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
						var tokenNode = Generator.CreateNode(token.Name);
						tokenNode.SetAnchor(token.StartOffset, token.EndOffset);

						var shift = (ShiftAction)action;
						/// Вносим в стек новое состояние
						Stack.Push(tokenNode, shift.TargetItemIndex);

						Log.Add(Message.Trace(
							$"Перенос",
							new Anchor(token.Line, token.Column, token.StartOffset)
						));

						token = LexingStream.NextToken();
					}
					/// Если нужно произвести свёртку
					else if (action is ReduceAction)
					{
						var reduce = (ReduceAction)action;
						var parentNode = Generator.CreateNode(reduce.ReductionAlternative.NonterminalSymbolName);

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

						Log.Add(Message.Trace(
							$"Свёртка по правилу {GrammarObject.Userify(reduce.ReductionAlternative)} -> {GrammarObject.Userify(reduce.ReductionAlternative.NonterminalSymbolName)}",
							new Anchor(token.Line, token.Column, token.StartOffset)
						));

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
						$"Неожиданный символ {GetTokenInfoForMessage(errorToken)} для состояния{Environment.NewLine}\t\t" + Table.ToString(Stack.PeekState(), null, "\t\t"),
						new Anchor(errorToken.Line, errorToken.Column, errorToken.StartOffset)
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
						token = LexingStream.NextToken();
					}
					else
					{
						Log.Add(Message.Trace(
							$"Попытка подобрать токены как Any для состояния {Environment.NewLine}\t\t" + Table.ToString(Stack.PeekState(), null, "\t\t"),
							new Anchor(token.Line, token.Column, token.StartOffset)
						));

						token = Lexer.CreateToken(Grammar.ANY_TOKEN_NAME);
					}
				}
			}

			if(root != null)
				TreePostProcessing(root);

			Statistics.TimeSpent = DateTime.Now - parsingStarted;

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
				var parentNode = Generator.CreateNode(reduce.ReductionAlternative.NonterminalSymbolName);

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

			int startOffset = anyNode.StartOffset.HasValue 
				? anyNode.StartOffset.Value 
				: token.StartOffset;
			int? endOffset = anyNode.EndOffset.HasValue
				? anyNode.EndOffset.Value
				: (int?)null;

			/// Пропускаем токены, пока не найдём тот, для которого
			/// в текущем состоянии нужно выполнить перенос или свёртку
			while (Table[Stack.PeekState(), token.Name].Count == 0
				&& token.Name != Grammar.EOF_TOKEN_NAME)
			{
				endOffset = token.EndOffset;
				token = LexingStream.NextToken();
			}

			if(endOffset.HasValue)
				anyNode.SetAnchor(startOffset, endOffset.Value);

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
			int? startOffset = null;
			int? endOffset = null;

			/// Снимаем со стека состояния до тех пор, пока не находим состояние,
			/// в котором есть пункт A -> * Any
			while (Stack.CountStates > 0 && Table.Items[Stack.PeekState()].FirstOrDefault(m => m.Alternative.Count == 1
				&& m.Alternative[0] == Grammar.ANY_TOKEN_NAME && m.Position == 0) == null)
			{
				if (Stack.CountSymbols > 0 && Stack.PeekSymbol().StartOffset.HasValue)
				{
					startOffset = Stack.PeekSymbol().StartOffset;
					if(!endOffset.HasValue)
					{
						endOffset = Stack.PeekSymbol().EndOffset;
					}
				}

				Stack.Pop();
			}

			if (Stack.CountStates > 0)
			{
				/// Пытаемся пропустить Any в этом месте,
				/// Any захватывает участок с начала последнего 
				/// снятого со стека символа до места восстановления
				var anyNode = Generator.CreateNode(Grammar.ANY_TOKEN_NAME);
				if(startOffset.HasValue)
					anyNode.SetAnchor(startOffset.Value, startOffset.Value);

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
