using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Land.Core.Parsing.LR;

namespace Land.Core
{
	public enum GrammarState { Unknown, Valid, Invalid }

	public enum GrammarType { LL, LR }

	public enum AnyOption { Include, Except, Avoid, IgnorePairs }

	[Serializable]
	public class Grammar
	{
		[Serializable]
		public class ElementQuantifierPair
		{
			public string Element { get; set; }
			public Quantifier Quantifier { get; set; }
		}

		public GrammarType Type { get; private set; }

		// Информация об успешности конструирования грамматики
		public GrammarState State { get; private set; }

		// Информация обо всех установленных опциях
		public OptionsManager Options { get; private set; } = new OptionsManager();

		// Содержание грамматики
		public string StartSymbol { get; private set; }
		public Dictionary<string, NonterminalSymbol> Rules { get; private set; } = new Dictionary<string, NonterminalSymbol>();
		public Dictionary<string, TerminalSymbol> Tokens { get; private set; } = new Dictionary<string, TerminalSymbol>();
		public Dictionary<string, PairSymbol> Pairs { get; private set; } = new Dictionary<string, PairSymbol>();
		public HashSet<string> NonEmptyPrecedence { get; private set; } = new HashSet<string>(); 
		public List<string> TokenOrder { get; private set; } = new List<string>();

		// Псевдонимы именованных нетерминалов
		public Dictionary<string, HashSet<string>> Aliases = new Dictionary<string, HashSet<string>>();

		// Зарезервированные имена специальных токенов
		public const string EOF_TOKEN_NAME = "EOF";
		public const string UNDEFINED_TOKEN_NAME = "UNDEFINED";
		public const string ERROR_TOKEN_NAME = "ERROR";
		public const string ANY_TOKEN_NAME = "Any";

		// Префиксы и счётчики для анонимных токенов и правил
		public const string AUTO_RULE_PREFIX = "auto__";
		private int AutoRuleCounter { get; set; } = 0;
		public const string AUTO_TOKEN_PREFIX = "AUTO__";
		private int AutoTokenCounter { get; set; } = 0;

		// Для корректных сообщений об ошибках
		public Dictionary<string, string> AutoTokenUserWrittenForm = new Dictionary<string, string>();
		public Dictionary<string, string> AutoRuleUserWrittenForm = new Dictionary<string, string>();
		public Dictionary<string, ElementQuantifierPair> AutoRuleQuantifier = new Dictionary<string, ElementQuantifierPair>();
		private Dictionary<string, PointLocation> _symbolAnchors = new Dictionary<string, PointLocation>();

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

				if (Pairs.ContainsKey(key))
				{
					return Pairs[key];
				}

