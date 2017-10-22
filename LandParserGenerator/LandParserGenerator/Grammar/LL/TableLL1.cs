using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
	/// <summary>
	/// Таблица LL(1) парсинга
	/// </summary>
	public class TableLL1
	{
		private List<Alternative>[,] Table { get; set; }
		private Dictionary<string, int> Lookaheads { get; set; }
		private Dictionary<string, int> NonterminalSymbols { get; set; }

		public TableLL1(Grammar g)
		{
			Table = new List<Alternative>[g.Rules.Count, g.Tokens.Count];

			NonterminalSymbols = g.Rules
				.Zip(Enumerable.Range(0, g.Rules.Count), (a, b) => new { smb = a.Key, idx = b })
				.ToDictionary(e => e.smb, e => e.idx);
			Lookaheads = g.Tokens
				.Zip(Enumerable.Range(0, g.Tokens.Count), (a, b) => new { smb = a.Key, idx = b })
				.ToDictionary(e => e.smb, e => e.idx);

			/// Заполняем ячейки таблицы
			foreach (var nt in g.Rules.Keys)
			{
				foreach (var tk in g.Tokens)
				{
					/// Список, потому что могут быть неоднозначности
					this[nt, tk.Key] = new List<Alternative>();
				}

				/// Проходим по всем продукциям
				foreach (var alt in g.Rules[nt])
				{
					var altFirst = g.First(alt);
					var containsEmpty = altFirst.Remove(Grammar.EmptyToken);

					/// Для каждого токена, с которого может начинаться альтернатива
					foreach(var tk in altFirst)
					{
						this[nt, tk.Name].Add(alt);
					}

					/// Если альтернатива может быть пустой
					if(containsEmpty)
					{
						var ntFollow = g.Follow(nt);

						/// её следует выбрать, если встретили то, что может идти
						/// после текущего нетерминала
						foreach (var tk in ntFollow)
						{
							this[nt, tk.Name].Add(alt);
						}
					}
				}
			}
		}

		public List<Alternative> this[string nt, string lookahead]
		{
			get { return Table[NonterminalSymbols[nt], Lookaheads[lookahead]]; }

			private set { Table[NonterminalSymbols[nt], Lookaheads[lookahead]] = value; }
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
					.Select(alts => alts.Count == 0 ? "" : alts.Count == 1 ? alts[0].ToString() : String.Join("/", alts))));

				output.WriteLine();
			}

			output.Close();
		}
	}
}
