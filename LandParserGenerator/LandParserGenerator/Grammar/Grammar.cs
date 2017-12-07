using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.LR;

namespace LandParserGenerator
{
	public enum GrammarState { Unknown, Valid, Invalid }

	public class Grammar
	{
		public GrammarState State { get; private set; }
        private LinkedList<string> ConstructionErrors { get; set; } = new LinkedList<string>();

        public string StartSymbol { get; private set; }
		public HashSet<string> ListSymbols { get; private set; } = new HashSet<string>();
		public HashSet<string> GhostSymbols { get; private set; } = new HashSet<string>();

		public Dictionary<string, NonterminalSymbol> Rules { get; private set; } = new Dictionary<string, NonterminalSymbol>();
		public Dictionary<string, TerminalSymbol> Tokens { get; private set; } = new Dictionary<string, TerminalSymbol>();
		public HashSet<string> SpecialTokens { get; private set; } = new HashSet<string>();
		public HashSet<string> SkipTokens { get; private set; } = new HashSet<string>();

        public List<string> TokenOrder { get; private set; } = new List<string>();

		public const string EOF_TOKEN_NAME = "EOF";
		public const string TEXT_TOKEN_NAME = "TEXT";
        public const string ERROR_TOKEN_NAME = "ERROR";

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
			DeclareTerminal(new TerminalSymbol(TEXT_TOKEN_NAME, null));
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

		public void DeclareSpecialTokens(params string[] tokens)
		{
			foreach (var token in tokens)
			{
				var terminal = new TerminalSymbol(token, null);
				var checkingResult = AlreadyDeclaredCheck(terminal);

				if (!String.IsNullOrEmpty(checkingResult))
                    ConstructionErrors.AddLast(checkingResult);
                else
                    SpecialTokens.Add(token);			
			}

			OnGrammarUpdate();
		}
        public void RemoveSpecialToken(string token)
        {
            SpecialTokens.Remove(token);
            OnGrammarUpdate();
        }
        public void DeclareNonterminal(NonterminalSymbol rule)
		{
			var checkingResult = AlreadyDeclaredCheck(rule);

			if (!String.IsNullOrEmpty(checkingResult))
                ConstructionErrors.AddLast(checkingResult);
            else
                Rules[rule.Name] = rule;

            OnGrammarUpdate();
		}
		public void DeclareTerminal(TerminalSymbol token)
		{
			var checkingResult = AlreadyDeclaredCheck(token);

            if (!String.IsNullOrEmpty(checkingResult))
                ConstructionErrors.AddLast(checkingResult);
            else
            {
                TokenOrder.Add(token.Name);
                Tokens[token.Name] = token;
            }

            OnGrammarUpdate();
		}

		public void SetSkipTokens(params string[] tokens)
		{
			foreach(var token in tokens)
				if(!Tokens.ContainsKey(token))
				{
                    throw new IncorrectGrammarException($"Отсутствует описание токена { token }");
				}

			SkipTokens = new HashSet<string>(tokens);
		}
		public void SetStartSymbol(string symbol)
		{
			if (!this.Rules.ContainsKey(symbol))
            {
                throw new IncorrectGrammarException(
                   $"Символ {symbol} не определён как нетерминальный"
                );
            }

            StartSymbol = symbol;
		}
		public void SetListSymbols(params string[] symbols)
		{
			ListSymbols = new HashSet<string>(symbols);

            foreach (var symbol in symbols)
            {
                if (!this.Rules.ContainsKey(symbol))
                {
                    throw new IncorrectGrammarException($"Символ {symbol} не определён как нетерминальный");
                }
            }
		}
		public void SetGhostSymbols(params string[] symbols)
		{
			GhostSymbols = new HashSet<string>(symbols);

			foreach (var symbol in symbols)
			{
				if (!this.Rules.ContainsKey(symbol))
				{
					throw new IncorrectGrammarException(
                        String.Format($"Символ {symbol} не определён как нетерминальный"));
				}
			}
		}
		public IEnumerable<string> CheckValidity()
		{
            var ErrorMessages = ConstructionErrors;

            foreach (var rule in Rules.Values)
            {
#if DEBUG
                Console.WriteLine(rule);
#endif
                foreach (var alt in rule)
                    foreach (var smb in alt)
                    {
                        if (this[smb] == null && !SpecialTokens.Contains(smb))
                            ErrorMessages.AddLast($"Неизвестный символ {smb} в правиле для нетерминала {rule.Name}");
                    }
            }

			if(String.IsNullOrEmpty(StartSymbol))
                ErrorMessages.AddLast($"Не задан стартовый символ");

			/// Грамматика валидна или невалидна в зависимости от результатов проверки
			State = ErrorMessages.Count > 0 ? GrammarState.Invalid : GrammarState.Valid;
            ConstructionErrors = new LinkedList<string>();

            return ErrorMessages;
		}

        #endregion

        #region Построение FIRST

        /// Нужно ли использовать модифицированный алгоритм First
        /// (с учётом пустого TEXT)
        private bool _useModifiedFirst = false;
        public bool UseModifiedFirst
        {
            get { return _useModifiedFirst; }
            set
            {
                if(value != _useModifiedFirst)
                {
                    FirstCacheConsistent = false;
                    FollowCacheConsistent = false;
                    _useModifiedFirst = value;
                }
            }
        }
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
			/// либо, если альтернатива пустая, null
			if (alt.Count > 0)
			{
                var first = new HashSet<string>();
                var elementsCounter = 0;

                /// Если первый элемент альтернативы - нетерминал,
                /// из которого выводится пустая строка,
                /// нужно взять first от следующего элемента
                for (; elementsCounter < alt.Count; ++elementsCounter)
                {
                    var elemFirst = First(alt[elementsCounter]);
                    var containsEmpty = elemFirst.Remove(null);

                    first.UnionWith(elemFirst);

                    /// Если из текущего элемента нельзя вывести пустую строку
                    /// и (для модифицированной версии First) он не равен ANY
                    if (!containsEmpty 
                        && (!UseModifiedFirst || alt[elementsCounter] != TEXT_TOKEN_NAME))
                        break;
                }

                if (elementsCounter == alt.Count)
                    first.Add(null);

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

				var newMarkers = new HashSet<Marker>();

				/// Проходим по всем пунктам, которые предшествуют нетерминалам
				foreach (var marker in closedMarkers
					.Where(m => Rules.ContainsKey(m.Next)))
				{
					var nt = Rules[marker.Next];
					/// Будем брать FIRST от того, что идёт после этого нетерминала + символ предпросмотра
					var sequenceAfterNt = marker.Alternative
						.Subsequence(marker.Position + 1)
						.Add(marker.Lookahead);

					foreach (var alt in nt)
					{
						foreach (var t in First(sequenceAfterNt))
						{
							newMarkers.Add(new Marker(alt, 0, t));
						}
					}
				}

				closedMarkers.UnionWith(newMarkers);
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
