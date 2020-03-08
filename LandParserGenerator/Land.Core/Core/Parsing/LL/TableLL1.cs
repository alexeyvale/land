using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Land.Core.Specification;

namespace Land.Core.Parsing.LL
{
	/// <summary>
	/// Таблица LL(1) парсинга
	/// </summary>
	[Serializable]
	public class TableLL1: BaseTable
	{
		/// В ячейку таблицы альтернативы попадают в том порядке, в котором записаны в правиле
		private List<Alternative>[,] Table { get; set; }
		private Dictionary<string, int> Lookaheads { get; set; }
		private Dictionary<string, int> NonterminalSymbols { get; set; }

		public TableLL1(Grammar g): base(g)
		{
			var lookaheadSymbols = g.Tokens.Keys
				.Concat(g.Pairs.Where(p => p.Value.IsTokenLike).Select(p => p.Key))
				.ToList();

			Table = new List<Alternative>[g.Rules.Count, lookaheadSymbols.Count];

			NonterminalSymbols = g.Rules
				.Zip(Enumerable.Range(0, g.Rules.Count), (a, b) => new { smb = a.Key, idx = b })
				.ToDictionary(e => e.smb, e => e.idx);
			Lookaheads = lookaheadSymbols
				.Zip(Enumerable.Range(0, lookaheadSymbols.Count), (a, b) => new { smb = a, idx = b })
				.ToDictionary(e => e.smb, e => e.idx);

			foreach (var nt in g.Rules.Keys)
			{
				foreach (var la in lookaheadSymbols)
				{
					/// Список, потому что могут быть неоднозначности
					this[nt, la] = new List<Alternative>();
				}

				/// Проходим по всем продукциям
				foreach (var alt in g.Rules[nt])
				{
					var altFirst = g.First(alt);
					var altContainsEmpty = altFirst.Remove(null);

					/// Для каждого токена, с которого может начинаться альтернатива
					foreach (var tk in altFirst)
					{
						/// добавляем эту альтернативу в соответствующую ячейку таблицы
						this[nt, tk].Add(alt);
					}

					/// Если альтернатива может быть пустой
					if (altContainsEmpty)
					{
						var ntFollow = g.Follow(nt);

						/// её следует выбрать для токена, который может идти следом,
						/// при этом если нетерминал порождён квантификаторами ?! или *!,
						/// данный токен не должен встречаться явно в First для текущего нетерминала,
						/// так как у пустой ветки самый низкий приоритет
						foreach (var tk in ntFollow.Where(t => 
							g.NonEmptyPrecedence.Contains(nt) && !g.First(nt).Contains(t)
							/// если Contains, то уже и так добавили эту ветку в таблицу
							|| !g.NonEmptyPrecedence.Contains(nt) && !altFirst.Contains(t)))
						{
							this[nt, tk].Add(alt);
						}
					}
				}
            }
		}

		public override List<Message> CheckValidity()
		{
			var errors = new List<Message>();

			foreach(var nt in NonterminalSymbols.Keys)
				foreach(var tok in Lookaheads.Keys)
				{
					if(this[nt, tok].Count > 1)
					{
						errors.Add(Message.Error(
							$"Грамматика не является LL(1): для нетерминала {GrammarObject.Userify(nt)} и токена {GrammarObject.Userify(tok)} допустимо несколько альтернатив: {String.Join(", ", this[nt, tok].Select(e=>GrammarObject.Userify(e)))}",
							GrammarObject.GetLocation(nt),
							"LanD"
						));
					}
				}

			return errors;
		}

		public List<Alternative> this[string nt, string lookahead]
		{
			get { return Table[NonterminalSymbols[nt], Lookaheads[lookahead]]; }

			private set { Table[NonterminalSymbols[nt], Lookaheads[lookahead]] = value; }
		}

		public Dictionary<string, List<Alternative>> this[string nt]
		{
			get
			{
				var allRecords = new Dictionary<string, List<Alternative>>();

				foreach (var lookahead in Lookaheads.Keys)
					allRecords[lookahead] = this[nt, lookahead];

				return allRecords;
			}
		}

		public override void ExportToCsv(string filename)
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
