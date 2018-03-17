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

		public Parser(Grammar g, ILexer lexer): base(g, lexer)
		{
			Table = new TableLR1(g);
		}

		public override Node Parse(string text)
		{
			Log = new List<Message>();
			Errors = new List<Message>();

			Node root = null;	

			/// Готовим лексер
			LexingStream = new TokenStream(Lexer, text);
			/// Создаём стек
			Stack = new ParsingStack(LexingStream);
			Stack.Push(0);
			/// Читаем первую лексему из входного потока
			var token = LexingStream.NextToken();

			while (true)
			{
				var currentState = Stack.PeekState();

				if (Stack.CountSymbols > 0)
					Log.Add(Message.Trace(
						$"Текущий токен: {token.Name}; символ на вершине стека: {Stack.PeekSymbol().Symbol}",
						token.Line,
						token.Column
					));
				else
					Log.Add(Message.Trace(
						$"Текущий токен: {token.Name}",
						token.Line,
						token.Column
					));

				if (Table[currentState, token.Name].Count == 1)
				{
					/// Если нужно произвести перенос
					if (Table[currentState, token.Name].Single() is ShiftAction)
					{
						var tokenNode = new Node(token.Name);
						tokenNode.SetAnchor(token.StartOffset, token.EndOffset);

						var action = (ShiftAction)Table[currentState, token.Name].Single();
						/// Вносим в стек новое состояние
						Stack.Push(tokenNode, action.TargetItemIndex);

						Log.Add(Message.Trace(
							$"Перенос токена {token.Name}",
							token.Line,
							token.Column
						));

                        token = LexingStream.NextToken();
						continue;
					}

					/// Если нужно произвести свёртку
					if (Table[currentState, token.Name].Single() is ReduceAction)
					{
						Stack.InitBatch();

						var action = (ReduceAction)Table[currentState, token.Name].Single();
						var parentNode = new Node(action.ReductionAlternative.NonterminalSymbolName);

						/// Снимаем со стека символы ветки, по которой нужно произвести свёртку
						for (var i = 0; i < action.ReductionAlternative.Count; ++i)
						{
							parentNode.AddFirstChild(Stack.PeekSymbol());
							Stack.Pop();
						}
						currentState = Stack.PeekState();

						/// Кладём на стек состояние, в которое нужно произвести переход
						Stack.Push(
							parentNode,
							Table.Transitions[currentState][action.ReductionAlternative.NonterminalSymbolName]
						);

						Stack.FinBatch();

						Log.Add(Message.Trace(
							$"Свёртка по правилу {action.ReductionAlternative}",
							token.Line,
							token.Column
						));

						continue;
					}

					if (Table[currentState, token.Name].Single() is AcceptAction)
					{
						root = Stack.PeekSymbol();
						break;
					}
				}
				else
				{
					/// Если встретился неожиданный токен, но он в списке пропускаемых
					if (grammar.Options.IsSet(ParsingOption.SKIP, token.Name))
					{
						token = LexingStream.NextToken();
						continue;
					}

					/// Если в текущем состоянии есть переход по Any
					if (Table[currentState, Grammar.TEXT_TOKEN_NAME].Count == 1)
					{
						token = SkipAny();
						continue;
					}

					token = ErrorRecovery();

					if (token == null)
					{
						Errors.Add(Message.Error(
							$"Неожиданный символ {token.Name}",
							token.Line,
							token.Column
						));

						return root;
					}
					else
						continue;
				}
			}

			TreePostProcessing(root);

			return root;
		}


		private IToken SkipAny()
		{
			var token = LexingStream.CurrentToken();
			var anyNode = new Node(Grammar.TEXT_TOKEN_NAME);
			var rawAction = Table[Stack.PeekState(), Grammar.TEXT_TOKEN_NAME].Single();

			/// Пока по Any нужно производить свёртки
			while(rawAction is ReduceAction)
			{
				Stack.InitBatch();

				var action = (ReduceAction)rawAction;
				var parentNode = new Node(action.ReductionAlternative.NonterminalSymbolName);

				/// Снимаем со стека символы ветки, по которой нужно произвести свёртку
				for (var i = 0; i < action.ReductionAlternative.Count; ++i)
				{
					parentNode.AddFirstChild(Stack.PeekSymbol());
					Stack.Pop();
				}
				var currentState = Stack.PeekState();

				/// Кладём на стек состояние, в которое нужно произвести переход
				Stack.Push(
					parentNode,
					Table.Transitions[currentState][action.ReductionAlternative.NonterminalSymbolName]
				);

				Stack.FinBatch();

				rawAction = Table[Stack.PeekState(), Grammar.TEXT_TOKEN_NAME].FirstOrDefault();
			}

			/// Если нужно произвести перенос
			if (rawAction is ShiftAction)
			{
				var action = (ShiftAction)rawAction;
				/// Вносим в стек новое состояние
				Stack.Push(anyNode, action.TargetItemIndex);
			}

			int startOffset = token.StartOffset;
			int endOffset = token.EndOffset;

			/// Пропускаем токены, пока не найдём тот, для которого
			/// в текущем состоянии нужно выполнить перенос или свёртку
			while (Table[Stack.PeekState(), token.Name].Count == 0)
			{
				endOffset = token.EndOffset;
				token = LexingStream.NextToken();
			}

			anyNode.SetAnchor(startOffset, endOffset);

			return token;
		}

		private IToken ErrorRecovery()
		{
			while (Stack.CountStates > 0)
			{
				Stack.Undo();

				var currentTokenActions = Table[Stack.PeekState(), LexingStream.CurrentToken().Name];
				var textTokenActions = Table[Stack.PeekState(), Grammar.TEXT_TOKEN_NAME];

				/// Если в текущем состоянии есть приоритетные действия 
				/// и действия для Any,
				/// 
				if (currentTokenActions.Count == 1 && textTokenActions.Count == 1)
				{
					return SkipAny();
				}
			}

			return null;
		}
	}
}
