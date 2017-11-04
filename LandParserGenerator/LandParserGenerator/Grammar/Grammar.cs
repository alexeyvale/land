using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.LR;

namespace LandParserGenerator
{
	public class GrammarActionResponse
	{
		public bool Success { get; set; }
		public List<string> ErrorMessages { get; set; }

		public static GrammarActionResponse GetSuccess()
		{
			return new GrammarActionResponse()
			{
				Success = true,
				ErrorMessages = null
			};
		}
	}

	public class GrammarActionResponse<T> where T: class
	{
		public T Result { get; set; }
		public string ErrorMessage { get; set; }
	}

	public enum GrammarState { Unknown, Valid, Invalid }

	public class Grammar
	{
		public GrammarState State { get; private set; }

		public string StartSymbol { get; private set; }

		public Dictionary<string, NonterminalSymbol> Rules { get; private set; } = new Dictionary<string, NonterminalSymbol>();
		public Dictionary<string, TerminalSymbol> Tokens { get; private set; } = new Dictionary<string, TerminalSymbol>();
		public HashSet<string> SpecialTokens { get; private set; } = new HashSet<string>();
		public HashSet<string> SkipTokens { get; private set; } = new HashSet<string>();

		public const string EOF_TOKEN_NAME = "EOF";
		public const string TEXT_TOKEN_NAME = "TEXT";


		public ISymbol this[string key]
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

		#region Создание грамматики

		public Grammar()
		{
			DeclareSpecialTokens(TEXT_TOKEN_NAME);
			DeclareTerminal(new TerminalSymbol(EOF_TOKEN_NAME, null));

			State = GrammarState.Valid;
		}

		private void OnGrammarUpdate()
		{
			/// Если грамматика была изменена,
			/// её корректность нужно перепроверить,
			/// а множества FIRST и FOLLOW - перестроить
			State = GrammarState.Unknown;
			FirstCacheConsistent = false;
			FollowCacheConsistent = false;
		}

		private string AlreadyDeclaredCheck(ISymbol smb)
		{
			if (Rules.ContainsKey(smb.Name))
			{
				return smb is NonterminalSymbol ?
					$"Повторное определение нетерминала {smb.Name}" :
					$"Символ {smb.Name} определён как нетерминальный";
			}

			if (Tokens.ContainsKey(smb.Name))
			{
				return smb is TerminalSymbol ?
					$"Повторное определение терминала {smb.Name}" :
					$"Символ {smb.Name} определён как нетерминальный";
			}

			return String.Empty;
		}

		public GrammarActionResponse DeclareSpecialTokens(params string[] tokens)
		{
			foreach (var token in tokens)
			{
				var terminal = new TerminalSymbol(token, null);

				var checkingResult = AlreadyDeclaredCheck(terminal);

				if (!String.IsNullOrEmpty(checkingResult))
				{
					return new GrammarActionResponse()
					{
						ErrorMessages = new List<string>() { checkingResult },
						Success = false
					};
				}

				SpecialTokens.Add(token);
				//Tokens.Add(token, terminal);
			}

			OnGrammarUpdate();

			return GrammarActionResponse.GetSuccess();
		}
		public GrammarActionResponse DeclareNonterminal(NonterminalSymbol rule)
		{
			var checkingResult = AlreadyDeclaredCheck(rule);

			if (!String.IsNullOrEmpty(checkingResult))
			{
				return new GrammarActionResponse()
				{
					ErrorMessages = new List<string>() { checkingResult },
					Success = false
				};
			}

#if DEBUG
			Console.WriteLine(rule);
#endif
			OnGrammarUpdate();

			Rules[rule.Name] = rule;
			return GrammarActionResponse.GetSuccess();
		}
		public GrammarActionResponse DeclareTerminal(TerminalSymbol token)
		{
			var checkingResult = AlreadyDeclaredCheck(token);

			if (!String.IsNullOrEmpty(checkingResult))
			{
				return new GrammarActionResponse()
				{
					ErrorMessages = new List<string>() { checkingResult },
					Success = false
				};
			}

			OnGrammarUpdate();

			Tokens[token.Name] = token;
			return GrammarActionResponse.GetSuccess();
		}
		public GrammarActionResponse SetSkipTokens(params string[] tokens)
		{
			foreach(var token in tokens)
				if(!Tokens.ContainsKey(token))
				{
					return new GrammarActionResponse()
					{
						ErrorMessages = new List<string>() { $"Отсутствует описание токена {token}" },
						Success = false
					};
				}

			SkipTokens = new HashSet<string>(tokens);

			return GrammarActionResponse.GetSuccess();
		}
		public GrammarActionResponse SetStartSymbol(string symbol)
		{
			if (!this.Rules.ContainsKey(symbol))
			{
				return new GrammarActionResponse()
				{
					ErrorMessages = new List<string>() { String.Format($"Символ {symbol} не определён как нетерминальный") },
					Success = false
				};
			}

			StartSymbol = symbol;
			return GrammarActionResponse.GetSuccess();
		}
		public GrammarActionResponse CheckValidity()
		{
			var ErrorMessages = new List<string>();

			foreach(var rule in Rules.Values)
				foreach(var alt in rule)
					foreach(var smb in alt)
					{
						if(this[smb] == null)
							ErrorMessages.Add($"Неизвестный символ {smb} в правиле для нетерминала {rule.Name}");
					}

			if(String.IsNullOrEmpty(StartSymbol))
				ErrorMessages.Add($"Не задан стартовый символ");

			/// Грамматика валидна или невалидна в зависимости от результатов проверки
			State = ErrorMessages.Count > 0 ? GrammarState.Invalid : GrammarState.Valid;

			return new GrammarActionResponse()
			{
				Success = ErrorMessages.Count > 0,
				ErrorMessages = ErrorMessages.Count > 0 ? ErrorMessages : null
			};
		}

