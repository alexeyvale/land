using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.LR;

namespace LandParserGenerator
{
	public enum GrammarState { Unknown, Valid, Invalid }

	public enum GrammarType { LL, LR }

	public class Grammar
	{
		private GrammarType Type { get; set; }

		// Информация об успешности конструирования грамматики
		public GrammarState State { get; private set; }

		// Информация обо всех установленных опциях
		public OptionsManager Options { get; private set; } = new OptionsManager();

		// Содержание грамматики
		public string StartSymbol { get; private set; }
		public Dictionary<string, NonterminalSymbol> Rules { get; private set; } = new Dictionary<string, NonterminalSymbol>();
		public Dictionary<string, TerminalSymbol> Tokens { get; private set; } = new Dictionary<string, TerminalSymbol>();
		public HashSet<string> NonEmptyPrecedence { get; private set; } = new HashSet<string>(); 
		public List<string> TokenOrder { get; private set; } = new List<string>();

		// Зарезервированные имена специальных токенов
		public const string EOF_TOKEN_NAME = "EOF";
		public const string TEXT_TOKEN_NAME = "Any";
		public const string ERROR_TOKEN_NAME = "ERROR";

		// Префиксы и счётчики для анонимных токенов и правил
		public const string AUTO_RULE_PREFIX = "auto__";
		private int AutoRuleCounter { get; set; } = 0;
		public const string AUTO_TOKEN_PREFIX = "AUTO__";
		private int AutoTokenCounter { get; set; } = 0;

		// Для корректных сообщений об ошибках
		public Dictionary<string, string> AutoRulesUserWrittenForm = new Dictionary<string, string>(); // AutoRuleOrigin
		private Dictionary<string, Anchor> _symbolAnchors = new Dictionary<string, Anchor>();

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

