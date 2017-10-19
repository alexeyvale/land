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
						first[nt.Key].UnionWith(alt.First(this));
					}

					if (!changed)
					{
						changed = oldCount == first[nt.Key].Count;
					}
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

			follow[StartSymbol].Add(Tokens[EofTokenName]);

			var changed = true;

			while (changed)
			{
				changed = false;

				/// Проходим по всем продукциям и по всем элементам веток
				foreach (var nt in this.Rules)
					foreach (var alt in nt.Value)
					{
						for (var i = 0; i < alt.Count; ++i)
						{
							var elem = alt[i];

							/// Если встретили в ветке нетерминал
							if (Rules.ContainsKey(elem))
							{
								var oldCount = follow[elem].Count;

								/// Добавляем в его FOLLOW всё, что может идти после него
								follow[elem].UnionWith(alt.Subsequence(i + 1).First(this));

								/// Если в FIRST(подпоследовательность) была пустая строка
								if (follow[elem].Contains(Tokens[Token.EmptyTokenName]))
								{
									/// Исключаем пустую строку из FOLLOW
									follow[elem].Remove(Tokens[Token.EmptyTokenName]);
									/// Объединяем FOLLOW текущего нетерминала
									/// с FOLLOW определяемого данной веткой
									follow[elem].UnionWith(follow[nt.Key]);
								}

								if (!changed)
								{
									changed = oldCount == follow[nt.Key].Count;
								}
							}
						}
					}
			}

			return follow;
		}

		/// <summary>
		/// Построение замыкания множества пунктов
		/// </summary>
		public HashSet<Marker> BuildClosure(HashSet<Marker> markers)
		{
			var closedMarkers = new HashSet<Marker>(markers);

			int oldCount;

			do
			{
				oldCount = closedMarkers.Count;

				/// Проходим по всем пунктам, которые предшествуют нетерминалам
				foreach (var marker in markers
					.Where(m => Rules.ContainsKey(m.Next)))
				{
					var nt = Rules[marker.Next];
					/// Будем брать FIRST от того, что идёт после этого нетерминала + символ предпросмотра
					var sequenceAfterNt = marker.Alternative
						.Subsequence(marker.Position)
						.Add(marker.Lookahead.Name);

					foreach (var alt in nt)
					{
						foreach (var t in sequenceAfterNt.First(this))
						{
							closedMarkers.Add(new Marker(alt, 0, t));
						}
					}
				}
			}
			while (oldCount != closedMarkers.Count);

			return closedMarkers;
		}
	}
}
