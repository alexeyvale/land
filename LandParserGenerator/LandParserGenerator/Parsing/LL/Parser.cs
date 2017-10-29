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
		private ParsingStack Stack { get; set; }
		private TokenStream LexingStream { get; set; }

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
			LexingStream = new TokenStream(Lexer, text);

			/// Кладём на стек стартовый символ
			Stack = new ParsingStack(LexingStream);
			var root = new Node(grammar.StartSymbol);
			Stack.Push(root);

			/// Читаем первую лексему из входного потока
			var token = LexingStream.NextToken();

			/// Пока не прошли полностью правило для стартового символа
			while (Stack.Count > 0)
			{
				var stackTop = Stack.Peek();

				Log.Add($"Текущий токен: {token.Name}; символ на вершине стека: {stackTop.Symbol}");

				/// Если на вершине стека терминал, сопоставляем его с текущей лексемой
				if (grammar[stackTop.Symbol] is TerminalSymbol)
				{
					/// Если в текущем месте должен быть пропуск текста
					if(stackTop.Symbol == "TEXT")
					{
						/// Пропускаем и переходим к новой итерации
						token = SkipText();
						continue;
					}

					/// Если текущий токен совпадает с ожидаемым
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
							token = LexingStream.NextToken();
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
					Alternative alternativeToApply = null;

					/// Сообщаем об ошибке в случае неоднозначной грамматики
					if(alternatives.Count > 1)
					{
						errorMessage = String.Format(
							$"Неоднозначная грамматика: для нетерминала {stackTop.Symbol} и входного символа {token.Name} допустимо несколько альтернатив");
						return root;
					}

					/// Отдельно обрабатываем случай, когда нет записи в таблице
					if (alternatives.Count == 0)
					{
						/// Если встретился неожиданный токен, но он в списке пропускаемых
						if (grammar.SkipTokens.Contains(token.Name))
						{
							token = LexingStream.NextToken();
							continue;
						}
						else
						{
							/// Если неожиданный токен - это часть текста TEXT,
							/// в таблице для TEXT и текущего нетерминала должно быть правило
							if (Table[stackTop.Symbol, "TEXT"].Count == 1)
							{
								alternativeToApply = Table[stackTop.Symbol, "TEXT"][0];
							}
							/// Иначе запускаем восстановление от ошибок
							else
							{
								alternativeToApply = ErrorRecovery();

								if (alternativeToApply == null)
								{
									errorMessage = String.Format(
										$"Неожиданный символ {token.Name}");
									return root;
								}

								token = LexingStream.CurrentToken();
								stackTop = Stack.Peek();
							}
						}
					}
					else
					{
						alternativeToApply = alternatives[0];
					}

					Stack.InitBatch();

					/// снимаем со стека нетерминал и кладём содержимое его альтернативы
					Stack.Pop();

					for (var i = alternativeToApply.Count - 1; i >= 0; --i)
					{
						var newNode = new Node(alternativeToApply[i]);

						stackTop.AddFirstChild(newNode);
						Stack.Push(newNode);
					}

					Stack.FinBatch();

					continue;
				}

				token = LexingStream.NextToken();
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
			var textNode = Stack.Pop();

			/// Создаём последовательность символов, идущих в стеке после TEXT
			var alt = new Alternative();
			foreach (var elem in Stack)
				alt.Add(elem.Symbol);

			/// Определяем множество токенов, которые могут идти после TEXT
			var tokensAfterText = grammar.First(alt).Select(t=>t.Name);

			/// Смещения для участка, подобранного как текст
			var startOffset = LexingStream.CurrentToken().StartOffset;
			var endOffset = LexingStream.CurrentToken().EndOffset;

			/// Пропускаем
			IToken token = LexingStream.CurrentToken();

			while(!tokensAfterText.Contains(token.Name))
			{
				endOffset = token.EndOffset;
				token = LexingStream.NextToken();
			}

			textNode.SetAnchor(startOffset, endOffset);

			return token;
		}

		/// <summary>
		/// Восстановление в случае ошибки разбора - 
		/// если очередной токен не соответствует ожидаемому,
		/// или не нашли запись в таблице
		/// </summary>
		/// <returns>
		/// Альтернатива, по которой нужно пойти для некоторого из нетерминалов,
		/// которые ранее были в стеке. Указатель потока токенов смещается в позицию, 
		/// в которой он был перед переходом по неправильной ветке для этого нетерминала
		/// </returns>
		private Alternative ErrorRecovery()
		{
			while (Stack.Count > 0)
			{
				Stack.Undo();

				/// Если на вершине стека оказался нетерминал
				if (grammar[Stack.Peek().Symbol] is NonterminalSymbol)
				{
					/// Проверяем, есть ли у него ветка для TEXT или ERROR
					if (Table[Stack.Peek().Symbol, "TEXT"].Count == 1)
					{
						return Table[Stack.Peek().Symbol, "TEXT"][0];
					}

					if (Table[Stack.Peek().Symbol, "ERROR"].Count == 1)
					{
						return Table[Stack.Peek().Symbol, "ERROR"][0];
					}
				}
			}

			return null;
		}
	}
}