		public Grammar(GrammarType type)
		{
			Type = type;

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

		#region Описание символов

		public void AddAnchor(string smb, Anchor loc)
		{
			_symbolAnchors[smb] = loc;
		}

		public Anchor GetAnchor(string smb)
		{
			if (_symbolAnchors.ContainsKey(smb))
				return _symbolAnchors[smb];
			else
				return null;
		}

		private string AlreadyDeclaredCheck(string name)
		{
			if (Rules.ContainsKey(name))
			{
				return $"Повторное определение: символ {name} определён как нетерминальный";
			}

			if (Tokens.ContainsKey(name))
			{
				return $"Повторное определение: символ {name} определён как терминальный";
			}

			return String.Empty;
		}

        public void DeclareNonterminal(NonterminalSymbol rule)
		{
			var checkingResult = AlreadyDeclaredCheck(rule.Name);

			if (!String.IsNullOrEmpty(checkingResult))
				throw new IncorrectGrammarException(checkingResult);
            else
                Rules[rule.Name] = rule;

            OnGrammarUpdate();
		}

		public void DeclareNonterminal(string name, List<Alternative> alternatives)
		{
			var rule = new NonterminalSymbol(name, alternatives);
			DeclareNonterminal(rule);
		}

		public string GenerateNonterminal(List<Alternative> alternatives)
		{
			var newName = AUTO_RULE_PREFIX + AutoRuleCounter++;
			Rules.Add(newName, new NonterminalSymbol(newName, alternatives));
			AutoRulesUserWrittenForm[newName] = "(" + String.Join("|", alternatives
				.Select(a=> String.Join(" ", a.Elements.Select(e=>Userify(e.Symbol))))) + ")";
			return newName;
		}

		//Формируем правило для списка элементов (если указан элемент и при нём - квантификатор)
		public string GenerateNonterminal(string elemName, Quantifier quantifier, bool precNonEmpty = false)
		{
			string newName = AUTO_RULE_PREFIX + AutoRuleCounter++;

			switch (quantifier)
			{
				case Quantifier.ONE_OR_MORE:
					switch(Type)
					{
						case GrammarType.LL:
							Rules[newName] = new NonterminalSymbol(newName, new string[][]{
								new string[]{ },
								new string[]{ elemName, newName }
							});
							if (precNonEmpty)
								NonEmptyPrecedence.Add(newName);
							AutoRulesUserWrittenForm[newName] = Userify(elemName) + "+";

							var oldName = newName;
							newName = AUTO_RULE_PREFIX + AutoRuleCounter++;
							Rules[newName] = new NonterminalSymbol(newName, new string[][]{
								new string[]{ elemName, oldName }
							});
							AutoRulesUserWrittenForm[newName] = Userify(elemName) + "+";
							break;
						case GrammarType.LR:
							Rules[newName] = new NonterminalSymbol(newName, new string[][]{
								new string[]{ elemName },
								new string[]{ newName, elemName }
							});
							AutoRulesUserWrittenForm[newName] = Userify(elemName) + "+";
							break;
						default:
							break;
					}
					break;
				case Quantifier.ZERO_OR_MORE:			
					switch (Type)
					{
						case GrammarType.LL:						
							Rules[newName] = new NonterminalSymbol(newName, new string[][]{
								new string[]{ },
								new string[]{ elemName, newName }
							});
							break;
						case GrammarType.LR:
							newName = AUTO_RULE_PREFIX + AutoRuleCounter++;
							Rules[newName] = new NonterminalSymbol(newName, new string[][]{
								new string[]{ },
								new string[]{ newName, elemName }
							});
							break;
						default:
							break;
					}
					if (precNonEmpty)
						NonEmptyPrecedence.Add(newName);
					AutoRulesUserWrittenForm[newName] = Userify(elemName) + "*";
					break;
				case Quantifier.ZERO_OR_ONE:
					Rules[newName] = new NonterminalSymbol(newName, new string[][]{
						new string[]{ },
						new string[]{ elemName }
					});
					if (precNonEmpty)
						NonEmptyPrecedence.Add(newName);
					AutoRulesUserWrittenForm[newName] = Userify(elemName) + "?";
					break;
			}

			return newName;
		}

		//Добавляем к терминалам регулярное выражение, в чистом виде встреченное в грамматике
		public string GenerateTerminal(string regex)
		{
			//Если оно уже сохранено с каким-то именем, не дублируем, а возвращаем это имя
			foreach (var token in Tokens.Values)
				if (token.Pattern != null && token.Pattern.Equals(regex))
					return token.Name;

			var newName = AUTO_TOKEN_PREFIX + AutoTokenCounter++;
			Tokens.Add(newName, new TerminalSymbol(newName, regex));
			return newName;
		}

		public void DeclareTerminal(TerminalSymbol terminal)
		{
			var checkingResult = AlreadyDeclaredCheck(terminal.Name);

			if (!String.IsNullOrEmpty(checkingResult))
				throw new IncorrectGrammarException(checkingResult);
			else
			{
				TokenOrder.Add(terminal.Name);
				Tokens[terminal.Name] = terminal;
			}

			OnGrammarUpdate();
		}

		public void DeclareTerminal(string name,  string pattern)
		{
			var terminal = new TerminalSymbol(name, pattern);
			DeclareTerminal(terminal);
		}

		#endregion

		#region Учёт опций

		public void SetOption(NodeOption option, params string[] symbols)
		{
			Options.Set(option, symbols);

			var errorSymbols = CheckIfNonterminals(symbols);
			if (errorSymbols.Count > 0)
				throw new IncorrectGrammarException(
					$"Символы '{String.Join("', '", errorSymbols)}' не определёны как нетерминальные"
				);
		}

		public void SetOption(ParsingOption option, params string[] symbols)
		{
			Options.Set(option, symbols);

			switch(option)
			{
				case ParsingOption.START:
					StartSymbol = Options.GetSymbols(ParsingOption.START).FirstOrDefault();
					if (CheckIfNonterminals(StartSymbol).Count > 0)
						throw new IncorrectGrammarException(
							$"В качестве стартового указан символ '{StartSymbol}', не являющийся нетерминальным"
						);
					break;
				case ParsingOption.SKIP:
					var errorSymbols = CheckIfTerminals(symbols);
					if (errorSymbols.Count > 0)
						throw new IncorrectGrammarException(
							$"Символы '{String.Join("', '", errorSymbols)}' не определёны как терминальные"
						);
					break;
				default:
					break;
			}
		}

		public void SetOption(MappingOption option, params string[] symbols)
		{
			Options.Set(option, symbols);

			var errorSymbols = CheckIfSymbols(symbols);
			if (errorSymbols.Count > 0)
				throw new IncorrectGrammarException(
					$"Символы '{String.Join("', '", errorSymbols)}' не определёны в грамматике"
				);
		}

		public void SetOption(MappingOption option, string[] symbols, params dynamic[] @params)
		{
			Options.Set(option, symbols, @params);

			var errorSymbols = CheckIfSymbols(symbols);
			if (errorSymbols.Count > 0)
				throw new IncorrectGrammarException(
					$"Символы '{String.Join("', '", errorSymbols)}' не определёны в грамматике"
				);
		}

		private List<string> CheckIfNonterminals(params string[] symbols)
		{
			return symbols.Where(s => !this.Rules.ContainsKey(s)).ToList();
		}

		private List<string> CheckIfTerminals(params string[] symbols)
		{
			return symbols.Where(s => !this.Tokens.ContainsKey(s)).ToList();
		}

		private List<string> CheckIfSymbols(params string[] symbols)
		{
			return CheckIfNonterminals(symbols).Intersect(CheckIfTerminals(symbols)).ToList();
		}

		#endregion

		/// <summary>
		/// Замена символа во всех правилах
		/// </summary>
		/// <param name="from">Заменяемый символ</param>
		/// <param name="to">Символ, на который заменяем</param>
		private void ChangeSymbol(string from, string to)
		{
			foreach (var rule in Rules.Values)
				foreach (var alt in rule.Alternatives)
					foreach (var elem in alt.Elements)
						if (elem.Symbol == from)
							elem.Symbol = to;

			if (Rules.ContainsKey(from))
			{
				var body = Rules[from];
				Rules.Remove(from);
				Rules.Add(to, body);
			}
			else if (Tokens.ContainsKey(from))
			{
				var body = Tokens[from];
				Tokens.Remove(from);
				Tokens.Add(to, body);
			}

		}

		public IEnumerable<Message> CheckValidity()
		{
			var errors = new LinkedList<Message>();

            foreach (var rule in Rules.Values)
            {
#if DEBUG
                Console.WriteLine(rule);
#endif
                foreach (var alt in rule)
                    foreach (var smb in alt)
                    {
                        if (this[smb] == null)
							errors.AddLast(Message.Error(
								$"Неизвестный символ {smb} в правиле для нетерминала {Userify(rule.Name)}",
								GetAnchor(rule.Name),
								"LanD"
							));
                    }
            }

			if (String.IsNullOrEmpty(StartSymbol))
				errors.AddLast(Message.Error(
					$"Не задан стартовый символ",
					null,
					"LanD"
				));

			/// Грамматика валидна или невалидна в зависимости от результатов проверки
			State = errors.Count > 0 ? GrammarState.Invalid : GrammarState.Valid;

            return errors;
		}

		public string Userify(string name)
		{
			return name.StartsWith(AUTO_TOKEN_PREFIX) ? Tokens[name].Pattern :
				name.StartsWith(AUTO_RULE_PREFIX) ? AutoRulesUserWrittenForm[name] :
				name;
		}

		public string Userify(ISymbol smb)
		{
			return Userify(smb.Name);
		}

		public string Userify(Alternative alt)
		{
			return alt.Elements.Count > 0 ? String.Join(" ", alt.Elements.Select(e => Userify(e.Symbol))) : "eps";
		}

		#endregion

		#region Построение FIRST

		/// Нужно ли использовать модифицированный алгоритм First
		/// (с учётом пустого Any)
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

		#region For LR Parsing

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

		#endregion
	}
}
