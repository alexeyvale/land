using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
	public class GrammarActionResponse
	{
		public bool Success { get; set; }
		public string ErrorMessage { get; set; }

		public static GrammarActionResponse GetSuccess()
		{
			return new GrammarActionResponse()
			{
				Success = true,
				ErrorMessage = String.Empty
			};
		}
	}

	public class Grammar
	{
		private static Grammar _instance = new Grammar();
		public static Grammar Instance { get { return _instance; } }

		public string StartSymbol { get; private set; }

		public Dictionary<string, Rule> Rules { get; private set; } = new Dictionary<string, Rule>();
		public Dictionary<string, Token> Tokens { get; private set; } = new Dictionary<string, Token>();

		public IGrammarElement this[string key]
		{
			get
			{
				if (Rules.ContainsKey(key))
				{
					return Rules[key];
				}

				if (Tokens.ContainsKey(key))
				{
					return Tokens[key];
				}

				return null;
			}
		}

		public const string EofTokenName = "EOF";
		public const string TextTokenName = "TEXT";

		public Grammar()
		{
			/// Заводим токены, определённые по умолчанию
			Tokens[EofTokenName] = new Token(EofTokenName, String.Empty);
			Tokens[Token.EmptyTokenName] = Token.Empty;
			Tokens[TextTokenName] = new Token(TextTokenName, String.Empty);
		}

		public GrammarActionResponse DeclareNonterminal(Rule rule)
		{
			if (Rules.ContainsKey(rule.Name))
			{
				return new GrammarActionResponse()
				{
					ErrorMessage = String.Format($"Повторное определение нетерминала {rule.Name}"),
					Success = false
				};
			}

			if (Tokens.ContainsKey(rule.Name))
			{
				return new GrammarActionResponse()
				{
					ErrorMessage = String.Format($"Символ {rule.Name} определён как терминальный"),
					Success = false
				};
			}
			
			Rules[rule.Name] = rule;
			return GrammarActionResponse.GetSuccess();
		}
		public GrammarActionResponse DeclareTerminal(Token token)
		{
			if (Tokens.ContainsKey(token.Name))
			{
				return new GrammarActionResponse()
				{
					ErrorMessage = String.Format($"Повторное определение терминала {token.Name}"),
					Success = false
				};
			}

			if (Rules.ContainsKey(token.Name))
			{
				return new GrammarActionResponse()
				{
					ErrorMessage = String.Format($"Символ {token.Name} определён как нетерминальный"),
					Success = false
				};
			}

			Tokens[token.Name] = token;
			return GrammarActionResponse.GetSuccess();
		}
		public GrammarActionResponse SetStartSymbol(string symbol)
		{
			if (!this.Rules.ContainsKey(symbol))
			{
				return new GrammarActionResponse()
				{
					ErrorMessage = String.Format($"Символ {symbol} не определён как нетерминальный"),
					Success = false
				};
			}

			StartSymbol = symbol;
			return GrammarActionResponse.GetSuccess();
		}

		/// <summary>
		/// Построение множеств FIRST для нетерминалов
		/// </summary>
		public Dictionary<string, HashSet<Token>> BuildFirst()
		{
			var first = new Dictionary<string, HashSet<Token>>();

			/// Изначально множества пустые
			foreach (var nt in Rules)
			{
				first[nt.Key] = new HashSet<Token>();
			}

			var changed = true;

			/// Пока итеративно вносятся изменения
			while (changed)
			{
				changed = false;

				/// Проходим по всем альтернативам и пересчитываем FIRST 
				foreach (var nt in Rules)
				{
					var oldCount = first[nt.Key].Count;

					foreach (var alt in nt.Value)
					{
						first[nt.Key].UnionWith(alt.First());
					}

					changed = oldCount == first[nt.Key].Count;
				}
			}

			return first;
		}

		/// <summary>
		/// Построение FOLLOW
		/// </summary>
		public Dictionary<string, HashSet<Token>> BuildFollow()
		{
			var follow = new Dictionary<string, HashSet<Token>>();

			foreach (var nt in Rules)
			{
				follow[nt.Key] = new HashSet<Token>();
			}

			follow[StartSymbol] = new HashSet<string>() { Token.EOF };

			var changed = true;

			/// Пока итеративно вносятся изменения
			while (changed)
			{
				changed = false;

				foreach (var nt in this.Rules)
				{
					var oldCount = follow[nt.Key].Count;

					foreach (var alt in nt.Value)
						foreach(var elem in alt)
						{
							if(elem is Rule)
							{

							}
						}

					changed = oldCount == Follow[nt.Key].Count;
				}
			}

			return follow;
		}

		/// <summary>
		/// Построение замыкания множества пунктов
		/// </summary>
		public static HashSet<Marker> BuildClosure(HashSet<Marker> markers)
		{
			var changed = true;

			while(changed)
			{
				changed = false;

				
			}

			return markers;
		}


		/// <summary>
		/// Построение таблиц LL1-анализа
		/// </summary>
		public static void BuildLL1()
		{

		}

		/// <summary>
		/// Построение таблиц LR1-анализа
		/// </summary>
		public static void BuildLR1()
		{

		}
	}
}
