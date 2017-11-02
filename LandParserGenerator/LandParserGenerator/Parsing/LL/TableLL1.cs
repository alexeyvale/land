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
	public class TableLL1
	{
		private HashSet<Alternative>[,] Table { get; set; }
		private Dictionary<string, int> Lookaheads { get; set; }
		private Dictionary<string, int> NonterminalSymbols { get; set; }

		public TableLL1(Grammar g)
		{
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

					var containsEmpty = altFirst.Remove(null);
					var containsText = altFirst.Any(t => t == Grammar.TEXT_TOKEN_NAME);

					/// Для каждого реально возвращаемого лексером токена, 
                    /// с которого может начинаться альтернатива
					foreach (var tk in altFirst.Where(t=>!g.SpecialTokens.Contains(t)))
					{
						/// добавляем эту альтернативу в соответствующую ячейку таблицы
						this[nt, tk].Add(alt);
					}

					/// Если альтернатива может быть пустой
					if (containsEmpty)
					{
						var ntFollow = g.Follow(nt);

						var followContainsText = ntFollow.Contains(Grammar.TEXT_TOKEN_NAME);

						/// её следует выбрать, если встретили то, что может идти
						/// после текущего нетерминала
						foreach (var tk in ntFollow.Where(t => !g.SpecialTokens.Contains(t)))
						{
							this[nt, tk].Add(alt);
						}

						if(followContainsText)
						{
                            foreach (var tk in g.Tokens.Keys
                                .Except(ntFollow).Except(g.First(nt)))
                            {
                                this[nt, tk].Add(alt);
                            }
                        }
					}

					/// Если альтернатива может начинаться с ANY,
                    /// переход к этой альтернативе должен происходить
                    /// по любому символу, с которого не может начинаться
                    /// правило для текущего нетерминала
					if (containsText)
					{
                        foreach (var tk in g.Tokens.Keys
                            .Except(g.First(nt)))
                        {
                            this[nt, tk].Add(alt);
                        }
					}
				}
			}
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
	}
}
