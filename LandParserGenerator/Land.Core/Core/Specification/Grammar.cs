﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Land.Core.Parsing.LR;

namespace Land.Core.Specification
{
	public enum GrammarState { Unknown, Valid, ForcedValid, Invalid }

	public enum GrammarType { LL, LR }

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

		// Стартовый символ грамматики
		public string StartSymbol => 
			Options?.GetSymbols(ParsingOption.GROUP_NAME, ParsingOption.START).FirstOrDefault();

		// Содержание грамматики
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
		public const string CUSTOM_BLOCK_START_TOKEN_NAME = "CUSTOM_BLOCK_START";
		public const string CUSTOM_BLOCK_END_TOKEN_NAME = "CUSTOM_BLOCK_END";

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
			DeclareTerminal(new TerminalSymbol(CUSTOM_BLOCK_START_TOKEN_NAME, null));
			DeclareTerminal(new TerminalSymbol(CUSTOM_BLOCK_END_TOKEN_NAME, null));

			State = GrammarState.Valid;
		}

		public void OnGrammarUpdate()
		{
			/// Если грамматика была изменена,
			/// её корректность нужно перепроверить,
			/// а множества FIRST и FOLLOW - перестроить
			State = GrammarState.Unknown;
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

		/// Добавляем к терминалам регулярное выражение, в чистом виде встреченное в грамматике
		public string GenerateTerminal(string pattern)
		{
			ConstructionLog.Add($"grammar.GenerateTerminal(@\"{pattern.Replace("\"", "\"\"")}\");");

			/// Если оно уже сохранено с каким-то именем, не дублируем, а возвращаем это имя
			var existing = GetTerminal(pattern);
			if (!String.IsNullOrEmpty(existing))
				return existing;

			var newName = AUTO_TOKEN_PREFIX + AutoTokenCounter++;
			Tokens.Add(newName, new TerminalSymbol(newName, pattern));
			AutoTokenUserWrittenForm[newName] = pattern;

			return newName;
		}

		/// Ищем уже объявленный терминал, соответствующий паттерну
		public string GetTerminal(string pattern) =>
			Tokens.Values.FirstOrDefault(t => t.Pattern == pattern)?.Name;

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

		public void SetOption(string group, string option, List<string> symbols, List<dynamic> @params)
		{
			ConstructionLog.Add($"grammar.SetOption(\"{group}\", \"{option}\", new List<string>{{ {String.Join(", ", symbols.Select(smb => $"\"{smb}\""))} }}, new List<dynamic>{{ {String.Join(", ", @params.Select(param => GetParamString(param)))} }});");

			Options.Set(group, option, symbols.ToList(), @params);

			#region Проверка корректности задания опции

			group = group.ToLower();
			option = option.ToLower();

			List<string> errorSymbols = null;

			switch (group)
			{
				case NodeOption.GROUP_NAME:
					errorSymbols = CheckIfSymbols(symbols).ToList();
					if (errorSymbols.Count > 0)
						throw new IncorrectGrammarException(
							$"Символ{(errorSymbols.Count > 1 ? "ы" : "")} '{String.Join("', '", errorSymbols)}' " +
								$"не определен{(errorSymbols.Count > 1 ? "ы" : "")}"
						);
					break;
				case ParsingOption.GROUP_NAME:
					switch (option)
					{
						case ParsingOption.START:
							if (CheckIfNonterminals(new List<string> { StartSymbol }).Count > 0)
								throw new IncorrectGrammarException(
									$"В качестве стартового указан символ '{StartSymbol}', не являющийся нетерминальным"
								);
							break;
						case ParsingOption.SKIP:
							errorSymbols = CheckIfTerminals(symbols);
							if (errorSymbols.Count > 0)
							{
								throw new IncorrectGrammarException(
									$"Символ{(errorSymbols.Count > 1 ? "ы" : "")} '{String.Join("', '", errorSymbols)}' " +
										$"не определен{(errorSymbols.Count > 1 ? "ы" : "")} как терминальны{(errorSymbols.Count > 1 ? "е" : "й")}"
								);
							}

							errorSymbols = symbols
								.Where(s => Pairs.Values.Any(e => e.Left.Contains(s) || e.Right.Contains(s)))
								.ToList();
							if(errorSymbols.Count > 0)
							{
								throw new IncorrectGrammarException(
									$"Символ{(errorSymbols.Count > 1 ? "ы" : "")} '{String.Join("', '", errorSymbols)}' " +
										$"участву{(errorSymbols.Count > 1 ? "ют" : "ет")} в определении одной или нескольких пар"
								);
							}
							break;
						case ParsingOption.RECOVERY:
							errorSymbols = CheckIfNonterminals(symbols);
							if (errorSymbols.Count > 0)
								throw new IncorrectGrammarException(
									$"Символ{(errorSymbols.Count > 1 ? "ы" : "")} '{String.Join("', '", errorSymbols)}' " +
										$"не определен{(errorSymbols.Count > 1 ? "ы" : "")} как нетерминальны{(errorSymbols.Count > 1 ? "е" : "й")}"
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

							/// Если при установке опции переданы конкретные символы, проверяем, возможно ли на них восстановление
							if (Options.GetSymbols(ParsingOption.GROUP_NAME, ParsingOption.RECOVERY).Count > 0)
							{
								foreach (var smb in Options.GetSymbols(ParsingOption.GROUP_NAME, ParsingOption.RECOVERY))
								{
									if (!recoverySymbols.Contains(smb))
										throw new IncorrectGrammarException(
											$"Восстановление на символе '{smb}' невозможно, поскольку из него не выводится строка, " +
												$"начинающаяся с {Grammar.ANY_TOKEN_NAME}, или перед этим {Grammar.ANY_TOKEN_NAME} в процессе вывода стоит нетерминал"
										);
								}
							}
							/// Иначе считаем, что восстановление разрешено везде, где оно возможно
							else
							{
								Options.Set(ParsingOption.GROUP_NAME, ParsingOption.RECOVERY, recoverySymbols.ToList(), null);
							}
							break;
						default:
							break;
					}
					break;
			}

			#endregion
		}

		private string GetParamString(dynamic param) =>
			param is string ? $"\"{param}\"" : param is double ?
				param.ToString().Replace(',', '.') : param.ToString();

		private List<string> CheckIfNonterminals(List<string> symbols)
		{
			return symbols.Where(s => !this.Rules.ContainsKey(s)).ToList();
		}

		private List<string> CheckIfTerminals(List<string> symbols)
		{
			return symbols.Where(s => !this.Tokens.ContainsKey(s)).ToList();
		}

		private List<string> CheckIfAliases(List<string> symbols)
		{
			return symbols.Where(s => !this.Aliases.Any(a => a.Value.Contains(s))).ToList();
		}

		private List<string> CheckIfSymbols(List<string> symbols)
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

					this.Options.Clear(ParsingOption.GROUP_NAME, ParsingOption.START);
					Options.Set(ParsingOption.GROUP_NAME, ParsingOption.START, new List<string> { newStartName }, null);
				}
			}

			/// Игнорируем регистр в строковых литералах, исключая экранированные символы
			/// и символы Юникод в формате \uXXXX
			if(Options.IsSet(ParsingOption.GROUP_NAME, ParsingOption.IGNORECASE))
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
									$"Неизвестный символ {smb} в правиле для нетерминала {Developerify(rule.Name)}",
									GetLocation(rule.Name),
									"LanD"
								));

							if (smb == Grammar.ANY_TOKEN_NAME)
							{
								var union = new HashSet<string>();

								/// Проверяем, что в качестве аргументов для Any не указаны
								/// имена неизвестных символов
								foreach (var kvp in smb.Arguments.AnyArguments)
								{
									union.UnionWith(kvp.Value);
									foreach (var arg in kvp.Value)
									{
										if (this[arg] == null)
											messages.Add(Message.Error(
												$"Неизвестный символ {Developerify(arg)} в аргументах опции {kvp.Key} символа {Grammar.ANY_TOKEN_NAME} для нетерминала {Developerify(rule.Name)}",
												GetLocation(rule.Name),
												"LanD"
											));
									}
								}

								/// Проверяем, что не существует символов, указанных
								/// сразу в нескольких опциях
								if (union.Count < smb.Arguments.AnyArguments.Sum(o => o.Value.Count))
								{
									messages.Add(Message.Error(
												$"Множества аргументов нескольких опций символа {Grammar.ANY_TOKEN_NAME} для нетерминала {Developerify(rule.Name)} пересекаются",
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
						var firstBuilder = new FirstBuilder(this, false);

						var emptyElementsRepetition = AutoRuleQuantifier.Where(kvp =>
							(kvp.Value.Quantifier == Quantifier.ZERO_OR_MORE || kvp.Value.Quantifier == Quantifier.ONE_OR_MORE)
							&& firstBuilder.First(kvp.Value.Element).Contains(null)).Select(kvp => kvp.Key).ToList();

						foreach (var nt in emptyElementsRepetition)
						{
							messages.Add(Message.Error(
								$"Определение нетерминала {Developerify(nt)} допускает левую рекурсию: в списке допустимо бесконечное количество пустых элементов",
								GetLocation(nt),
								"LanD"
							));
						}

						foreach (var nt in FindLeftRecursion(firstBuilder).Except(emptyElementsRepetition))
						{
							messages.Add(Message.Error(
								$"Определение нетерминала {Developerify(nt)} допускает левую рекурсию",
								GetLocation(nt),
								"LanD"
							));
						}
					}

					if (Rules.ContainsKey(StartSymbol))
					{
						/// Также добавляем предупреждения о последовательно идущих Any
						messages.AddRange(CheckConsecutiveAny());
					}
				}

				/// Проверяем корректность задания локальных опций
				messages.AddRange(LocalOptionsCheck());

				/// Грамматика валидна или невалидна в зависимости от наличия сообщений об ошибках
				State = messages.Any(m => m.Type == MessageType.Error) 
					? GrammarState.Invalid 
					: GrammarState.Valid;
			}

			return messages;
		}

		private List<Message> LocalOptionsCheck()
		{
			var builtInGroups = new HashSet<string> { ParsingOption.GROUP_NAME, NodeOption.GROUP_NAME };
			var result = new List<Message>();

			foreach (var rule in Rules.Values)
			{
				foreach (var alt in rule)
				{
					foreach (var entry in alt)
					{
						var badGroups = entry.Options.GetGroups().Except(builtInGroups).ToList();

						if (badGroups.Any())
						{
							result.Add(Message.Error(
								$"В правиле для нетерминала {Developerify(rule.Name)} присутствуют опции из категорий, локальное использование которых не допускается: {String.Join(", ", badGroups)}",
								GetLocation(rule.Name),
								"LanD"
							));
						}
					}
				}
			}

			return result;
		}

		/// Возвращает леворекурсивно определённые нетерминалы
		private List<string> FindLeftRecursion(FirstBuilder firstBuilder)
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

						if (!firstBuilder.First(altStartingNonterminals[i]).Contains(null))
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
							var newName = alt[i].Arguments.AnyArguments.ContainsKey(AnyArgument.Except)
								? Grammar.ANY_TOKEN_NAME + AnyArgument.Except.ToString() + anys.Count
								: Grammar.ANY_TOKEN_NAME + anys.Count;
							anys[newName] = new Tuple<Alternative, int>(alt, i);
							alt[i].Symbol = newName;
							Tokens.Add(newName, new TerminalSymbol(newName, String.Empty));
						}
					}

			var firstBuilder = new FirstBuilder(this, false);
			var followBuilder = new FollowBuilder(this, firstBuilder);

			/// Для каждого Any, не являющегося AnyExcept, 
			/// находим Any, которые могут идти после него 
			/// и не являются этим же самым Any
			foreach(var pair in anys.Where(kvp=>!kvp.Key.Contains(AnyArgument.Except.ToString())).Select(kvp=>kvp.Value))
			{
				var nextTokens = firstBuilder.First(pair.Item1.Subsequence(pair.Item2 + 1));
				if (nextTokens.Contains(null))
				{
					nextTokens.Remove(null);
					nextTokens.UnionWith(followBuilder.Follow(pair.Item1.NonterminalSymbolName));
				}

				/// Множество токенов Any, о которых надо предупредить разработчика грамматики
				var warningTokens = nextTokens.Where(t => t.StartsWith(Grammar.ANY_TOKEN_NAME) 
					&& t != pair.Item1[pair.Item2]);

				if (warningTokens.Count() > 0)
				{
					var anyUserifyRegex = $"{Grammar.ANY_TOKEN_NAME}({AnyArgument.Except.ToString()})?\\d+";
					var fromAltUserified = Regex.Replace(Developerify(pair.Item1), anyUserifyRegex, Grammar.ANY_TOKEN_NAME);
					var fromNontermUserified = Regex.Replace(Developerify(pair.Item1.NonterminalSymbolName), anyUserifyRegex, Grammar.ANY_TOKEN_NAME);

					foreach (var token in warningTokens)
					{
						var nextAltUserified = Regex.Replace(Developerify(anys[token].Item1), anyUserifyRegex, Grammar.ANY_TOKEN_NAME);
						var nextNontermUserified = Regex.Replace(Developerify(anys[token].Item1.NonterminalSymbolName), anyUserifyRegex, Grammar.ANY_TOKEN_NAME);

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
				AutoRuleUserWrittenForm[smb] = Developerify(smb);
		}

		public string Developerify(string name)
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
							return Developerify(elementName) + "+";
						case Quantifier.ZERO_OR_MORE:
							return Developerify(elementName) + "*";
						case Quantifier.ZERO_OR_ONE:					
							return Developerify(elementName) + "?";
					}
				}
				else
				{
					return $"({String.Join(" | ", Rules[name].Alternatives.Select(a=>Developerify(a)))})";
                }
			}

			return AutoTokenUserWrittenForm.ContainsKey(name) ? AutoTokenUserWrittenForm[name] : name;
		}

		public string Developerify(Entry entry)
		{
			if (entry.Arguments.AnyArguments.Count > 0)
				return $"{Grammar.ANY_TOKEN_NAME}({String.Join(", ", entry.Arguments.AnyArguments.Select(kvp => $"{kvp.Key}({String.Join(", ", kvp.Value.Select(e => Developerify(e)))})"))})";
			else
				return Developerify(entry.Symbol);
		}

		public string Developerify(ISymbol smb)
		{
			return Developerify(smb.Name);
		}

		public string Developerify(Alternative alt)
		{
			/// Если альтернатива пустая, показываем пользователю эпсилон
			return alt.Elements.Count > 0 ? String.Join(" ", DeveloperifyElementwise(alt)) : "\u03B5";
		}

		public List<string> DeveloperifyElementwise(Alternative alt)
		{
			return alt.Elements.Select(e => Developerify(e)).ToList();
		}

		public string Userify(string name)
		{
			return this.Options.IsSet(ParsingOption.GROUP_NAME, ParsingOption.USERIFY, name)
				? $"{this.Options.GetParams(ParsingOption.GROUP_NAME, ParsingOption.USERIFY, name)[0]}"
				: Developerify(name);
		}

		#endregion

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
						/// Выводим локальные опции
						foreach(var group in entry.Options.GetGroups())
						{
							/// Параметры текущей опции
							List<dynamic> parameters;
							/// Текст для группы опций
							var groupText = String.Join(", ", entry.Options.GetOptions(group).Select(o => 
								$"{o}{((parameters = entry.Options.GetParams(group, o)).Count > 0 ? $"({String.Join(", ", parameters)})" : "")}"
							));
							/// Добавляем текст группы к основному тексту
							result += $"%{group}({groupText}) ";
						}
						/// Если текущий символ - это параметризованный Any, выводим его параметры
						if(entry.Arguments.AnyArguments.Count > 0)
						{
							result += $"{Grammar.ANY_TOKEN_NAME}({String.Join(", ", entry.Arguments.AnyArguments.Select(kvp => $"{kvp.Key}({String.Join(", ", kvp.Value.Select(e => Developerify(e)))})"))}) ";
						}
						else
							result += $"{Developerify(entry.Symbol)} ";
                    }
					result += Environment.NewLine;
				}
			}

			result += Environment.NewLine + "%%" + Environment.NewLine;

			/// Выводим опции
			foreach(var smb in Options.GetSymbols())
			{
				var smbManager = Options.GetOptions(smb);

				foreach(var group in smbManager.GetGroups())
					foreach(var option in smbManager.GetOptions(group))
					{
						if (this.Type == GrammarType.LR &&
							group.Equals(ParsingOption.GROUP_NAME.ToString(), StringComparison.CurrentCultureIgnoreCase) &&
							option.Equals(ParsingOption.START.ToString(), StringComparison.CurrentCultureIgnoreCase))
						{
							result += $"%{group} {option} {Rules[StartSymbol].Alternatives[0][0]}{Environment.NewLine}";
						}
						else
						{
							List<dynamic> parameters;
							result += $"%{group} {option}{((parameters = Options.GetParams(group, option, smb)).Count > 0 ? "(" + String.Join(", ", parameters.Select(p => p.ToString())) + ")" : "")} {smb}{Environment.NewLine}";
						}
					}
			}

			return result;
		}

		public string GetConstructionLog(Alternative alt)
		{
			return $"{Environment.NewLine}new Alternative() {{ Alias = {(!String.IsNullOrEmpty(alt.Alias) ? $"\"{alt.Alias}\"" : "null")}, Elements = new List<Entry>() {{ {String.Join($", ", alt.Elements.Select(entry => GetConstructionLog(entry)))}}}}}";
		}

		public string GetConstructionLog(Entry entry)
		{
			return $"new Entry(\"{entry.Symbol}\", {GetConstructionLog(entry.Options)}, {GetConstructionLog(entry.Arguments)})";
		}

		public string GetConstructionLog(SymbolArguments args)
		{
			return $"new SymbolArguments() {{ AnyArguments = new Dictionary<AnyArgument, HashSet<string>>{{ {String.Join(", ", args.AnyArguments.Select(op => $"{{AnyArgument.{op.Key}, new HashSet<string>(){{{String.Join(", ", op.Value.Select(v => $"\"{v}\""))}}}}}"))} }} }} ";
		}

		public string GetConstructionLog(SymbolOptionsManager opts)
		{
			return $"new SymbolOptionsManager(new Dictionary<string, Dictionary<string, List<dynamic>>>{{{String.Join(", ", opts.CloneRaw().Select(g=>$"{{\"{g.Key}\", new Dictionary<string, List<dynamic>>{{{String.Join(", ", g.Value.Select(o=>$"{{\"{o.Key}\", new List<dynamic>{{{String.Join(", ", o.Value.Select(p => GetParamString(p)))}}}}}"))}}}}}"))}}})";
		}
	}
}
