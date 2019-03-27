using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

using Land.Core.Parsing;

namespace Land.Core.Parsing.LL.Optimized
{
	/// <summary>
	/// Таблица LL(1) парсинга
	/// </summary>
	[Serializable]
	public class TableLL1: BaseTable
	{
		private Dictionary<int, Dictionary<int, List<Alternative>>> Table { get; set; }

		public TableLL1(Grammar g): base(g)
		{
			Table = g.RulesIdx.ToDictionary(
				r=>r.Key, 
				r=>g.TokensIdx.ToDictionary(t=>t.Key, t => new List<Alternative>())
			);

			foreach (var ntIdx in g.RulesIdx.Keys)
			{
				/// Проходим по всем продукциям
				foreach (var alt in g.RulesIdx[ntIdx])
				{
					var altFirst = g.First(alt.Elements.Select(e=>e.Index).ToList());
					var altContainsEmpty = altFirst.Remove(null);

					/// Для каждого токена, с которого может начинаться альтернатива
					foreach (int tkFirstIdx in altFirst)
					{
						/// добавляем эту альтернативу в соответствующую ячейку таблицы
						this[ntIdx, tkFirstIdx].Add(alt);
					}

					/// Если альтернатива может быть пустой
					if (altContainsEmpty)
					{
						var ntFollow = g.Follow(ntIdx);

						/// её следует выбрать для токена, который может идти следом,
						/// при этом если нетерминал порождён квантификаторами ?! или *!,
						/// данный токен не должен встречаться явно в First для текущего нетерминала,
						/// так как у пустой ветки самый низкий приоритет
						foreach (var tkFollow in ntFollow.Where(t => 
							g.NonEmptyPrecedence.Contains(GrammarObject.IndexToSymbol[ntIdx]) 
							&& !g.First(ntIdx).Contains(t)
							/// если Contains, то уже и так добавили эту ветку в таблицу
							|| !g.NonEmptyPrecedence.Contains(GrammarObject.IndexToSymbol[ntIdx]) 
							&& !altFirst.Contains(t)))
						{
							this[ntIdx, tkFollow].Add(alt);
						}
					}
				}
            }
		}

		public override List<Message> CheckValidity()
		{
			var errors = new List<Message>();

			foreach(var ntIdx in Table.Keys)
				foreach(var tkInfo in Table[ntIdx])
				{
					if(tkInfo.Value.Count > 1)
					{
						errors.Add(Message.Error(
							$"Грамматика не является LL(1): для нетерминала {GrammarObject.Userify(ntIdx)} и токена {GrammarObject.Userify(tkInfo.Key)} " +
								 $"допустимо несколько альтернатив: {String.Join(", ", this[ntIdx, tkInfo.Key].Select(e=> GrammarObject.Userify(e)))}",
							GrammarObject.GetLocation(GrammarObject.IndexToSymbol[ntIdx]),
							"LanD"
						));
					}
				}

			return errors;
		}

		public List<Alternative> this[int ntIdx, int tkIdx]
		{
			get { return Table[ntIdx][tkIdx]; }

			private set { Table[ntIdx][tkIdx] = value; }
		}

		public Dictionary<int, List<Alternative>> this[int ntIdx]
		{
			get
			{
				var allRecords = new Dictionary<int, List<Alternative>>();

				foreach (var tkIdx in GrammarObject.TokensIdx.Keys)
					allRecords[tkIdx] = this[ntIdx, tkIdx];

				return allRecords;
			}
		}

		/* Not implemented */
		public override void ExportToCsv(string filename)
		{ }
	}
}
