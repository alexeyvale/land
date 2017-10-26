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
		private Stack<string> Stack { get; set; }
		private ILexer Lexer { get; set; }

		public Parser(Grammar g, ILexer lexer)
		{
			grammar = g;
			Table = new TableLL1(g);
			Lexer = lexer;
		}

		/// <summary>
		/// LL(1) разбор
		/// </summary>
		public bool Parse(string filename, out string errorMessage)
		{
			errorMessage = String.Empty;

			/// Готовим лексер
			Lexer.SetSource(filename);

			/// Кладём на стек стартовый символ
			Stack = new Stack<string>();
			Stack.Push(grammar.StartSymbol);

			/// Читаем первую лексему из входного потока
			var token = Lexer.NextToken();

			/// Пока не прошли полностью правило для стартового символа
			while (Stack.Count > 0)
			{
				var stackTop = Stack.Peek();

				/// Если на вершине стека терминал, сопоставляем его с текущей лексемой
				if (grammar[stackTop] is TerminalSymbol)
				{
					/// Если в текущем месте возможен пропуск текста
					if(stackTop == "TEXT")
					{
						/// Снимаем со стека символ TEXT
						Stack.Pop();
						/// Пропускаем текст и переходим к новой итерации
						token = SkipText();
						continue;
					}
					if (stackTop == token.Name)
					{
						Stack.Pop();
					}
					else
					{
						errorMessage = String.Format(
							$"Неожиданный символ {token}, ожидалось {stackTop}");
						return false;
					}
				}
				/// Если на вершине стека нетерминал, выбираем альтернативу по таблице
				else if(grammar[stackTop] is NonterminalSymbol)
				{
					var alternatives = Table[stackTop, token.Name];

					/// Сообщаем об ошибке в случае неоднозначной грамматики
					if(alternatives.Count > 1)
					{
						errorMessage = String.Format(
							$"Неоднозначная грамматика: для нетерминала {stackTop} и входного символа {token} допустимо несколько альтернатив");
						return false;
					}

					/// В случае, если нет альтернативы, начинающейся с текущей лексемы
					if (alternatives.Count == 0)
					{
						/// Если в правиле есть пустая ветка
						foreach(var alt in grammar.Rules[stackTop])
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
							Stack.Push(alternatives[0][i]);
						}

						continue;
					}
				}

				token = Lexer.NextToken();
			}

			return true;
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
				alt.Add(elem);

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
