using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;

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
						stackTop.SetAnchor(token.Line, token.Column);
						Stack.Pop();

						/// Пропускаем текст и переходим к новой итерации
						token = SkipText();
						continue;
					}
					if (stackTop.Symbol == token.Name)
					{
						stackTop.SetAnchor(token.Line, token.Column);
						Stack.Pop();
					}
					else
					{
						errorMessage = String.Format(
							$"Неожиданный символ {token}, ожидалось {stackTop}");
						return root;
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
							$"Неоднозначная грамматика: для нетерминала {stackTop} и входного символа {token} допустимо несколько альтернатив");
						return root;
					}

					/// В случае, если нет альтернативы, начинающейся с текущей лексемы
					if (alternatives.Count == 0)
					{
						/// Если в правиле есть пустая ветка
						foreach(var alt in grammar.Rules[stackTop.Symbol])
						{
							if(alt.Count == 0)
							{
								/// Выталкиваем нетерминал со стека без прочтения следующей лексемы
								Stack.Pop();
								continue;
							}
						}
					}
					/// снимаем со стека нетерминал и кладём содержимое его альтернативы
					else
					{
						Stack.Pop();

						for (var i = alternatives[0].Count - 1; i >= 0; --i)
						{
							var newNode = new Node(alternatives[0][i]);

							stackTop.AddChildFirst(newNode);
							Stack.Push(newNode);
						}

						continue;
					}
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