				return null;
			}
		}

		#region Создание грамматики

		public Grammar(GrammarType type)
		{
			Type = type;

			DeclareTerminal(new TerminalSymbol(ANY_TOKEN_NAME, null));
			DeclareTerminal(new TerminalSymbol(EOF_TOKEN_NAME, null));
			DeclareTerminal(new TerminalSymbol(UNDEFINED_TOKEN_NAME, null));

			State = GrammarState.Valid;
		}

		public void OnGrammarUpdate()
		{
			/// Если грамматика была изменена,
			/// её корректность нужно перепроверить,
			/// а множества FIRST и FOLLOW - перестроить
			State = GrammarState.Unknown;
			FirstCacheConsistent = false;
			FollowCacheConsistent = false;
			SentenceTokensCacheConsistent = false;
		}

		#region Описание символов

		public void AddAliases(string smb, HashSet<string> aliases)
		{
			var intersectionWithSymbols = aliases.Intersect(Rules.Keys.Union(Tokens.Keys)).ToList();

			if(intersectionWithSymbols.Count > 0)
			{
				throw new IncorrectGrammarException(
						$"Среди псевдонимов нетерминала {smb} присутствуют символы, определённые как терминалы или нетерминалы: {String.Join(", ", intersectionWithSymbols)}"
					);
			}

			foreach(var kvp in Aliases)
			{
				var intersectionWithAliases = kvp.Value.Intersect(aliases).ToList();

				if(intersectionWithAliases.Count > 0)
					throw new IncorrectGrammarException(
						$"Нетерминалы {smb} и {kvp.Key} имеют общие псевдонимы: {String.Join(", ", intersectionWithAliases)}"
					);
			}

			Aliases[smb] = aliases;
		}

		public void AddAnchor(string smb, PointLocation loc)
		{
			_symbolAnchors[smb] = loc;
		}

		public PointLocation GetAnchor(string smb)
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

			if (Pairs.ContainsKey(name))
			{
				return $"Повторное определение: символ {name} определён как пара";
			}

			if (Aliases.Any(p=>p.Value.Contains(name)))
			{
				return $"Повторное определение: символ {name} определён как псевдоним";
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
			if (Type == GrammarType.LR)
			{
				/// Проверяем, нет ли уже правила с такими альтернативами
				var generated = Rules.Where(r => r.Key.StartsWith(AUTO_RULE_PREFIX))
					.Select(r => r.Value).FirstOrDefault(r => r.Alternatives.Intersect(alternatives).Count() == alternatives.Count);

				if (generated != null)
					return generated.Name;
			}

			var newName = AUTO_RULE_PREFIX + AutoRuleCounter++;
			Rules.Add(newName, new NonterminalSymbol(newName, alternatives));
			return newName;
		}

		/// Формируем правило для списка элементов (если указан элемент и при нём - квантификатор)
		public string GenerateNonterminal(string elemName, Quantifier quantifier, bool precNonEmpty = false)
		{
			if (Type == GrammarType.LR)
			{
				var generated = Rules.Where(r => r.Key.StartsWith(AUTO_RULE_PREFIX))
				.Select(r => r.Value).FirstOrDefault(r => AutoRuleQuantifier.ContainsKey(r.Name)
				  && AutoRuleQuantifier[r.Name].Element == elemName
				  && AutoRuleQuantifier[r.Name].Quantifier == quantifier);

				if (generated != null)
					return generated.Name;
			}

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
							AutoRuleQuantifier[newName] = new ElementQuantifierPair()
							{
								Element = elemName,
								Quantifier = quantifier
							};

							var oldName = newName;
							newName = AUTO_RULE_PREFIX + AutoRuleCounter++;
							Rules[newName] = new NonterminalSymbol(newName, new string[][]{
								new string[]{ elemName, oldName }
							});
							AutoRuleQuantifier[newName] = new ElementQuantifierPair()
							{
								Element = elemName,
								Quantifier = quantifier
							};
							break;
						case GrammarType.LR:
							Rules[newName] = new NonterminalSymbol(newName, new string[][]{
								new string[]{ elemName },
								new string[]{ newName, elemName }
							});
							AutoRuleQuantifier[newName] = new ElementQuantifierPair()
							{
								Element = elemName,
								Quantifier = quantifier
							};
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
					AutoRuleQuantifier[newName] = new ElementQuantifierPair()
					{
						Element = elemName,
						Quantifier = quantifier
					};
					break;
				case Quantifier.ZERO_OR_ONE:
					Rules[newName] = new NonterminalSymbol(newName, new string[][]{
						new string[]{ },
						new string[]{ elemName }
					});
					if (precNonEmpty)
						NonEmptyPrecedence.Add(newName);
					AutoRuleQuantifier[newName] = new ElementQuantifierPair()
					{
						Element = elemName,
						Quantifier = quantifier
					};
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
			AutoTokenUserWrittenForm[newName] = regex;

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

		public void DeclarePair(string name, HashSet<string> left, HashSet<string> right)
		{
			var checkingResult = AlreadyDeclaredCheck(name);

			if (!String.IsNullOrEmpty(checkingResult))
				throw new IncorrectGrammarException(checkingResult);
			else
			{
				Pairs[name] = new PairSymbol()
				{
					Left = left,
					Right = right,
					Name = name
				};
			}

			OnGrammarUpdate();
		}

		#endregion

		#region Учёт опций

		public void SetOption(NodeOption option, params string[] symbols)
		{
			Options.Set(option, symbols);

			var errorSymbols = CheckIfNonterminals(symbols).Intersect(СheckIfAliases(symbols)).ToList();
			if (errorSymbols.Count > 0)
				throw new IncorrectGrammarException(
					$"Символы '{String.Join("', '", errorSymbols)}' не определены как нетерминальные или псевдонимы"
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
							$"Символы '{String.Join("', '", errorSymbols)}' не определены как терминальные"
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
					$"Символы '{String.Join("', '", errorSymbols)}' не определены в грамматике"
				);
		}

		public void SetOption(MappingOption option, string[] symbols, params dynamic[] @params)
		{
			if (symbols == null || symbols.Length == 0)
			{
				symbols = new string[] { OptionsManager.GLOBAL_PARAMETERS_SYMBOL };
				Options.Set(option, symbols, @params);
			}
			else
			{
				Options.Set(option, symbols, @params);

				var errorSymbols = CheckIfSymbols(symbols);
				if (errorSymbols.Count > 0)
					throw new IncorrectGrammarException(
						$"Символы '{String.Join("', '", errorSymbols)}' не определены в грамматике"
					);
			}
		}

		private List<string> CheckIfNonterminals(params string[] symbols)
		{
			return symbols.Where(s => !this.Rules.ContainsKey(s)).ToList();
		}

		private List<string> CheckIfTerminals(params string[] symbols)
		{
			return symbols.Where(s => !this.Tokens.ContainsKey(s)).ToList();
		}

		private List<string> СheckIfAliases(params string[] symbols)
		{
			return symbols.Where(s => !this.Aliases.Any(a => a.Value.Contains(s))).ToList();
		}

		private List<string> CheckIfSymbols(params string[] symbols)
		{
			return CheckIfNonterminals(symbols)
				.Intersect(CheckIfTerminals(symbols))
				.Intersect(СheckIfAliases(symbols)).ToList();
		}

		#endregion

		public void PostProcessing()
		{
			/// Для LR грамматики добавляем фиктивный стартовый символ, чтобы произошла
			/// финальная свёртка
			if(Type == GrammarType.LR)
			{
				if (!String.IsNullOrEmpty(StartSymbol))
				{
					var newStartName = AUTO_RULE_PREFIX + AutoRuleCounter++;

					this.DeclareNonterminal(new NonterminalSymbol(newStartName, new string[][]
					{
						new string[]{ StartSymbol }
					}));

					this.Options.Clear(ParsingOption.START);
					this.SetOption(ParsingOption.START, newStartName);
				}
			}

			if(Options.IsSet(ParsingOption.IGNORECASE))
			{
				foreach (var token in Tokens.Where(t => t.Key.StartsWith(AUTO_TOKEN_PREFIX)))
					token.Value.Pattern = String.Join("",
						token.Value.Pattern.Trim('\'').Select(c => Char.IsLetter(c) ? $"[{Char.ToLower(c)}{Char.ToUpper(c)}]" : $"'{c}'"));
            }
		}

		public IEnumerable<Message> CheckValidity()
		{
			var messages = new List<Message>();

            foreach (var rule in Rules.Values)
            {
				messages.Add(Message.Trace(
					rule.ToString(),
					GetAnchor(rule.Name),
					"LanD"
				));

				foreach (var alt in rule)
                    foreach (var smb in alt)
                    {
                        if (this[smb] == null)
							messages.Add(Message.Error(
								$"Неизвестный символ {smb} в правиле для нетерминала {Userify(rule.Name)}",
								GetAnchor(rule.Name),
								"LanD"
							));

						if(smb == Grammar.ANY_TOKEN_NAME)
						{
							var union = new HashSet<string>();

							/// Проверяем, что в качестве аргументов для Any не указаны
							/// имена неизвестных символов
							foreach (var kvp in smb.Options.AnyOptions)
							{
								union.UnionWith(kvp.Value);
								foreach (var arg in kvp.Value)
								{
									if (this[arg] == null)
										messages.Add(Message.Error(
											$"Неизвестный символ {Userify(arg)} в аргументах опции {kvp.Key} символа {Grammar.ANY_TOKEN_NAME} для нетерминала {Userify(rule.Name)}",
											GetAnchor(rule.Name),
											"LanD"
										));
								}
							}

							/// Проверяем, что не существует символов, указанных
							/// сразу в нескольких опциях
							if (union.Count < smb.Options.AnyOptions.Sum(o => o.Value.Count))
							{
								messages.Add(Message.Error(
											$"Множества аргументов нескольких опций символа {Grammar.ANY_TOKEN_NAME} для нетерминала {Userify(rule.Name)} пересекаются",
											GetAnchor(rule.Name),
											"LanD"
										));
							}
						}
                    }
            }	

			/// Если в грамматике не фигурируют неопределённые символы
			if (messages.All(m=>m.Type != MessageType.Error))
			{
				/// Проверяем наличие левой рекурсии для случая LL
				if (Type == GrammarType.LL)
				{
					var emptyElementsRepetition = AutoRuleQuantifier.Where(kvp => 
						(kvp.Value.Quantifier == Quantifier.ZERO_OR_MORE || kvp.Value.Quantifier == Quantifier.ONE_OR_MORE)
						&& First(kvp.Value.Element).Contains(null)).Select(kvp => kvp.Key).ToList();

					foreach (var nt in emptyElementsRepetition)
					{
						messages.Add(Message.Error(
							$"Определение нетерминала {Userify(nt)} допускает левую рекурсию: в списке допустимо бесконечное количество пустых элементов",
							GetAnchor(nt),
							"LanD"
						));
					}

					foreach (var nt in FindLeftRecursion().Except(emptyElementsRepetition))
					{
						messages.Add(Message.Error(
							$"Определение нетерминала {Userify(nt)} допускает левую рекурсию",
							GetAnchor(nt),
							"LanD"
						));
					}
				}

				/// Также добавляем предупреждения о последовательно идущих Any
				messages.AddRange(CheckConsecutiveAny());
			}

			if (String.IsNullOrEmpty(StartSymbol))
				messages.Add(Message.Error(
					$"Не задан стартовый символ",
					null,
					"LanD"
				));

			/// Грамматика валидна или невалидна в зависимости от наличия сообщений об ошибках
			State = messages.Any(m=>m.Type == MessageType.Error) ? GrammarState.Invalid : GrammarState.Valid;

			return messages;
		}

		/// Возвращает леворекурсивно определённые нетерминалы
		private List<string> FindLeftRecursion()
		{
			List<string> recursive = new List<string>();

			/// Составили списки смежностей для нетерминалов,
			/// учитываем только нетерминалы, которым не предшествуют 
			Dictionary<string, HashSet<string>> graph = new Dictionary<string, HashSet<string>>();
			foreach (string nt in Rules.Keys)
			{
				graph.Add(nt, new HashSet<string>());
				foreach (var alt in Rules[nt])
				{
					var altStartingNonterminals = alt.Elements.Select(e => e.Symbol).TakeWhile(s => Rules.ContainsKey(s)).ToList();

					for(var i = 0; i< altStartingNonterminals.Count; ++i)
					{
						graph[nt].Add(altStartingNonterminals[i]);

						if (!First(altStartingNonterminals[i]).Contains(null))
							break;
					}
				}
			}

			/// Готовимся к первому DFS
			/// Завели структуры для хранения номеров и метки открытия
			Dictionary<string, bool> opened = new Dictionary<string, bool>(graph.Count);
			Stack<string> finished = new Stack<string>(graph.Count);
			int maxInd = graph.Count;
			foreach (string res in graph.Keys)
				opened.Add(res, false);
			Stack<string> way = new Stack<string>();
			/// Обходим в глубину
			foreach (string key in graph.Keys)
				if (!opened[key])
				{
					way.Push(key);
					opened[key] = true;
					while (way.Count > 0)
					{
						string curSymb = graph[way.Peek()].FirstOrDefault(e => !opened[e]);
						if (curSymb != null)
						{
							opened[curSymb] = true;
							way.Push(curSymb);
						}
						else
							finished.Push(way.Pop());
					}
				}


			/// Инвертируем граф
			Dictionary<string, HashSet<string>> invertedGraph = new Dictionary<string, HashSet<string>>(graph.Count);
			foreach (string key in graph.Keys)
				invertedGraph.Add(key, new HashSet<string>());
			foreach (KeyValuePair<string, HashSet<string>> p in graph)
				foreach (string str in p.Value)
					invertedGraph[str].Add(p.Key);

			/// Готовимся ко второму DFS
			foreach (string res in graph.Keys)
				opened[res] = false;
			way = new Stack<string>();
			bool bigComponent = false;

			foreach (string key in finished)
				if (!opened[key])
				{
					way.Push(key);
					opened[key] = true;
					while (way.Count > 0)
					{
						string curSymb = invertedGraph[way.Peek()].FirstOrDefault(e => !opened[e]);
						if (curSymb != null)
						{
							opened[curSymb] = true;
							way.Push(curSymb);
						}
						else
							if (way.Count == 1)
						{
							if (bigComponent || graph[way.Peek()].Contains(way.Peek()))
								recursive.Add(way.Peek());
							way.Pop();
							bigComponent = false;
						}
						else
						{
							bigComponent = true;
							recursive.Add(way.Peek());
							way.Pop();
						}
					}
				}

			return recursive;
		}

		private List<Message> CheckConsecutiveAny()
		{
			var messages = new List<Message>();
			var anys = new Dictionary<string, Tuple<Alternative, int>>();

			/// Временно подменяем все вхождения Any на AnyНомер,
			/// чтобы сделать каждое вхождение уникальным
			foreach (var rule in Rules.Values)
				foreach (var alt in rule)
					for (var i = 0; i < alt.Count; ++i)
					{
						if (alt[i].Symbol == Grammar.ANY_TOKEN_NAME
							&& !alt[i].Options.AnyOptions.ContainsKey(AnyOption.Except))
						{
							var newName = Grammar.ANY_TOKEN_NAME + anys.Count;
							anys[newName] = new Tuple<Alternative, int>(alt, i);
							alt[i].Symbol = newName;
							Tokens.Add(newName, new TerminalSymbol(newName, String.Empty));
						}
					}
			BuildFirst();
			BuildFollow();

			/// Для каждого Any находим Any, которые могут идти после него
			/// и не являются этим же самым Any
			foreach(var pair in anys.Values)
			{
				var nextTokens = First(pair.Item1.Subsequence(pair.Item2 + 1));
				if (nextTokens.Contains(null))
				{
					nextTokens.Remove(null);
					nextTokens.UnionWith(Follow(pair.Item1.NonterminalSymbolName));
				}

				/// Множество токенов Any, о которых надо предупредить разработчика грамматики
				var warningTokens = nextTokens.Where(t => t.StartsWith(Grammar.ANY_TOKEN_NAME) && t != pair.Item1[pair.Item2]);

				if (warningTokens.Count() > 0)
				{
					var anyUserifyRegex = $"{Grammar.ANY_TOKEN_NAME}\\d+";
					var fromAltUserified = Regex.Replace(Userify(pair.Item1), anyUserifyRegex, Grammar.ANY_TOKEN_NAME);
					var fromNontermUserified = Regex.Replace(Userify(pair.Item1.NonterminalSymbolName), anyUserifyRegex, Grammar.ANY_TOKEN_NAME);

					foreach (var token in warningTokens)
					{
						var nextAltUserified = Regex.Replace(Userify(anys[token].Item1), anyUserifyRegex, Grammar.ANY_TOKEN_NAME);
						var nextNontermUserified = Regex.Replace(Userify(anys[token].Item1.NonterminalSymbolName), anyUserifyRegex, Grammar.ANY_TOKEN_NAME);

						messages.Add(Message.Warning(
							$"После символа {Grammar.ANY_TOKEN_NAME} из альтернативы {fromAltUserified} нетерминала {fromNontermUserified} может следовать символ {Grammar.ANY_TOKEN_NAME} из альтернативы {nextAltUserified} нетерминала {nextNontermUserified}",
							GetAnchor(pair.Item1.NonterminalSymbolName),
							"LanD"
						));
					}
				}
			}

			/// Возвращаем всё как было
			foreach (var val in anys.Values)
			{
				Tokens.Remove(val.Item1[val.Item2].Symbol);
				val.Item1[val.Item2].Symbol = Grammar.ANY_TOKEN_NAME;
			}

			OnGrammarUpdate();

			return messages;
		}

		public void RebuildUserificationCache()
		{
			AutoRuleUserWrittenForm = new Dictionary<string, string>();

			foreach(var smb in Rules.Keys.Where(k=>k.StartsWith(AUTO_RULE_PREFIX)))
				AutoRuleUserWrittenForm[smb] = Userify(smb);
		}

		public string Userify(string name)
		{
			if(name.StartsWith(AUTO_RULE_PREFIX))
			{
				if (AutoRuleUserWrittenForm.ContainsKey(name))
					return AutoRuleUserWrittenForm[name];

				if(AutoRuleQuantifier.ContainsKey(name))
				{
					var elementName = Rules[name].Alternatives
						.SelectMany(a => a.Elements).FirstOrDefault(e => e.Symbol != name);

					switch (AutoRuleQuantifier[name].Quantifier)
					{
						case Quantifier.ONE_OR_MORE:
							return Userify(elementName) + "+";
						case Quantifier.ZERO_OR_MORE:
							return Userify(elementName) + "*";
						case Quantifier.ZERO_OR_ONE:					
							return Userify(elementName) + "?";
					}
				}
				else
				{
					return $"({String.Join(" | ", Rules[name].Alternatives.Select(a=>Userify(a)))})";
                }
			}

			return AutoTokenUserWrittenForm.ContainsKey(name) ? AutoTokenUserWrittenForm[name] : name;
		}

		public string Userify(Entry entry)
		{
			if (entry.Options.AnyOptions.Count > 0)
				return $"{Grammar.ANY_TOKEN_NAME}({String.Join(", ", entry.Options.AnyOptions.Select(kvp => $"{kvp.Key}({String.Join(", ", kvp.Value.Select(e => Userify(e)))})"))})";
			else
				return Userify(entry.Symbol);
		}

		public string Userify(ISymbol smb)
		{
			return Userify(smb.Name);
		}

		public string Userify(Alternative alt)
		{
			/// Если альтернатива пустая, показываем пользователю эпсилон
			return alt.Elements.Count > 0 ? String.Join(" ", UserifyElementwise(alt)) : "\u03B5";
		}

		public List<string> UserifyElementwise(Alternative alt)
		{
			return alt.Elements.Select(e => Userify(e)).ToList();
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
                        && (!UseModifiedFirst || alt[elementsCounter] != ANY_TOKEN_NAME))
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
		}

		public HashSet<string> Follow(string nonterminal)
		{
			return FollowCache[nonterminal];
		}

		#endregion

		#region Построение SentenceTokens

		private bool SentenceTokensCacheConsistent { get; set; } = false;
		private Dictionary<string, HashSet<string>> _sentenceTokens;
		private Dictionary<string, HashSet<string>> SentenceTokensCache
		{
			get
			{
				if (_sentenceTokens == null || !SentenceTokensCacheConsistent)
				{
					SentenceTokensCacheConsistent = true;
					try
					{
						BuildSentenceTokens();
					}
					catch
					{
						SentenceTokensCacheConsistent = false;
						throw;
					}
				}

				return _sentenceTokens;
			}

			set { _sentenceTokens = value; }
		}

		/// <summary>
		/// Построение множеств SYMBOLS для нетерминалов
		/// </summary>
		private void BuildSentenceTokens()
		{
			_sentenceTokens = new Dictionary<string, HashSet<string>>();

			/// Изначально множества пустые
			foreach (var nt in Rules)
			{
				_sentenceTokens[nt.Key] = new HashSet<string>();
			}

			var changed = true;

			/// Пока итеративно вносятся изменения
			while (changed)
			{
				changed = false;

				/// Проходим по всем альтернативам и пересчитываем FIRST 
				foreach (var nt in Rules)
				{
					var oldCount = _sentenceTokens[nt.Key].Count;

					foreach (var alt in nt.Value)
					{
						_sentenceTokens[nt.Key].UnionWith(SentenceTokens(alt));
					}

					if (!changed)
					{
						changed = oldCount != _sentenceTokens[nt.Key].Count;
					}
				}
			}
		}

		public HashSet<string> SentenceTokens(Alternative alt)
		{
			if (alt.Count > 0)
			{
				var symbols = new HashSet<string>();
				var elementsCounter = 0;

				for (; elementsCounter < alt.Count; ++elementsCounter)
				{
					var elemFirst = SentenceTokens(alt[elementsCounter]);
					symbols.UnionWith(elemFirst);
				}

				return symbols;
			}
			else
			{
				return new HashSet<string>() { };
			}
		}

		public HashSet<string> SentenceTokens(string symbol)
		{
			var gramSymbol = this[symbol];

			if (gramSymbol is NonterminalSymbol)
				return new HashSet<string>(SentenceTokensCache[gramSymbol.Name]);
			else
				return new HashSet<string>() { gramSymbol.Name };
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

		public string GetGrammarText()
		{
			var result = String.Empty;

			/// Выводим терминалы в порядке их описания
			foreach (var token in TokenOrder.Where(t => !String.IsNullOrEmpty(Tokens[t].Pattern)))
				result += $"{token}:\t{Tokens[token].Pattern}{Environment.NewLine}";

			result += Environment.NewLine;

			/// Выводим правила
			foreach (var rule in Rules.Where(r => !r.Key.StartsWith(AUTO_RULE_PREFIX)))
			{
				result += $"{rule.Key}\t=\t";
				for(var altIdx = 0; altIdx < rule.Value.Alternatives.Count; ++altIdx)
				{
					if (altIdx > 0)
						result += "\t| ";
					foreach(var entry in rule.Value.Alternatives[altIdx])
					{				
						if (entry.Options.NodeOption.HasValue)
							result += $"%{entry.Options.NodeOption.Value.ToString().ToLower()} ";
						if (entry.Options.IsLand)
							result += "%land ";
						if(entry.Options.Priority.HasValue)
							result += $"%priority({entry.Options.Priority.Value}) ";
						if(entry.Options.AnyOptions.Count > 0)
						{
							result += $"{Grammar.ANY_TOKEN_NAME}({String.Join(", ", entry.Options.AnyOptions.Select(kvp => $"{kvp.Key}({String.Join(", ", kvp.Value.Select(e => Userify(e)))})"))})";
						}
						else
							result += $"{Userify(entry.Symbol)} ";
                    }
					result += Environment.NewLine;
				}
			}

			result += Environment.NewLine + "%%" + Environment.NewLine;

			/// Выводим опции
			foreach (ParsingOption option in Enum.GetValues(typeof(ParsingOption)))
				if (Options.IsSet(option))
				{
					if (option == ParsingOption.START && this.Type == GrammarType.LR)
						result += $"%parsing {option.ToString().ToLower()} {Rules[StartSymbol].Alternatives[0][0]}{Environment.NewLine}";
					else
						result += $"%parsing {option.ToString().ToLower()} {String.Join(" ", Options.GetSymbols(option))}{Environment.NewLine}";
				}
			foreach (NodeOption option in Enum.GetValues(typeof(NodeOption)))
				if (Options.IsSet(option))
					result += $"%node {option.ToString().ToLower()} {String.Join(" ", Options.GetSymbols(option))}{Environment.NewLine}";
			foreach (MappingOption option in Enum.GetValues(typeof(MappingOption)))
				if (Options.IsSet(option))
					switch (option)
					{
						case MappingOption.LAND:
							result += $"%mapping {option.ToString().ToLower()} {String.Join(" ", Options.GetSymbols(option))}{Environment.NewLine}";
							break;
						case MappingOption.BASEPRIORITY:
							result += $"%mapping {option.ToString().ToLower()}({(double)Options.GetParams(option, OptionsManager.GLOBAL_PARAMETERS_SYMBOL).Single()}){Environment.NewLine}";
							break;
						case MappingOption.PRIORITY:
							foreach(var smb in Options.GetSymbols(option))
								result += $"%mapping {option.ToString().ToLower()}({(double)Options.GetParams(option, smb).Single()}) {smb}{Environment.NewLine}";
							break;
					}	

			return result;
		}
	}
}
