using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
	public class Range
	{
		public int StartIndex { get; set; }

		private int _length;
		public int Length
		{
			get { return _length; }

			set
			{
				_length = value;
				_endIndex = StartIndex + _length - 1;
			}
		}

		private int _endIndex;
		public int EndIndex
		{
			get { return _endIndex; }

			set
			{
				_endIndex = value;
				_length = _endIndex - StartIndex + 1;
			}
		}
	}

	public class Transformer
	{
		private Grammar GrammarOriginal { get; set; }
		private Grammar GrammarTransformed { get; set; }

		private Dictionary<Alternative, HashSet<int>>  Forbidden { get; set; }

		public Transformer(Grammar grammar)
		{
			GrammarTransformed = grammar;

			/// Частично копируем грамматику, чтобы на основе исходной версии грамматики
			/// проверять замены на соответствие определению
			GrammarOriginal = new Grammar(grammar.Type);
			foreach (var tk in grammar.Tokens.Where(t=>t.Key!=Grammar.EOF_TOKEN_NAME && t.Key!=Grammar.TEXT_TOKEN_NAME))
				GrammarOriginal.DeclareTerminal(tk.Key, tk.Value.Pattern);
			foreach (var nt in grammar.Rules)
				GrammarOriginal.DeclareNonterminal(nt.Key, nt.Value.Alternatives.Select(a => new Alternative()
				{
					NonterminalSymbolName = a.NonterminalSymbolName,
					Elements = a.Elements.Select(e => new Entry(e.Symbol)).ToList()
				}).ToList());
			GrammarOriginal.SetOption(ParsingOption.START, grammar.StartSymbol);

			/// Запрещаем заменять символы, отмеченные как land, а также символы,
			/// необходимые для их достижения
			var forbiddenSymbols = grammar.Options.GetSymbols(MappingOption.LAND);
			var oldCount = 0;

			while (oldCount != forbiddenSymbols.Count)
			{
				oldCount = forbiddenSymbols.Count;
				foreach (var smb in grammar.Rules)
				{
					foreach (var alt in smb.Value.Alternatives)
						if (alt.Elements.Any(e=>forbiddenSymbols.Contains(e) || e.Options.IsLand))
						{
							forbiddenSymbols.Add(alt.NonterminalSymbolName);
							break;
						}
				}
			}

			Forbidden = new Dictionary<Alternative, HashSet<int>>();

			/// Отмечаем все вхождения запрещённых символов
			foreach (var smb in grammar.Rules)
				foreach (var alt in smb.Value.Alternatives)
				{
					Forbidden[alt] = new HashSet<int>();
					for (var i = 0; i < alt.Count; ++i)
						if (forbiddenSymbols.Contains(alt[i]) || alt[i].Options.IsLand)
							Forbidden[alt].Add(i);
				}
		}

		public Grammar Transform()
		{
			/// Проходим по всем нетерминалам, наиная со стартового символа,
			/// и пытаемся заменить на Any части их продукций
			var reachableSymbols = new HashSet<string>();
			var queue = new Queue<string>();

			reachableSymbols.Add(GrammarTransformed.StartSymbol);
			queue.Enqueue(GrammarTransformed.StartSymbol);

			while (queue.Count > 0)
			{
				var currentNonterminal = queue.Dequeue();

				foreach (var alt in GrammarTransformed.Rules[currentNonterminal])
				{
					var shift = 0;
					foreach (var range in Replace(GrammarTransformed, alt))
					{
						var entry = new Entry(Grammar.TEXT_TOKEN_NAME);
						entry.Options.AnySyncTokens = range.Item2;

						GrammarTransformed.Replace(alt, range.Item1.StartIndex - shift, range.Item1.Length, entry);
						shift += range.Item1.Length - 1;
					}

					/// Проходим по преобразованной альтернативе и добавляем в очередь
					/// оставшиеся в ней ранее не исследованные нетерминалы
					foreach(var smb in alt)
					{
						/// Заодно формируем множество достижимых символов грамматики
						if (GrammarTransformed[smb] is NonterminalSymbol)
						{
							if (!reachableSymbols.Contains(smb))
							{
								queue.Enqueue(smb);
								reachableSymbols.Add(smb);
							}
						}
						else
							reachableSymbols.Add(smb);
					}
				}

				/// Эвристика: если у текущего символа есть пустая альтернатива и альтернатива, 
				/// порождающая только Any, можно выкинуть пустую ветку;
				/// если получилось несколько альтернатив, состоящих только из Any, можно схлопнуть их в одну
				var grammarNonterminal = GrammarTransformed.Rules[currentNonterminal];
				var hasPureAny = false;
				var emptyAlternativeIdx = (int?)null;

				for(var i=0; i<grammarNonterminal.Alternatives.Count; ++i)
				{
					/// Если альтернатива состоит из одного Any
					if (grammarNonterminal[i].Count == 1 && grammarNonterminal[i][0].Symbol == Grammar.TEXT_TOKEN_NAME)
					{
						/// и это не первая такая альтернатива
						if (hasPureAny)
						{
							grammarNonterminal.Alternatives.RemoveAt(i);
							--i;
						}
						else
							hasPureAny = true;
					}
					else
					{
						if (grammarNonterminal[i].Count == 0)
							emptyAlternativeIdx = i;
					}
				}

				if (hasPureAny && emptyAlternativeIdx.HasValue)
					grammarNonterminal.Alternatives.RemoveAt(emptyAlternativeIdx.Value);
			}

			/// Формируем множества недостижимых токенов и нетерминалов
			var unreachableTokens = new HashSet<string>(GrammarTransformed.Tokens.Keys).Except(reachableSymbols);
			var unreachableRules = new HashSet<string>(GrammarTransformed.Rules.Keys).Except(reachableSymbols);

			/// Все недостижимые токены, не указанные в skip и не являющиеся специальными, убираем
			foreach(var token in unreachableTokens.Where(t=>
				t!=Grammar.TEXT_TOKEN_NAME 
				&& t != Grammar.EOF_TOKEN_NAME 
				&& !GrammarTransformed.Options.GetSymbols(ParsingOption.SKIP).Contains(t))
			)
			{
				GrammarTransformed.TokenOrder.Remove(token);
				GrammarTransformed.Tokens.Remove(token);
			}

			/// Также удаляем недостижимые правила
			foreach (var nonterm in unreachableRules)
			{
				GrammarTransformed.Rules.Remove(nonterm);
			}

			GrammarTransformed.OnGrammarUpdate();

			return GrammarTransformed;
		}

		private List<Tuple<Range, HashSet<string>>> Replace(Grammar g, Alternative alt)
		{
			var replacements = new List<Tuple<Range, HashSet<string>>>();

			if (alt.Elements.Count > 0)
			{
				/// Формируем набор диапазонов, которые можно попробовать заменить на Any
				var ranges = new Queue<Range>(GetRanges(alt));

				while (ranges.Count > 0)
				{
					var curRange = ranges.Dequeue();
					var anyRange = ReplaceInRange(g, alt, curRange);

					/// Если в текущем диапазоне замену произвести удалось, он разбивается на два поддиапазона
					if (anyRange != null)
					{
						replacements.Add(anyRange);

						var leftSearchRange = new Range()
						{
							StartIndex = curRange.StartIndex,
							EndIndex = anyRange.Item1.StartIndex
						};
						foreach (var range in GetRanges(alt, leftSearchRange))
							ranges.Enqueue(range);

						var rightSearchRange = new Range()
						{
							StartIndex = anyRange.Item1.EndIndex,
							EndIndex = curRange.EndIndex
						};
						foreach (var range in GetRanges(alt, rightSearchRange))
							ranges.Enqueue(range);
					}
				}
			}

			return replacements;
		}

		private Tuple<Range, HashSet<string>> ReplaceInRange(Grammar g, Alternative alt, Range range)
		{
			/// Суммарная длина отступов от границ участка
			for(var skipLength = 0; skipLength < range.Length; ++ skipLength)
				/// Цикл по возможным вариантам для левой границы с учётом отступа
				for(var curLeft = range.StartIndex; curLeft <= range.StartIndex + skipLength; ++curLeft)
				{
					var curRight = curLeft + range.Length - skipLength - 1;

					/// Не заменяем на ANY одиночный терминальный символ
					if (curRight == curLeft && GrammarOriginal[alt[curLeft]] is TerminalSymbol)
						continue;

					var curElements = alt.Subsequence(curLeft, curRight).Elements;
					HashSet<string> syncSet;

					g.Replace(alt, curLeft, range.Length - skipLength, Grammar.TEXT_TOKEN_NAME);
					var brokeDefinition = !CheckDefinition(g, alt, curLeft, curRight, out syncSet);
					g.Replace(alt, curLeft, 1, curElements.ToArray());

					/// Если выполняется определение
					if (!brokeDefinition)
					{
						for (var i = curLeft; i <= curRight; ++i)
							Forbidden[alt].Add(i);

						return new Tuple<Range, HashSet<string>>(
							new Range()
							{
								StartIndex = curLeft,
								Length = range.Length - skipLength
							}, syncSet);
					}
				}
			return null;
		}

		private List<Range> GetRanges(Alternative alt, Range searchRange = null)
		{
			var left = searchRange != null ? Math.Max(0, searchRange.StartIndex) : 0;
			var right = searchRange != null ? Math.Min(alt.Elements.Count - 1, searchRange.EndIndex) : alt.Elements.Count - 1;

			var ranges = new List<Range>();
			var leftIdx = -1;

			/// Идём по указанному участку в поиске фрагментов, которые можно попытаться заменить на Any
			for (var i = left; i <= right; ++i)
			{
				if (!Forbidden[alt].Contains(i))
				{
					if (leftIdx == -1)
						leftIdx = i;
				}
				else
				{
					if (leftIdx != -1)
					{
						ranges.Add(new Range()
						{
							StartIndex = leftIdx,
							EndIndex = i - 1
						});
						leftIdx = -1;
					}
				}
			}

			if (leftIdx != -1)
			{
				ranges.Add(new Range()
				{
					StartIndex = leftIdx,
					EndIndex = right
				});
				leftIdx = -1;
			}

			return ranges;
		}

		private bool CheckLL1(Grammar g)
		{
			/// Проверяем, не нарушает ли где-нибудь символ Any свойства LL(1)
			foreach(var nt in g.Rules.Values)
			{
				var firsts = nt.Alternatives.Select(a => g.First(a)).ToList();

				/// Нет ли у одного нетерминала двух веток, которые могут начинаться с Any
				if (firsts.Where(f => f.Contains(Grammar.TEXT_TOKEN_NAME)).Count() > 1)
					return false;
				/// Нет ли ветки, из которой выводится пустая строка, ветки, начинающейся с Any и Any в Follow
				if (firsts.Any(f => f.Contains(null)) && firsts.Any(f => f.Contains(Grammar.TEXT_TOKEN_NAME)) && g.Follow(nt.Name).Contains(Grammar.TEXT_TOKEN_NAME))
					return false;
			}

			return true;
		}

		private bool CheckDefinition(Grammar g, Alternative alt, int left, int right, out HashSet<string> syncSet)
		{
			var sequenceToReplace = alt.Subsequence(left, right);
			var sequenceFollowing = alt.Subsequence(right + 1);

			var tmpSyncSet = new HashSet<string>(GrammarOriginal.First(sequenceFollowing));
			if (tmpSyncSet.Contains(null))
				tmpSyncSet.UnionWith(GrammarOriginal.Follow(alt.NonterminalSymbolName));

			/// Если множество символов, составляющих предложения, порождаемые указанным участком, 
			/// не пересекается со множеством символов, которые могут следовать сразу за этим участком
			/// в последовательностях, порождаемых остальной частью правила;
			/// а также если последовательность, порождаемая остатком правила может быть пустой
			/// или SentenceTokens не пересекается также со множеством токенов, следующих за нетерминалом,
			/// определяемым альтернативой alt
			if (GrammarOriginal.SentenceTokens(sequenceToReplace).Intersect(tmpSyncSet).Count() == 0)
			{
				syncSet = tmpSyncSet;
				return true;
			}

			syncSet = null;
			return false;
		}
	}
}