		#endregion

		#region Построение FIRST

		private bool FirstCacheConsistent { get; set; } = false;
		private Dictionary<string, HashSet<string>> _first;
		private Dictionary<string, HashSet<string>> FirstCache
		{
			get
			{
				if (_first == null || !FirstCacheConsistent)
				{
					FirstCacheConsistent = true;
					try
					{
						BuildFirst();
					}
					catch
					{
						FirstCacheConsistent = false;
						throw;
					}
				}

				return _first;
			}

			set { _first = value; }
		}

		/// <summary>
		/// Построение множеств FIRST для нетерминалов
		/// </summary>
		private void BuildFirst()
		{
			_first = new Dictionary<string, HashSet<string>>();

			/// Изначально множества пустые
			foreach (var nt in Rules)
			{
				_first[nt.Key] = new HashSet<string>();
			}

			var changed = true;

			/// Пока итеративно вносятся изменения
			while (changed)
			{
				changed = false;

				/// Проходим по всем альтернативам и пересчитываем FIRST 
				foreach (var nt in Rules)
				{
					var oldCount = _first[nt.Key].Count;

					foreach (var alt in nt.Value)
					{
						_first[nt.Key].UnionWith(First(alt));
					}

					if (!changed)
					{
						changed = oldCount != _first[nt.Key].Count;
					}
				}
			}

#if DEBUG
			foreach(var set in _first)
			{
				Console.WriteLine($"FIRST({set.Key}) = {String.Join(" ", set.Value.Select(v=> v?.ToString() ?? "eps"))}");
            }
#endif
		}

		public HashSet<string> First(Alternative alt)
		{
			/// FIRST альтернативы - это либо FIRST для первого символа в альтернативе,
			/// либо, если альтернатива пустая, соответствующий токен
			if (alt.Count > 0)
			{
				var first = First(alt[0]);

				/// Если первый элемент альтернативы - нетерминал,
				/// из которого выводится пустая строка
				for (var i=1; i < alt.Count; ++i)
				{
					/// Если из очередного элемента ветки
					/// выводится пустая строка
					if (first.Contains(null))
					{
						first.Remove(null);
						first.UnionWith(First(alt[i]));
					}
					else
						break;
				}

				return first;
			}
			else
			{
				return new HashSet<string>() { null };
			}
		}

		public HashSet<string> First(string symbol)
		{
            /// Если переданный символ является специальным
            /// и не находится в числе определённых пользователем
            if (this.SpecialTokens.Contains(symbol))
                return new HashSet<string>() { symbol };

            var gramSymbol = this[symbol];

            if (gramSymbol is NonterminalSymbol)
				return new HashSet<string>(FirstCache[gramSymbol.Name]);
			else
				return new HashSet<string>() { gramSymbol.Name };
		}

		#endregion

		#region Построение FOLLOW
		private bool FollowCacheConsistent { get; set; } = false;
		private Dictionary<string, HashSet<string>> _follow;
		private Dictionary<string, HashSet<string>> FollowCache
		{
			get
			{
				if (_follow == null || !FollowCacheConsistent)
				{
					FollowCacheConsistent = true;
					try
					{
						BuildFollow();
					}
					catch
					{
						FollowCacheConsistent = false;
						throw;
					}
				}

				return _follow;
			}

			set { _follow = value; }
		}

		/// <summary>
		/// Построение FOLLOW
		/// </summary>
		private void BuildFollow()
		{
			_follow = new Dictionary<string, HashSet<string>>();

			foreach (var nt in Rules)
			{
				_follow[nt.Key] = new HashSet<string>();
			}

			_follow[StartSymbol].Add(EOF_TOKEN_NAME);

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
								var oldCount = _follow[elem].Count;

								/// Добавляем в его FOLLOW всё, что может идти после него
								_follow[elem].UnionWith(First(alt.Subsequence(i + 1)));

								/// Если в FIRST(подпоследовательность) была пустая строка
								if (_follow[elem].Contains(null))
								{
									/// Исключаем пустую строку из FOLLOW
									_follow[elem].Remove(null);
									/// Объединяем FOLLOW текущего нетерминала
									/// с FOLLOW определяемого данной веткой
									_follow[elem].UnionWith(_follow[nt.Key]);
								}

								if (!changed)
								{
									changed = oldCount != _follow[elem].Count;
								}
							}
						}
					}
			}

#if DEBUG
			foreach (var set in _follow)
			{
				Console.WriteLine($"FOLLOW({set.Key}) = {String.Join(" ", set.Value.Select(v => v ?? "eps"))}");
			}
#endif
		}

		public HashSet<string> Follow(string nonterminal)
		{
			return FollowCache[nonterminal];
		}

		#endregion

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
						.Add(marker.Lookahead);

					foreach (var alt in nt)
					{
						foreach (var t in First(sequenceAfterNt))
						{
							closedMarkers.Add(new Marker(alt, 0, t));
						}
					}
				}
			}
			while (oldCount != closedMarkers.Count);

			return closedMarkers;
		}

		public HashSet<Marker> Goto(HashSet<Marker> I, string smb)
		{
			var res = new HashSet<Marker>();

			foreach(var marker in I.Where(m=>m.Next == smb))
			{
				res.Add(marker.ShiftNext());
			}

			return BuildClosure(res);
		}
	}
}
