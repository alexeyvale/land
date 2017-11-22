using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing.LL
{
	/// <summary>
	/// Таблица LL(1) парсинга
	/// </summary>
	public class TableLL1_ver1
	{
		private HashSet<Alternative>[,] Table { get; set; }
		private Dictionary<string, int> Lookaheads { get; set; }
		private Dictionary<string, int> NonterminalSymbols { get; set; }

        private Dictionary<string, HashSet<string>> AnyFirsts { get; set; } = new Dictionary<string, HashSet<string>>();
        private Dictionary<string, HashSet<string>> AnyLasts { get; set; } = new Dictionary<string, HashSet<string>>();

        public TableLL1_ver1(Grammar g)
		{
            /// Подготовительые мероприятия, направленные на то, 
            /// чтобы в нашей грамматике не осталось символов TEXT
            ReplaceAny(g);
            FindAnyFirstCompetitors(g);
            FindAnyStopTokens(g);
            ReplaceAnyFirstAndLast(g);

            Table = new HashSet<Alternative>[g.Rules.Count, g.Tokens.Count];

			NonterminalSymbols = g.Rules
				.Zip(Enumerable.Range(0, g.Rules.Count), (a, b) => new { smb = a.Key, idx = b })
				.ToDictionary(e => e.smb, e => e.idx);
			Lookaheads = g.Tokens.Keys
				.Zip(Enumerable.Range(0, g.Tokens.Count), (a, b) => new { smb = a, idx = b })
				.ToDictionary(e => e.smb, e => e.idx);

			foreach (var nt in g.Rules.Keys)
			{
				foreach (var tk in g.Tokens)
				{
					/// Список, потому что могут быть неоднозначности
					this[nt, tk.Key] = new HashSet<Alternative>();
				}

				/// Проходим по всем продукциям
				foreach (var alt in g.Rules[nt])
				{
					var altFirst = g.First(alt);

					var altContainsEmpty = altFirst.Remove(null);

					/// Для каждого реально возвращаемого лексером токена, 
                    /// с которого может начинаться альтернатива
					foreach (var tk in altFirst.Where(t=>!g.SpecialTokens.Contains(t)))
					{
						/// добавляем эту альтернативу в соответствующую ячейку таблицы
						this[nt, tk].Add(alt);
					}

					/// Если альтернатива может быть пустой
					if (altContainsEmpty)
					{
						var ntFollow = g.Follow(nt);

						/// её следует выбрать, если встретили то, что может идти
						/// после текущего нетерминала
						foreach (var tk in ntFollow.Where(t => !g.SpecialTokens.Contains(t)))
						{
							this[nt, tk].Add(alt);
						}
					}
				}
			}

            ExportToCsv("current_table.csv");
		}

		public HashSet<Alternative> this[string nt, string lookahead]
		{
			get { return Table[NonterminalSymbols[nt], Lookaheads[lookahead]]; }

			private set { Table[NonterminalSymbols[nt], Lookaheads[lookahead]] = value; }
		}

		public Dictionary<string, HashSet<Alternative>> this[string nt]
		{
			get
			{
				var allRecords = new Dictionary<string, HashSet<Alternative>>();

				foreach (var lookahead in Lookaheads.Keys)
					allRecords[lookahead] = this[nt, lookahead];

				return allRecords;
			}
		}

		public void ExportToCsv(string filename)
		{
			var output = new StreamWriter(filename);

			var orderedLookaheads = Lookaheads.OrderBy(l => l.Value);
			output.WriteLine("," + String.Join(",", orderedLookaheads.Select(l => l.Key)));

			foreach (var nt in NonterminalSymbols.Keys)
			{
				output.Write($"{nt},");

				output.Write(String.Join(",",
					orderedLookaheads.Select(l=>this[nt, l.Key])
					.Select(alts => alts.Count == 0 ? "" : alts.Count == 1 ? alts.Single().ToString() : String.Join("/", alts))));

				output.WriteLine();
			}

			output.Close();
		}

        #region Any replacement

        /// <summary>
        /// Замена символов TEXT на индивидуальные нетерминалы
        /// </summary>
        private void ReplaceAny(Grammar g)
        {
            var anyCounter = 0;

            var newNonterminals = new LinkedList<NonterminalSymbol>();

            /// Проходим по всем продукциям
            foreach(var rule in g.Rules.Values)
                foreach(var alt in rule)
                {
                    /// Пока в ветке есть ANY
                    while(alt.Contains(Grammar.TEXT_TOKEN_NAME))
                    {
                        /// Вводим новый нетерминал и заменяем на него вхождение ANY
                        newNonterminals.AddLast(new NonterminalSymbol($"any{anyCounter}", new string[][]
                        {
                            new string[]{ $"any{anyCounter}_first", $"any{anyCounter}_tail" },
                            new string[]{ },
                        }));
                        newNonterminals.AddLast(new NonterminalSymbol($"any{anyCounter}_tail", new string[][]
                        {
                            new string[]{ $"any{anyCounter}_last", $"any{anyCounter}_tail" },
                            new string[]{ },
                        }));

                        g.DeclareSpecialTokens($"any{anyCounter}_first", $"any{anyCounter}_last");

                        /// Сразу исключаем из ANY признак конца файла
                        AnyFirsts[$"any{anyCounter}_first"] = new HashSet<string>() { Grammar.EOF_TOKEN_NAME };
                        AnyLasts[$"any{anyCounter}_last"] = new HashSet<string>() { Grammar.EOF_TOKEN_NAME };

                        alt.ReplaceFirst(Grammar.TEXT_TOKEN_NAME, $"any{anyCounter++}");
                    }
                }
            
            foreach(var nt in newNonterminals)
            {
                g.Rules[nt.Name] = nt;
            }
        }

        private void FindAnyFirstCompetitors(Grammar g)
        {
            foreach (var nt in g.Rules.Values.Where(r => !r.Name.StartsWith("any")))
                foreach (var alt in nt)
                {
                    /// Если после некоторого элемента ветки может быть any,
                    /// а из самого этого элемента можно вывести пустую строку,
                    /// нужно убрать конкуренцию any и этого элемента
                    
                    /// Находим элементы, из которых можно вывести пустую строку
                    var nullableIndices = alt.Elements
                        .Zip(Enumerable.Range(0, alt.Count), (elem, idx) => new { elem, idx })
                        .Where(e => g.First(e.elem).Contains(null))
                        .Select(e => e.idx);

                    /// Проходим по каждому из них и смотрим,
                    /// может ли там быть any
                    foreach (var idx in nullableIndices)
                    {
                        var firstAfterIdx = g.First(alt.Subsequence(idx + 1));
                        foreach(var any in firstAfterIdx.Where(s=>s!=null && s.StartsWith("any")))
                        {
                            AnyFirsts[any].UnionWith(g.First(alt[idx]));
                        }
                    }       

                    var altFirst = g.First(alt);

                    if (altFirst.Any(s=> s != null && s.StartsWith("any")))
                    {
                        var anyName = altFirst.Where(s => s != null
                            && s.StartsWith("any")).Single();

                        /// Если any входит в first от текущей ветки,
                        /// оно не должно конкурироватьс началами других веток
                        AnyFirsts[anyName].UnionWith(g.First(nt.Name).Except(altFirst));

                        /// также, если есть пустая ветка, any не должно конкурировать
                        /// со множеством follow
                        if (nt.Alternatives.Any(a => g.First(a).Contains(null)))
                        {
                            AnyFirsts[anyName].UnionWith(g.Follow(nt.Name));
                        }
                    }
                }

            var anySets = new Dictionary<string, HashSet<string>>[] { AnyFirsts, AnyLasts };
            foreach (var set in anySets)
            {
                foreach (var val in set.Values)
                    val.Remove(null);
            }  
        }

        private void FindAnyStopTokens(Grammar g)
        {
            foreach (var nt in g.Rules.Values.Where(r => !r.Name.StartsWith("any")))
                foreach (var alt in nt)
                    for (var i = 0; i < alt.Elements.Count; ++i)
                        if (alt.Elements[i].Value.StartsWith("any"))
                        {
                            var anyPrefix = alt.Elements[i].Value;
                            var firstAfterAny = g.First(alt.Subsequence(i + 1));
                            var mayBeEmpty = firstAfterAny.Remove(null);

                            AnyFirsts[$"{anyPrefix}_first"].UnionWith(firstAfterAny);
                            AnyLasts[$"{anyPrefix}_last"].UnionWith(firstAfterAny);

                            if (mayBeEmpty)
                            {
                                AnyFirsts[$"{anyPrefix}_first"].UnionWith(g.Follow(nt.Name));
                                AnyLasts[$"{anyPrefix}_last"].UnionWith(g.Follow(nt.Name));
                            }
                        }
        }

        /// <summary>
        /// Задание правил для anyk_first и anyk_last
        /// </summary>
        private void ReplaceAnyFirstAndLast(Grammar g)
        {
            var anyFirstSets = new Dictionary<string, HashSet<string>>();
            var anyLastSets = new Dictionary<string, HashSet<string>>();
            var setsToModify = new Dictionary<string, HashSet<string>>[] 
            {
                AnyFirsts,
                AnyLasts
            };
            var setsToBuild = new Dictionary<string, HashSet<string>>[]
            {
                anyFirstSets,
                anyLastSets
            };

            bool changed;
            do
            {
                changed = false;

                for(var i=0; i<setsToModify.Length; ++i)
                    foreach(var any in setsToModify[i].Keys)
                        if(!setsToBuild[i].ContainsKey(any))
                        {
                            var dependencies =
                                setsToModify[i][any].Where(s => s.StartsWith("any")).ToList();
                            var dependencyLeft = false;

                            foreach (var dependency in dependencies)
                                /// Зависимость текста от самого себя игнорируем
                                if (dependency == any)
                                {
                                    setsToModify[i][any].Remove(dependency);
                                }
                                else if (anyFirstSets.ContainsKey(dependency))
                                {
                                    setsToModify[i][any].Remove(dependency);
                                    setsToModify[i][any].UnionWith(anyFirstSets[dependency]);
                                }
                                else
                                {
                                    dependencyLeft = true;
                                }

                            /// Если все зависимости заменили на множества токенов
                            if (!dependencyLeft)
                            {
                                setsToBuild[i][any] = 
                                    new HashSet<string>(g.Tokens.Keys.Except(setsToModify[i][any]));
                                changed = true;
                            }
                        }
            }
            while (changed);

            /// Если остались символы, которые по причине циклических зависимостей
            /// не смогли разложить на токены
            if (anyFirstSets.Count != AnyFirsts.Count 
                || anyLastSets.Count != AnyLasts.Count)
            {
                throw new IncorrectGrammarException(
                    "В грамматике есть циклические зависимости между символами TEXT"
                );
            }

            foreach (var set in setsToBuild)
                foreach (var any in set)
                {
                    g.RemoveSpecialToken(any.Key);

                    var newRule = new NonterminalSymbol(any.Key);

                    foreach (var token in any.Value)
                        newRule.Add(token);

                    g.DeclareNonterminal(newRule);
                }
        }

        #endregion Any replacement
    }
}
