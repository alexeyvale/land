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

		private Stack<int> StatesStack { get; set; }
		private Stack<string> SymbolsStack { get; set; }
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

			StatesStack = new Stack<int>();
			StatesStack.Push(0);

			SymbolsStack = new Stack<string>();

			/// Готовим лексер
			LexingStream = new TokenStream(Lexer, text);
			/// Читаем первую лексему из входного потока
			var token = LexingStream.NextToken();

			while (true)
			{
				var currentState = StatesStack.Peek();

				if (SymbolsStack.Count > 0)
					Log.Add($"Текущий токен: {token.Name}; символ на вершине стека: {SymbolsStack.Peek()}");
				else
					Log.Add($"Текущий токен: {token.Name}");

				if (Table[currentState, token.Name].Count == 1)
				{
					/// Если нужно произвести перенос
					if (Table[currentState, token.Name].Single() is ShiftAction)
					{
						var action = (ShiftAction)Table[currentState, token.Name].Single();
						/// Вносим в стек новое состояние
						StatesStack.Push(action.TargetItemIndex);
						SymbolsStack.Push(token.Name);

						Log.Add($"Перенос токена {token.Name}");

                        token = LexingStream.NextToken();
						continue;
					}

					/// Если нужно произвести перенос
					if (Table[currentState, token.Name].Single() is ReduceAction)
					{
						var action = (ReduceAction)Table[currentState, token.Name].Single();
						/// Снимаем со стека символы ветки, по которой нужно произвести свёртку
						for (var i = 0; i < action.ReductionAlternative.Count; ++i)
						{
							SymbolsStack.Pop();
							StatesStack.Pop();
						}
						currentState = StatesStack.Peek();

						/// Кладём на стек состояние, в которое нужно произвести переход
						StatesStack.Push(Table.Transitions[currentState]
							[action.ReductionAlternative.NonterminalSymbolName]);
						SymbolsStack.Push(action.ReductionAlternative.NonterminalSymbolName);

						Log.Add($"Свёртка по правилу {action.ReductionAlternative}");

						continue;
					}

					if (Table[currentState, token.Name].Single() is AcceptAction)
					{
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
