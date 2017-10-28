using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing.LL
{
	public class Parser
	{
		private Grammar grammar { get; set; }
		private TableLL1 Table { get; set; }
		private Stack<Node> Stack { get; set; }
		private ILexer Lexer { get; set; }

		public List<string> Log { get; private set; }

		public Parser(Grammar g, ILexer lexer)
		{
			grammar = g;
			Table = new TableLL1(g);
			Lexer = lexer;
		}

		/// <summary>
		/// LL(1) разбор
		/// </summary>
		/// <returns>
		/// Корень дерева разбора
		/// </returns>
		public Node Parse(string text, out string errorMessage)
		{
			Log = new List<string>();
			errorMessage = String.Empty;

			/// Готовим лексер
			Lexer.SetSourceText(text);

			/// Кладём на стек стартовый символ
			Stack = new Stack<Node>();
			var root = new Node(grammar.StartSymbol);
			Stack.Push(root);

			/// Читаем первую лексему из входного потока
			var token = Lexer.NextToken();

			/// Пока не прошли полностью правило для стартового символа
			while (Stack.Count > 0)
			{
				var stackTop = Stack.Peek();

				Log.Add($"Текущий токен: {token.Name}; символ на вершине стека: {stackTop.Symbol}");

				/// Если на вершине стека терминал, сопоставляем его с текущей лексемой
				if (grammar[stackTop.Symbol] is TerminalSymbol)
				{
					/// Если в текущем месте возможен пропуск текста
					if(stackTop.Symbol == "TEXT")
					{
						/// Снимаем со стека символ TEXT
						stackTop.SetAnchor(token.StartOffset, token.EndOffset);
						Stack.Pop();

						/// Пропускаем текст и переходим к новой итерации
						token = SkipText();
						continue;
					}
					if (stackTop.Symbol == token.Name)
					{
						stackTop.SetAnchor(token.StartOffset, token.EndOffset);
						Stack.Pop();
					}
					else
					{
						/// Если встретился неожиданный токен, но он в списке пропускаемых
						if (grammar.SkipTokens.Contains(token.Name))
						{
							token = Lexer.NextToken();
							continue;
						}
						else
						{
							errorMessage = String.Format(
								$"Неожиданный символ {token.Name}, ожидалось {stackTop.Symbol}");
							return root;
						}
					}
				}
				/// Если на вершине стека нетерминал, выбираем альтернативу по таблице
				else if(grammar[stackTop.Symbol] is NonterminalSymbol)
				{
					var alternatives = Table[stackTop.Symbol, token.Name];

					/// Сообщаем об ошибке в случае неоднозначной грамматики
					if(alternatives.Count > 1)
					{
						errorMessage = String.Format(
							$"Неоднозначная грамматика: для нетерминала {stackTop.Symbol} и входного символа {token.Name} допустимо несколько альтернатив");
						return root;
					}

					/// Сообщаем об ошибке в случае, если непонятно, что делать
					if (alternatives.Count == 0)
					{
						/// Если встретился неожиданный токен, но он в списке пропускаемых
						if (grammar.SkipTokens.Contains(token.Name))
						{
							token = Lexer.NextToken();
							continue;
						}
						else
						{
							errorMessage = String.Format(
								$"Неожиданный символ {token.Name}");
							return root;
						}
					}

					/// снимаем со стека нетерминал и кладём содержимое его альтернативы
					Stack.Pop();

					for (var i = alternatives[0].Count - 1; i >= 0; --i)
					{
						var newNode = new Node(alternatives[0][i]);

						stackTop.AddChildFirst(newNode);
						Stack.Push(newNode);
					}

					continue;
				}

				token = Lexer.NextToken();
			}

			return root;
		}

		/// <summary>
		/// Пропуск токенов в позиции, задаваемой символом TEXT
		/// </summary>
		/// <returns>
		/// Токен, найденный сразу после символа TEXT
		/// </returns>
		private IToken SkipText()
		{
			/// Создаём последовательность символов, идущих в стеке после TEXT
			var alt = new Alternative();
			foreach (var elem in Stack)
				alt.Add(elem.Symbol);

			/// Определяем множество токенов, которые могут идти после TEXT
			var tokensAfterText = grammar.First(alt).Select(t=>t.Name);

			/// Пропускаем
			IToken token;
			do
			{
				token = Lexer.NextToken();
			}
			while (!tokensAfterText.Contains(token.Name));

			return token;
		}
	}
}
