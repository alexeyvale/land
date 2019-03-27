using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Land.Core.Parsing.LR;

namespace Land.Core
{
	public enum GrammarState { Unknown, Valid, ForcedValid, Invalid }

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

		// Зарезервированные имена специальных нетерминальных символов
		public const string CUSTOM_BLOCK_RULE_NAME = "custom_block";

		// Префиксы и счётчики для анонимных токенов и правил
		public const string AUTO_RULE_PREFIX = "auto__";
		private int AutoRuleCounter { get; set; } = 0;
		public const string AUTO_TOKEN_PREFIX = "AUTO__";
		private int AutoTokenCounter { get; set; } = 0;

		// Для корректных сообщений об ошибках
		public Dictionary<string, string> AutoTokenUserWrittenForm = new Dictionary<string, string>();
		public Dictionary<string, string> AutoRuleUserWrittenForm = new Dictionary<string, string>();
		public Dictionary<string, ElementQuantifierPair> AutoRuleQuantifier = new Dictionary<string, ElementQuantifierPair>();
		private Dictionary<string, PointLocation> _symbolLocations = new Dictionary<string, PointLocation>();

		// Лог конструирования грамматики
		public List<string> ConstructionLog = new List<string>();

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
			ConstructionLog.Add($"var grammar = new Grammar(GrammarType.{type});");

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
			ConstructionLog.Add($"grammar.AddAliases(\"{smb}\", new HashSet<string>() {{ {String.Join(", ", aliases.Select(a => "\"" + a + "\""))} }});");

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

		public void AddLocation(string smb, PointLocation loc)
		{
			_symbolLocations[smb] = loc;

			/// Если грамматика LL и якорь устанавливается для символа, сгенерированного
			/// для некоторой сущности с квантификатором +, этот же якорь надо установить
			/// для вспомогательного символа с квантификатором *, входящего в правило для данного
			if (Type == GrammarType.LL 
				&& AutoRuleQuantifier.ContainsKey(smb) 
				&& AutoRuleQuantifier[smb].Quantifier == Quantifier.ONE_OR_MORE)
				_symbolLocations[Rules[smb].Alternatives[0][1]] = loc;
		}

		public PointLocation GetLocation(string smb)
		{
			if (_symbolLocations.ContainsKey(smb))
				return _symbolLocations[smb];
			else
				return null;
		}

		private string AlreadyDeclaredCheck(string name)
		{
			if(name == CUSTOM_BLOCK_RULE_NAME)
			{
				return $"Имя {CUSTOM_BLOCK_RULE_NAME} является зарезервированным и не может быть использовано в грамматике";
			}

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

		public void DeclareNonterminal(string name, List<Alternative> alternatives)
		{
			ConstructionLog.Add($"grammar.DeclareNonterminal(\"{name}\", new List<Alternative>() {{ {String.Join(", ", alternatives.Select(alt => GetConstructionLog(alt)))} }});");

			var rule = new NonterminalSymbol(name, alternatives);
			DeclareNonterminal(rule);
		}

		public string GenerateNonterminal(List<Alternative> alternatives)
		{
			ConstructionLog.Add($"grammar.GenerateNonterminal(new List<Alternative>() {{ {String.Join(", ", alternatives.Select(alt => GetConstructionLog(alt)))} }});");

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
			ConstructionLog.Add($"grammar.GenerateNonterminal(\"{elemName}\", Quantifier.{quantifier}, {precNonEmpty.ToString().ToLower()});");

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
								Quantifier = Quantifier.ZERO_OR_MORE
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
		public string GenerateTerminal(string pattern)
		{
			ConstructionLog.Add($"grammar.GenerateTerminal(@\"{pattern.Replace("\"", "\"\"")}\");");

			//Если оно уже сохранено с каким-то именем, не дублируем, а возвращаем это имя
			foreach (var token in Tokens.Values)
				if (token.Pattern != null && token.Pattern.Equals(pattern))
					return token.Name;

			var newName = AUTO_TOKEN_PREFIX + AutoTokenCounter++;
			Tokens.Add(newName, new TerminalSymbol(newName, pattern));
			AutoTokenUserWrittenForm[newName] = pattern;

			return newName;
		}

		public void DeclareTerminal(string name,  string pattern, bool lineStart = false)
		{
			ConstructionLog.Add($"grammar.DeclareTerminal(\"{name}\", @\"{pattern.Replace("\"", "\"\"")}\", {lineStart.ToString().ToLower()});");

			var terminal = new TerminalSymbol(name, pattern, lineStart);
			DeclareTerminal(terminal);
		}

		public void DeclarePair(string name, HashSet<string> left, HashSet<string> right)
		{
			ConstructionLog.Add($"grammar.DeclarePair(\"{name}\", new HashSet<string>(){{ {String.Join(", ", left.Select(l => $"\"{l}\""))} }}, new HashSet<string>(){{ {String.Join(", ", right.Select(r => $"\"{r}\""))} }});");

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

		private void DeclareTerminal(TerminalSymbol terminal)
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

		private void DeclareNonterminal(NonterminalSymbol rule)
		{
			var checkingResult = AlreadyDeclaredCheck(rule.Name);

			if (!String.IsNullOrEmpty(checkingResult))
				throw new IncorrectGrammarException(checkingResult);
			else
				Rules[rule.Name] = rule;

			OnGrammarUpdate();
		}

		#endregion

		#region Учёт опций

		public void SetOption(NodeOption option, params string[] symbols)
		{
			ConstructionLog.Add($"grammar.SetOption(NodeOption.{option}, new string[]{{ {String.Join(", ", symbols.Select(smb => $"\"{smb}\""))} }});");

			Options.Set(option, symbols);

			var errorSymbols = CheckIfNonterminals(symbols).Intersect(CheckIfAliases(symbols)).ToList();
			if (errorSymbols.Count > 0)
				throw new IncorrectGrammarException(
					$"Символы '{String.Join("', '", errorSymbols)}' не определены как нетерминальные или псевдонимы"
				);
		}

		public void SetOption(ParsingOption option, params string[] symbols)
		{
			ConstructionLog.Add($"grammar.SetOption(ParsingOption.{option}, new string[]{{ {String.Join(", ", symbols.Select(smb => $"\"{smb}\""))} }});");

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
					var errorSmbSkip = CheckIfTerminals(symbols);
					if (errorSmbSkip.Count > 0)
						throw new IncorrectGrammarException(
							$"Символ{(errorSmbSkip.Count > 1 ? "ы" : "")} '{String.Join("', '", errorSmbSkip)}' " +
								$"не определен{(errorSmbSkip.Count > 1 ? "ы" : "")} как терминальны{(errorSmbSkip.Count > 1 ? "е" : "й")}"
						);
					break;
				case ParsingOption.RECOVERY:
					var errorSmbRecovery= CheckIfNonterminals(symbols);
					if (errorSmbRecovery.Count > 0)
						throw new IncorrectGrammarException(
							$"Символ{(errorSmbRecovery.Count > 1 ? "ы" : "")} '{String.Join("', '", errorSmbRecovery)}' " +
								$"не определен{(errorSmbRecovery.Count > 1 ? "ы" : "")} как нетерминальны{(errorSmbRecovery.Count > 1 ? "е" : "й")}"
						);

					/// Находим множество нетерминалов, на которых возможно восстановление
					var recoverySymbols = new HashSet<string>(
						Rules.Values.Where(r => r.Alternatives
							.Any(a => a.Count > 0 && a[0].Symbol == Grammar.ANY_TOKEN_NAME)).Select(r => r.Name)
					);
					var oldCount = 0;

					while (oldCount != recoverySymbols.Count)
					{
						oldCount = recoverySymbols.Count;

						recoverySymbols.UnionWith(
							Rules.Values.Where(r => r.Alternatives
								.Any(a => a.Count > 0 && recoverySymbols.Contains(a[0].Symbol))).Select(r => r.Name)
						);
					}
					/// Проверяем, принадлежат ли к этому множеству указанные в грамматике символы
					if (Options.GetSymbols(ParsingOption.RECOVERY).Count > 0)
					{
						foreach (var smb in Options.GetSymbols(ParsingOption.RECOVERY))
						{
							if (!recoverySymbols.Contains(smb))
								throw new IncorrectGrammarException(
									$"Восстановление на символе '{smb}' невозможно, поскольку из него не выводится строка, " +
										$"начинающаяся с {Grammar.ANY_TOKEN_NAME}, или перед этим {Grammar.ANY_TOKEN_NAME} в процессе вывода стоит нетерминал"
								);
						}
					}
					else
					{
						Options.Set(ParsingOption.RECOVERY, recoverySymbols.ToArray());
					}
					break;
				default:
					break;
			}
		}

		public void SetOption(MappingOption option, params string[] symbols)
		{
			ConstructionLog.Add($"grammar.SetOption(MappingOption.{option}, new string[]{{ {String.Join(", ", symbols.Select(smb => $"\"{smb}\""))}}});");

			Options.Set(option, symbols);

			var errorSymbols = CheckIfSymbols(symbols);
			if (errorSymbols.Count > 0)
				throw new IncorrectGrammarException(
					$"Символы '{String.Join("', '", errorSymbols)}' не определены в грамматике"
				);
		}

		public void SetOption(MappingOption option, string[] symbols, params dynamic[] @params)
		{
			ConstructionLog.Add($"grammar.SetOption(MappingOption.{option}, new string[] {{ {String.Join(", ", symbols.Select(smb => $"\"{smb}\""))} }}, new dynamic[]{{ {String.Join(", ", @params.Select(param => param is string ? $"\"{param}\"" : param.ToString()))}}} );");

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

		public void SetOption(CustomBlockOption option, string[] symbols, params dynamic[] @params)
		{
			ConstructionLog.Add($"grammar.SetOption(CustomBlockOption.{option}, new string[] {{ {String.Join(", ", symbols.Select(smb => $"\"{smb}\""))} }}, new dynamic[]{{ {String.Join(", ", @params.Select(param => param is string ? $"\"{param}\"" : param.ToString()))}}} );");

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

		private List<string> CheckIfAliases(params string[] symbols)
		{
			return symbols.Where(s => !this.Aliases.Any(a => a.Value.Contains(s))).ToList();
		}

		private List<string> CheckIfSymbols(params string[] symbols)
		{
			return CheckIfNonterminals(symbols)
				.Intersect(CheckIfTerminals(symbols))
				.Intersect(CheckIfAliases(symbols)).ToList();
		}

		#endregion

		public void PostProcessing()
		{
			ConstructionLog.Add($"grammar.PostProcessing();");

			/// Для LR грамматики добавляем фиктивный стартовый символ, чтобы произошла
			/// финальная свёртка
			if (Type == GrammarType.LR)
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

			/// Игнорируем регистр в строковых литералах, исключая экранированные символы
			/// и символы Юникод в формате \uXXXX
			if(Options.IsSet(ParsingOption.IGNORECASE))
			{
				var regex = new Regex(@"'([^'\\]*|(\\\\)+|\\[^\\])*'");

				foreach (var token in Tokens.Values.Where(t => !String.IsNullOrEmpty(t.Pattern)))
				{
					var newPattern = new System.Text.StringBuilder();
					var currentPosition = 0;

					foreach(Match match in regex.Matches(token.Pattern))
					{
						newPattern.Append(token.Pattern.Substring(currentPosition, match.Index - currentPosition));

						var precededByBackslash = false;
						var stringOpened = false;
						var unicodeCodeCounter = 0;

						/// Обрезаем начальную и конечную кавычку
						foreach (var chr in match.Value.Substring(1, match.Value.Length - 2))
						{
							/// Если пропускаем код Unicode, добавляем символ
							/// без дополнительных рассуждений
							if (unicodeCodeCounter > 0)
							{
								newPattern.Append(chr);
								--unicodeCodeCounter;
							}
							else
							{
								/// не трогаем экранированные символы
								if (!precededByBackslash && Char.IsLetter(chr))
								{
									if (stringOpened)
									{
										newPattern.Append("'");
										stringOpened = false;
									}
									newPattern.Append($"[{Char.ToLower(chr)}{Char.ToUpper(chr)}]");
								}
								else
								{
									/// Встретили код \uXXXX
									if (precededByBackslash && chr == 'u')
									{
										unicodeCodeCounter = 4;
									}
									else if (!stringOpened)
									{
										newPattern.Append("'");
										stringOpened = true;
									}
									newPattern.Append(chr);
								}

								/// Учёт подряд идущих обратных слешей
								precededByBackslash = !precededByBackslash && chr == '\\';
							}
						}

						if(stringOpened)
							newPattern.Append("'");

						currentPosition = match.Index + match.Length;
					}

					token.Pattern = newPattern.Append(token.Pattern.Substring(currentPosition)).ToString();
				}
			}
		}

		#region Валидация

		public void ForceValid()
		{
			State = GrammarState.ForcedValid;
		}

		public IEnumerable<Message> CheckValidity()
		{
			var messages = new List<Message>();

			/// Задан ли стартовый символ
			if (String.IsNullOrEmpty(StartSymbol))
			{
				messages.Add(Message.Error(
					$"Не задан стартовый символ",
					null,
					"LanD"
				));
			}
			else
			{
				/// Проверяем грамматику на использование неопределённых символов
				foreach (var rule in Rules.Values)
				{
					messages.Add(Message.Trace(
						rule.ToString(),
						GetLocation(rule.Name),
						"LanD"
					));

					foreach (var alt in rule)
						foreach (var smb in alt)
						{
							if (this[smb] == null)
								messages.Add(Message.Error(
									$"Неизвестный символ {smb} в правиле для нетерминала {Userify(rule.Name)}",
									GetLocation(rule.Name),
									"LanD"
								));

							if (smb == Grammar.ANY_TOKEN_NAME)
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
												GetLocation(rule.Name),
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
												GetLocation(rule.Name),
												"LanD"
											));
								}
							}
						}
				}

				/// Если в грамматике не фигурируют неопределённые символы
				if (messages.All(m => m.Type != MessageType.Error))
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
								GetLocation(nt),
								"LanD"
							));
						}

						foreach (var nt in FindLeftRecursion().Except(emptyElementsRepetition))
						{
							messages.Add(Message.Error(
								$"Определение нетерминала {Userify(nt)} допускает левую рекурсию",
								GetLocation(nt),
								"LanD"
							));
						}
					}

					/// Также добавляем предупреждения о последовательно идущих Any
					messages.AddRange(CheckConsecutiveAny());
				}

				/// Грамматика валидна или невалидна в зависимости от наличия сообщений об ошибках
				State = messages.Any(m => m.Type == MessageType.Error) ? GrammarState.Invalid : GrammarState.Valid;
			}

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
						if (alt[i].Symbol == Grammar.ANY_TOKEN_NAME)
						{
							var newName = alt[i].Options.AnyOptions.ContainsKey(AnyOption.Except)
								? Grammar.ANY_TOKEN_NAME + AnyOption.Except.ToString() + anys.Count
								: Grammar.ANY_TOKEN_NAME + anys.Count;
							anys[newName] = new Tuple<Alternative, int>(alt, i);
							alt[i].Symbol = newName;
							Tokens.Add(newName, new TerminalSymbol(newName, String.Empty));
						}
					}
			BuildFirst();
			BuildFollow();

			/// Для каждого Any, не являющегося AnyExcept, 
			/// находим Any, которые могут идти после него 
			/// и не являются этим же самым Any
			foreach(var pair in anys.Where(kvp=>!kvp.Key.Contains(AnyOption.Except.ToString())).Select(kvp=>kvp.Value))
			{
				var nextTokens = First(pair.Item1.Subsequence(pair.Item2 + 1));
				if (nextTokens.Contains(null))
				{
					nextTokens.Remove(null);
					nextTokens.UnionWith(Follow(pair.Item1.NonterminalSymbolName));
				}

				/// Множество токенов Any, о которых надо предупредить разработчика грамматики
				var warningTokens = nextTokens.Where(t => t.StartsWith(Grammar.ANY_TOKEN_NAME) 
					&& t != pair.Item1[pair.Item2]);

				if (warningTokens.Count() > 0)
				{
					var anyUserifyRegex = $"{Grammar.ANY_TOKEN_NAME}({AnyOption.Except.ToString()})?\\d+";
					var fromAltUserified = Regex.Replace(Userify(pair.Item1), anyUserifyRegex, Grammar.ANY_TOKEN_NAME);
					var fromNontermUserified = Regex.Replace(Userify(pair.Item1.NonterminalSymbolName), anyUserifyRegex, Grammar.ANY_TOKEN_NAME);

					foreach (var token in warningTokens)
					{
						var nextAltUserified = Regex.Replace(Userify(anys[token].Item1), anyUserifyRegex, Grammar.ANY_TOKEN_NAME);
						var nextNontermUserified = Regex.Replace(Userify(anys[token].Item1.NonterminalSymbolName), anyUserifyRegex, Grammar.ANY_TOKEN_NAME);

						messages.Add(Message.Warning(
							$"После символа {Grammar.ANY_TOKEN_NAME} из альтернативы {fromAltUserified} нетерминала {fromNontermUserified} может следовать символ {Grammar.ANY_TOKEN_NAME} из альтернативы {nextAltUserified} нетерминала {nextNontermUserified}",
							GetLocation(pair.Item1.NonterminalSymbolName),
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

		#endregion

		#region Юзерификация

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

		public HashSet<string> First(List<string> sequence)
		{
			/// FIRST последовательности - это либо FIRST для первого символа,
			/// либо, если последовательность пустая, null
			if (sequence.Count > 0)
			{
				var first = new HashSet<string>();
				var elementsCounter = 0;

				/// Если первый элемент - нетерминал, из которого выводится пустая строка,
				/// нужно взять first от следующего элемента
				for (; elementsCounter < sequence.Count; ++elementsCounter)
				{
					var elemFirst = First(sequence[elementsCounter]);
					var containsEmpty = elemFirst.Remove(null);

					first.UnionWith(elemFirst);

					/// Если из текущего элемента нельзя вывести пустую строку
					/// и (для модифицированной версии First) он не равен ANY
					if (!containsEmpty
						&& (!UseModifiedFirst || sequence[elementsCounter] != ANY_TOKEN_NAME))
						break;
				}

				if (elementsCounter == sequence.Count)
					first.Add(null);

				return first;
			}
			else
			{
				return new HashSet<string>() { null };
			}
		}

		public HashSet<string> First(Alternative alt)
		{
			return First(alt.Elements.Select(e => e.Symbol).ToList());
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

		#region Замыкание пунктов и Goto

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
						case MappingOption.PRIORITY:
							foreach(var smb in Options.GetSymbols(option))
								result += $"%mapping {option.ToString().ToLower()}({(double)Options.GetParams(option, smb).Single()}) {smb}{Environment.NewLine}";
							break;
					}	

			return result;
		}

		public string GetConstructionLog(Alternative alt)
		{
			return $"{Environment.NewLine}new Alternative() {{ Alias = {(!String.IsNullOrEmpty(alt.Alias) ? $"\"{alt.Alias}\"" : "null")}, Elements = new List<Entry>() {{ {String.Join($", ", alt.Elements.Select(entry => GetConstructionLog(entry)))}}}}}";
		}

		public string GetConstructionLog(Entry entry)
		{
			return $"new Entry(\"{entry.Symbol}\", {GetConstructionLog(entry.Options)})";
		}

		public string GetConstructionLog(LocalOptions opts)
		{
			return $"new LocalOptions() {{ NodeOption = {(opts.NodeOption != null ? $"NodeOption.{opts.NodeOption}" : "null")}, Priority = {(opts.Priority != null ? $"{opts.Priority.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}" : "null")}, IsLand = {opts.IsLand.ToString().ToLower()}, AnyOptions = new Dictionary<AnyOption, HashSet<string>>() {{ {String.Join(", ", opts.AnyOptions.Select(op => $"{{AnyOption.{op.Key}, new HashSet<string>(){{{String.Join(", ", op.Value.Select(v => $"\"{v}\""))}}}}}"))} }} }}";
		}

		#region Оптимизация

		public Dictionary<int, NonterminalSymbol> RulesIdx { get; private set; } = new Dictionary<int, NonterminalSymbol>();
		public Dictionary<int, TerminalSymbol> TokensIdx { get; private set; } = new Dictionary<int, TerminalSymbol>();

		public int StartSymbolIdx { get; private set; }

		public Dictionary<string, int> SymbolToIndex { get; set; } = new Dictionary<string, int>();
		public List<string> IndexToSymbol { get; set; } = new List<string>();

		public const int EOF_TOKEN_INDEX = 10;
		public const int UNDEFINED_TOKEN_INDEX = 11;
		public const int ERROR_TOKEN_INDEX = 12;
		public const int ANY_TOKEN_INDEX = 13;

		private Dictionary<int, HashSet<int?>> FirstCacheIdx { get; set; }

		public HashSet<int?> First(List<int> sequence)
		{
			if (sequence.Count > 0)
			{
				var first = new HashSet<int?>();
				var elementsCounter = 0;

				for (; elementsCounter < sequence.Count; ++elementsCounter)
				{
					var elemFirst = First(sequence[elementsCounter]);
					var containsEmpty = elemFirst.Remove(null);

					first.UnionWith(elemFirst);

					/// Если из текущего элемента нельзя вывести пустую строку
					/// и (для модифицированной версии First) он не равен ANY
					if (!containsEmpty
						&& (!UseModifiedFirst || sequence[elementsCounter] != ANY_TOKEN_INDEX))
						break;
				}

				if (elementsCounter == sequence.Count)
					first.Add(null);

				return first;
			}
			else
			{
				return new HashSet<int?>() { null };
			}
		}

		public HashSet<int?> First(int idx)
		{
			if (RulesIdx.ContainsKey(idx))
				return new HashSet<int?>(FirstCacheIdx[idx]);
			else
				return new HashSet<int?>() { idx };
		}

		private Dictionary<int, HashSet<int>> FollowCacheIdx { get; set; }

		public HashSet<int> Follow(int ntIdx)
		{
			return FollowCacheIdx[ntIdx];
		}

		public string Userify(int idx)
		{
			return Userify(IndexToSymbol[idx]);
		}

		public bool Optimize()
		{
			if (this.State != GrammarState.Valid && this.State != GrammarState.Valid)
				return false;

			/// Резервируем первые 10 номеров для служебных целей
			IndexToSymbol.AddRange(Enumerable.Repeat<string>(null, 10));

			IndexToSymbol.Add(Grammar.EOF_TOKEN_NAME);
			IndexToSymbol.Add(Grammar.UNDEFINED_TOKEN_NAME);
			IndexToSymbol.Add(Grammar.ERROR_TOKEN_NAME);
			IndexToSymbol.Add(Grammar.ANY_TOKEN_NAME);

			SymbolToIndex.Add(Grammar.EOF_TOKEN_NAME, Grammar.EOF_TOKEN_INDEX);
			SymbolToIndex.Add(Grammar.UNDEFINED_TOKEN_NAME, Grammar.UNDEFINED_TOKEN_INDEX);
			SymbolToIndex.Add(Grammar.ERROR_TOKEN_NAME, Grammar.ERROR_TOKEN_INDEX);
			SymbolToIndex.Add(Grammar.ANY_TOKEN_NAME, Grammar.ANY_TOKEN_INDEX);

			TokensIdx[Grammar.EOF_TOKEN_INDEX] = Tokens[Grammar.EOF_TOKEN_NAME];
			TokensIdx[Grammar.UNDEFINED_TOKEN_INDEX] = Tokens[Grammar.UNDEFINED_TOKEN_NAME];
			TokensIdx[Grammar.ANY_TOKEN_INDEX] = Tokens[Grammar.ANY_TOKEN_NAME];

			/// Индексируем все токены
			foreach (var token in Tokens.Values.Where(t=> !String.IsNullOrEmpty(t.Pattern)))
			{
				token.Index = IndexToSymbol.Count;
				TokensIdx[token.Index] = token;

				SymbolToIndex.Add(token.Name, IndexToSymbol.Count);
				IndexToSymbol.Add(token.Name);
			}

			/// Индексируем все правила
			foreach (var rule in Rules.Values)
			{
				rule.Index = IndexToSymbol.Count;
				RulesIdx[rule.Index] = rule;

				SymbolToIndex.Add(rule.Name, IndexToSymbol.Count);
				IndexToSymbol.Add(rule.Name);
			}

			/// Запоминаем индекс стартового символа
			StartSymbolIdx = SymbolToIndex[StartSymbol];

			/// Подставляем индексы в альтернативы
			foreach (var rule in Rules.Values)
			{
				foreach (var alt in rule.Alternatives)
				{
					foreach (var entry in alt.Elements)
					{
						entry.Index = SymbolToIndex[entry.Symbol];
						entry.Options.Optimize(this);
					}
				}
			}

			/// Формируем индексный First
			FirstCacheIdx = new Dictionary<int, HashSet<int?>>();

			foreach(var key in FirstCache.Keys)
			{
				FirstCacheIdx[SymbolToIndex[key]] = new HashSet<int?>(
					FirstCache[key].Select(e=> e!= null ? SymbolToIndex[e] : (int?)null)
				);
			}

			/// Формируем индексный Follow
			FollowCacheIdx = new Dictionary<int, HashSet<int>>();

			foreach (var key in FollowCache.Keys)
			{
				FollowCacheIdx[SymbolToIndex[key]] = new HashSet<int>(
					FollowCache[key].Select(e => SymbolToIndex[e])
				);
			}

			return true;
		}

		#endregion
	}
}
