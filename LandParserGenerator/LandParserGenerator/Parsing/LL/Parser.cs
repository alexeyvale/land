using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing.LL
{
	public class Parser: BaseParser
	{
		private const int MAX_RECOVERY_ATTEMPTS = 5;

		private TableLL1 Table { get; set; }
		private Stack<Node> Stack { get; set; }
		private TokenStream LexingStream { get; set; }

		public Parser(Grammar g, ILexer lexer): base(g, lexer)
		{
			Table = new TableLL1(g);

            /// В ходе парсинга потребуется First,
            /// учитывающее возможную пустоту ANY
            g.UseModifiedFirst = true;
		}

		/// <summary>
		/// LL(1) разбор
		/// </summary>
		/// <returns>
		/// Корень дерева разбора
		/// </returns>
		public override Node Parse(string text)
		{
			Log = new List<Message>();
			Statistics = new Statistics();

			var parsingStarted = DateTime.Now; 

            /// Готовим лексер и стеки
            LexingStream = new TokenStream(Lexer, text);
			Stack = new Stack<Node>();

			/// Кладём на стек стартовый символ
			var root = new Node(grammar.StartSymbol);
			Stack.Push(new Node(Grammar.EOF_TOKEN_NAME));
			Stack.Push(root);

			/// Читаем первую лексему из входного потока
			var token = LexingStream.NextToken();

			/// Пока не прошли полностью правило для стартового символа
			while (Stack.Count > 0)
			{
				var stackTop = Stack.Peek();

				Log.Add(Message.Trace(
					$"Текущий токен: {GetTokenInfoForMessage(token)} | Символ на вершине стека: {grammar.Userify(stackTop.Symbol)}",
					LexingStream.CurrentToken().Line, 
					LexingStream.CurrentToken().Column
				));

                /// Если символ на вершине стека совпадает с текущим токеном
                if(stackTop.Symbol == token.Name)
                {
					/// Снимаем узел со стека и устанавливаем координаты в координаты токена
					var node = Stack.Pop();

					/// Если текущий токен - признак пропуска символов, запускаем алгоритм
					if (token.Name == Grammar.ANY_TOKEN_NAME)
					{
						token = SkipAny(node);
						/// Если при пропуске текста произошла ошибка, прерываем разбор
						if (token.Name == Grammar.ERROR_TOKEN_NAME)
						{
							break;
						}
					}
					/// иначе читаем следующий токен
					else
					{
						node.SetAnchor(token.StartOffset, token.EndOffset);
						node.SetValue(token.Text);

						token = LexingStream.NextToken();
					}

					continue;
				}

				/// Если на вершине стека нетерминал, выбираем альтернативу по таблице
				if (grammar[stackTop.Symbol] is NonterminalSymbol)
				{
					var alternatives = Table[stackTop.Symbol, token.Name];
					Alternative alternativeToApply = null;

					/// Сообщаем об ошибке в случае неоднозначной грамматики
					if (alternatives.Count > 1)
					{
						Log.Add(Message.Error(
							$"Неоднозначная грамматика: для нетерминала {grammar.Userify(stackTop.Symbol)} и входного символа {grammar.Userify(token.Name)} допустимо несколько альтернатив",
							token.Line,
							token.Column
						));
						break;
					}
					/// Если же в ячейке ровно одна альтернатива
					else if (alternatives.Count == 1)
					{
						alternativeToApply = alternatives.Single();

						Stack.Pop();

						for (var i = alternativeToApply.Count - 1; i >= 0; --i)
						{
							var newNode = new Node(alternativeToApply[i].Symbol, alternativeToApply[i].Options);

							stackTop.AddFirstChild(newNode);
							Stack.Push(newNode);
						}

						continue;
					}
				}

				/// Если не смогли ни сопоставить текущий токен с терминалом на вершине стека,
				/// ни найти ветку правила для нетерминала на вершине стека
				if (token.Name == Grammar.ANY_TOKEN_NAME)
				{
					token = LexingStream.CurrentToken();

					Log.Add(Message.Error(
						grammar.Tokens.ContainsKey(stackTop.Symbol) ?
							$"Неожиданный символ {GetTokenInfoForMessage(token)}, ожидался символ {grammar.Userify(stackTop.Symbol)}" :
							$"Неожиданный символ {GetTokenInfoForMessage(token)}, ожидался один из следующих символов: {String.Join(", ", Table[stackTop.Symbol].Where(t => t.Value.Count > 0).Select(t => grammar.Userify(t.Key)))}",
						token.Line,
						token.Column
					));

					break;
				}
				/// Если непонятно, что делать с текущим токеном, и он конкретный
				/// (не Any), заменяем его на Any
				else
				{
					/// Если встретился неожиданный токен, но он в списке пропускаемых
					if (grammar.Options.IsSet(ParsingOption.SKIP, token.Name))
					{
						token = LexingStream.NextToken();
					}
					else
					{
						token = Lexer.CreateToken(Grammar.ANY_TOKEN_NAME);
					}
				}
			}

			TreePostProcessing(root);

			Statistics.TimeSpent = DateTime.Now - parsingStarted;

			return root;
		}

		/// <summary>
		/// Пропуск токенов в позиции, задаваемой символом Any
		/// </summary>
		/// <returns>
		/// Токен, найденный сразу после символа Any
		/// </returns>
		private IToken SkipAny(Node anyNode)
		{
			IToken token = LexingStream.CurrentToken();
			HashSet<string> tokensAfterText;

			/// Если с Any не связана последовательность стоп-символов
			if (anyNode.Options.AnySyncTokens.Count == 0)
			{
				/// Создаём последовательность символов, идущих в стеке после Any
				var alt = new Alternative();
				foreach (var elem in Stack)
					alt.Add(elem.Symbol);

				/// Определяем множество токенов, которые могут идти после Any
				tokensAfterText = grammar.First(alt);
				/// Само Any во входном потоке нам и так не встретится, а вывод сообщения об ошибке будет красивее
				tokensAfterText.Remove(Grammar.ANY_TOKEN_NAME);
			}
			else
			{
				tokensAfterText = anyNode.Options.AnySyncTokens;
			}

			/// Если Any непустой (текущий токен - это не токен,
			/// который может идти после Any)
			if (!tokensAfterText.Contains(token.Name))
			{
				/// Проверка на случай, если допропускаем текст в процессе восстановления
				if (!anyNode.StartOffset.HasValue)
					anyNode.SetAnchor(token.StartOffset, token.EndOffset);

				/// Смещение для участка, подобранного как текст
				int endOffset = token.EndOffset;

				while (!tokensAfterText.Contains(token.Name)
					&& !anyNode.Options.AnyErrorTokens.Contains(token.Name)
					&& token.Name != Grammar.EOF_TOKEN_NAME)
				{
					anyNode.Value.Add(token.Text);
					endOffset = token.EndOffset;

					token = LexingStream.NextToken();
				}

				anyNode.SetAnchor(anyNode.StartOffset.Value, endOffset);

				/// Если дошли до конца входной строки, и это было не по плану
				if (token.Name == Grammar.EOF_TOKEN_NAME && !tokensAfterText.Contains(token.Name)
					|| anyNode.Options.AnyErrorTokens.Contains(token.Name))
				{
					Log.Add(Message.Error(
						$"Ошибка при пропуске {Grammar.ANY_TOKEN_NAME}: неожиданный токен {grammar.Userify(token.Name)}, ожидался один из следующих символов: { String.Join(", ", tokensAfterText.Select(t => grammar.Userify(t))) }",
						null
					));

					return Lexer.CreateToken(Grammar.ERROR_TOKEN_NAME);
				}
			}

			return token;
		}
	}
}
