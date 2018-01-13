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
		private Grammar Gram { get; set; }
		private HashSet<Alternative>[,] Table { get; set; }
		private Dictionary<string, int> Lookaheads { get; set; }
		private Dictionary<string, int> NonterminalSymbols { get; set; }

		public TableLL1(Grammar g)
		{
			Gram = g;
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
					foreach (var tk in altFirst)
					{
						/// добавляем эту альтернативу в соответствующую ячейку таблицы
						this[nt, tk].Add(alt);
					}

					/// Если альтернатива может быть пустой
					/// и у неё нет приоритета
					if (altContainsEmpty)
					{
						var ntFollow = g.Follow(nt);

						/// её следует выбрать, если встретили то, что может идти
						/// после текущего нетерминала
						foreach (var tk in ntFollow.Where(t => !g.NonEmptyPrecedence.Contains(nt) || !g.First(nt).Contains(t)))
						{
							this[nt, tk].Add(alt);
						}
					}
				}
            }
		}

		public List<Message> CheckValidity()
		{
			var errors = new List<Message>();

			foreach(var nt in NonterminalSymbols.Keys)
				foreach(var tok in Lookaheads.Keys)
				{
					if(this[nt, tok].Count > 1)
					{
						errors.Add(Message.Error(
							$"Грамматика не является LL(1): для нетерминала {Gram.Userify(nt)} и токена {Gram.Userify(tok)} допустимо несколько альтернатив: {String.Join(", ", this[nt, tok])}",
							Gram.GetAnchor(nt),
							"LanD"
						));
					}
				}

			return errors;
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
