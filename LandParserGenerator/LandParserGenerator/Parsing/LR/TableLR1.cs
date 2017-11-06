using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing.LR
{
	/// <summary>
	/// Таблица LR(1) парсинга
	/// </summary>
	public class TableLR1
	{
		private Dictionary<string, int> Lookaheads { get; set; }

		/// <summary>
		/// Множества состояний (множество множеств пунктов)
		/// </summary>
		private List<HashSet<Marker>> Items { get; set; }
		/// <summary>
		/// Действия, которые надо совершать при встрече различных терминалов
		/// </summary>
		private HashSet<Action>[,] Actions { get; set; }
		/// <summary>
		/// Переходы между состояниями
		/// </summary>
		public List<Dictionary<string, int>> Transitions { get; private set; }

		public TableLR1(Grammar g)
		{
			Lookaheads = g.Tokens.Keys.Union(g.SpecialTokens)
				.Zip(Enumerable.Range(0, g.Tokens.Count + g.SpecialTokens.Count), (a, b) => new { smb = a, idx = b })
				.ToDictionary(e => e.smb, e => e.idx);

			/// Строим набор множеств пунктов
			BuildItems(g);

			Actions = new HashSet<Action>[Items.Count, Lookaheads.Count];

			for(var i=0; i<Items.Count;++i)
			{
				foreach (var lookahead in Lookaheads)
					this[i, lookahead.Key] = new HashSet<Action>();

				foreach(var marker in Items[i])
				{
					/// A => alpha * a beta
					if(g[marker.Next] is TerminalSymbol || g.SpecialTokens.Contains(marker.Next))
					{
						this[i, marker.Next].Add(new ShiftAction()
						{
							TargetItemIndex = Transitions[i][marker.Next]
						});
					}

					/// A => alpha *
					if (String.IsNullOrEmpty(marker.Next) 
						&& marker.Alternative.NonterminalSymbolName != g.StartSymbol)
					{
						this[i, marker.Lookahead].Add(new ReduceAction()
						{
							ReductionAlternative = marker.Alternative
						});
					}
				}

				/// S => ...*, $
				if (Items[i].Any(item=>item.Alternative.NonterminalSymbolName == g.StartSymbol 
					&& String.IsNullOrEmpty(item.Next)
					&& item.Lookahead == Grammar.EOF_TOKEN_NAME))
				{
					this[i, Grammar.EOF_TOKEN_NAME].Add(new AcceptAction());
				}
			}
		}

		private void BuildItems(Grammar g)
		{
			Items = new List<HashSet<Marker>>()
			{
				g.BuildClosure(new HashSet<Marker>(
					(g[g.StartSymbol] as NonterminalSymbol).Alternatives.Select(a=>new Marker(a, 0, Grammar.EOF_TOKEN_NAME))
				))
			};

			Transitions = new List<Dictionary<string, int>>();

			for (var i = 0; i < Items.Count; ++i)
			{
				Transitions.Add(new Dictionary<string, int>());

				foreach (var smb in g.Tokens.Keys.Union(g.Rules.Keys).Union(g.SpecialTokens))
				{
					var gotoSet = g.Goto(Items[i], smb);

					if (gotoSet.Count > 0)
					{
						/// Проверяем, не совпадает ли полученное множество 
						/// с каким-либо из имеющихся
						var j = 0;
						for (; j < Items.Count; ++j)
							if (EqualMarkerSets(Items[j], gotoSet))
							{
								break;
							}

						/// Если не нашли совпадение
						if (j == Items.Count)
						{
							Items.Add(gotoSet);
						}

						Transitions[i][smb] = j;
					}
				}
			}
		}

		private bool EqualMarkerSets(HashSet<Marker> a, HashSet<Marker> b)
		{
			if (a.Count != b.Count)
				return false;

			foreach(var elem in a)
			{
				if (!b.Contains(elem))
					return false;
			}

			return true;
		}

		public HashSet<Action> this[int i, string lookahead]
		{
			get { return Actions[i, Lookaheads[lookahead]]; }

			private set { Actions[i, Lookaheads[lookahead]] = value; }
		}

		public void ExportToCsv(string filename)
		{
			var output = new StreamWriter(filename);

			var orderedLookaheads = Lookaheads.OrderBy(l => l.Value);
			output.WriteLine("," + String.Join(",", orderedLookaheads.Select(l => l.Key)));

			for(var i=0; i< Items.Count; ++i)
			{
				output.Write($"{i},");

				output.Write(String.Join(",",
					orderedLookaheads.Select(l => this[i, l.Key])
					.Select(alts => alts.Count == 0 ? "" : alts.Count == 1 ? alts.Single().ToString() : String.Join("/", alts))));

				output.WriteLine();
			}

			output.Close();
		}
	}
}
