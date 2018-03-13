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

			Forbidden = new Dictionary<Alternative, HashSet<int>>();

			foreach (var smb in grammar.Rules)
				foreach (var alt in smb.Value.Alternatives)
					Forbidden[alt] = new HashSet<int>();

			/// Надо как-то задать интересные области и провести дополнение корнем
		}

		public void Transform()
		{
			foreach (var nt in GrammarTransformed.Rules.Values)
				foreach (var alt in nt.Alternatives)
				{
					var shift = 0;
					foreach (var range in Replace(GrammarTransformed, alt))
					{
						GrammarTransformed.Replace(alt, range.StartIndex - shift, range.Length, Grammar.TEXT_TOKEN_NAME);
						shift += range.Length - 1;
					}
				}

			///// Убираем недостижимые терминалы и нетерминалы
			////var reachableSymbols = new HashSet<string>();
			////reachableSymbols.Add(GrammarOriginal.StartSymbol);

			////int oldCount;
			////do
			////{
			////	oldCount = reachableSymbols.Count;
			////	foreach (var nt in reachableSymbols.Where(Gramm))
			////}
			////while (oldCount != reachableSymbols.Count);

			var test = GrammarTransformed.FormatTokensAndRules();
		}

		private List<Range> Replace(Grammar g, Alternative alt)
		{
			var replacements = new List<Range>();

			if (alt.Elements.Count > 0)
			{
				/// Проверяем, нужно ли отступить от начала ветки на некоторое расстояние,
				/// чтобы из части ветки перед добавляемыми Any не выводилась пустая строка
				var currentElements = alt.Subsequence(0).Elements;
				g.Replace(alt, 0, alt.Elements.Count, Grammar.TEXT_TOKEN_NAME);
				var brokeLL1 = !CheckLL1(g);
				g.Replace(alt, 0, 1, currentElements.ToArray());

				if (brokeLL1)
				{
					if (!Forbidden[alt].Contains(0))
						Forbidden[alt].Add(0);

					for (var i = 1; i < alt.Elements.Count; ++i)
					{
						if (g.First(alt.Subsequence(0, i)).Contains(null))
							Forbidden[alt].Add(i);
						else
							break;
					}
				}

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
							EndIndex = anyRange.StartIndex
						};
						foreach (var range in GetRanges(alt, leftSearchRange))
							ranges.Enqueue(range);

						var rightSearchRange = new Range()
						{
							StartIndex = anyRange.EndIndex,
							EndIndex = curRange.EndIndex
						};
						foreach (var range in GetRanges(alt, rightSearchRange))
							ranges.Enqueue(range);
					}
				}
			}

			return replacements;
		}

		private Range ReplaceInRange(Grammar g, Alternative alt, Range range)
		{
			/// Суммарная длина отступов от границ участка
			for(var skipLength = 0; skipLength < range.Length; ++ skipLength)
				/// Цикл по возможным вариантам для левой границы с учётом отступа
				for(var curLeft = range.StartIndex; curLeft <= range.StartIndex + skipLength; ++curLeft)
				{
					var curRight = curLeft + range.Length - skipLength - 1;
					var curElements = alt.Subsequence(curLeft, curRight).Elements;
					g.Replace(alt, curLeft, range.Length - skipLength, Grammar.TEXT_TOKEN_NAME);
					var brokeDefinition = !CheckDefinition(g, alt, curLeft, curRight);
					g.Replace(alt, curLeft, 1, curElements.ToArray());

					/// Если выполняется определение
					if (!brokeDefinition)
					{
						for (var i = curLeft; i <= curRight; ++i)
							Forbidden[alt].Add(i);
						if (curLeft > range.StartIndex && !Forbidden[alt].Contains(curLeft - 1))
							Forbidden[alt].Add(curLeft - 1);
						if (curRight < range.StartIndex + range.Length - 1 && !Forbidden[alt].Contains(curRight + 1))
							Forbidden[alt].Add(curRight + 1);

						return new Range()
						{
							StartIndex = curLeft,
							Length = range.Length - skipLength
						};
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

		private bool CheckDefinition(Grammar g, Alternative alt, int left, int right)
		{
			var sequenceToReplace = alt.Subsequence(left, right);
			var sequenceFollowing = alt.Subsequence(right + 1);

			/// Если множество символов, составляющих предложения, порождаемые указанным участком, 
			/// не пересекается со множеством символов, которые могут следовать сразу за этим участком
			/// в последовательностях, порождаемых остальной частью правила
			if(GrammarOriginal.SentenceTokens(sequenceToReplace).Intersect(GrammarOriginal.First(sequenceFollowing)).Count() == 0)
			{
				/// Если последовательность, порождаемая остатком правила не может быть пустой
				/// или SentenceTokens не пересекается также со множеством токенов, следующих за нетерминалом,
				/// определяемым альтернативой alt
				if (!GrammarOriginal.First(sequenceFollowing).Contains(null)
					|| GrammarOriginal.SentenceTokens(sequenceToReplace).Intersect(GrammarOriginal.Follow(alt.NonterminalSymbolName)).Count() == 0)
					return true;
			}

			return false;
		}
	}
}
