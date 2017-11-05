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
					Log.Add($"Текущий токен: {token.Name}; символ на вершине стека: {Stack.PeekSymbol()}");
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
					errorMessage = String.Format($"Неожиданный символ {token.Name}");
					return root;
				}
			}

			return root;
		}


		private IToken SkipText()
		{	
			return null;
		}
	}
}
