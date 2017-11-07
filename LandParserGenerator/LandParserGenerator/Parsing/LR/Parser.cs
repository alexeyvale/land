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

		public override Node Parse(string text, out string errorMessage)
		{
			Log = new List<string>();
			errorMessage = String.Empty;
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
					Log.Add($"Текущий токен: {token.Name}; символ на вершине стека: {Stack.PeekSymbol().Symbol}");
				else
					Log.Add($"Текущий токен: {token.Name}");

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

						Log.Add($"Перенос токена {token.Name}");

                        token = LexingStream.NextToken();
						continue;
					}

					/// Если нужно произвести перенос
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

						Log.Add($"Свёртка по правилу {action.ReductionAlternative}");

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
					if (grammar.SkipTokens.Contains(token.Name))
					{
						token = LexingStream.NextToken();
						continue;
					}

					/// Если в текущем состоянии есть переход по TEXT
					if (Table[currentState, Grammar.TEXT_TOKEN_NAME].Count == 1)
					{
						token = SkipText();
						continue;
					}

					token = ErrorRecovery();

					if (token == null)
					{
						errorMessage = String.Format($"Неожиданный символ {token.Name}");
						return root;
					}
					else
						continue;
				}
			}

			return root;
		}


		private IToken SkipText()
		{
			var token = LexingStream.CurrentToken();
			var textNode = new Node(Grammar.TEXT_TOKEN_NAME);
			var rawAction = Table[Stack.PeekState(), Grammar.TEXT_TOKEN_NAME].Single();

			/// Пока по TEXT нужно производить свёртки
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
				Stack.Push(textNode, action.TargetItemIndex);
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

			textNode.SetAnchor(startOffset, endOffset);

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
				/// и действия для TEXT,
				/// 
				if (currentTokenActions.Count == 1 && textTokenActions.Count == 1)
				{
					return SkipText();
				}
			}

			return null;
		}
	}
}
