using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Land.Core.Parsing.LR;

namespace Land.Core.Specification
{
	public class ClosureGotoBuilder
	{
		private FirstBuilder FirstBuilder { get; set; }
		private FirstBuilder FirstModifiedBuilder { get; set; }

		private Grammar GrammarObject { get; set; }

		public ClosureGotoBuilder(Grammar g)
		{
			GrammarObject = g;

			FirstBuilder = new FirstBuilder(g, false);
			FirstModifiedBuilder = new FirstBuilder(g, true);
		}

		/// <summary>
		/// Построение замыкания множества пунктов
		/// </summary>
		public Item BuildClosure(Item item)
		{
			var closedMarkers = item.Markers;
			var anyMarkers = item.AnyProvokedMarkers;

			int oldUsualMarkersCount, oldAnyProvokedMarkersCount;

			do
			{
				oldUsualMarkersCount = closedMarkers.Count;
				oldAnyProvokedMarkersCount = anyMarkers.Count;

				var newMarkers = new HashSet<Marker>();

				/// Обрабатываем пункты, которые появились за счёт First'
				foreach (var marker in anyMarkers
					.Where(m => GrammarObject.Rules.ContainsKey(m.Next)).ToList())
				{
					var nt = GrammarObject.Rules[marker.Next];

					var sequenceAfterNt = marker.Alternative
						.Subsequence(marker.Position + 1)
						.Add(marker.Lookahead);

					foreach (var alt in nt)
					{
						foreach (var t in FirstModifiedBuilder.First(sequenceAfterNt)
							.Where(e => e != null))
						{
							anyMarkers.Add(new Marker(alt, 0, t));
						}
					}
				}

				/// Проходим по всем обычным пунктам, которые предшествуют нетерминалам
				foreach (var marker in closedMarkers
					.Where(m => GrammarObject.Rules.ContainsKey(m.Next)))
				{
					var nt = GrammarObject.Rules[marker.Next];
					/// Будем брать FIRST от того, что идёт после этого нетерминала 
					/// + символ предпросмотра
					var sequenceAfterNt = marker.Alternative
						.Subsequence(marker.Position + 1)
						.Add(marker.Lookahead);

					foreach (var alt in nt)
					{
						var first = FirstBuilder.First(sequenceAfterNt);

						foreach (var t in first)
						{
							newMarkers.Add(new Marker(alt, 0, t));
						}

						foreach (var t in FirstModifiedBuilder.First(sequenceAfterNt)
							.Except(first)
							.Where(e => e != null))
						{
							anyMarkers.Add(new Marker(alt, 0, t));
						}
					}
				}

				closedMarkers.UnionWith(newMarkers);
			}
			while (oldUsualMarkersCount != closedMarkers.Count
				|| oldAnyProvokedMarkersCount != anyMarkers.Count);

			anyMarkers.ExceptWith(closedMarkers);

			return new Item { 
				Markers = closedMarkers, 
				AnyProvokedMarkers = anyMarkers 
			};
		}

		public Item Goto(Item I, string smb)
		{
			var res = new Item
			{
				Markers = new HashSet<Marker>(
					I.Markers.Where(m => m.Next == smb).Select(m=>m.ShiftNext())
				),
				AnyProvokedMarkers = new HashSet<Marker>(
					I.AnyProvokedMarkers.Where(m => m.Next == smb).Select(m => m.ShiftNext())
				),
			};

			return BuildClosure(res);
		}

		public Tuple<HashSet<string>, HashSet<string>> First(Marker marker)
		{
			var sequenceAfterNt = marker.Alternative
				.Subsequence(marker.Position)
				.Add(marker.Lookahead);

			var first = FirstBuilder.First(sequenceAfterNt);

			var anyFirst = FirstModifiedBuilder.First(sequenceAfterNt);
			anyFirst.ExceptWith(first);

			return new Tuple<HashSet<string>, HashSet<string>>(first, anyFirst);
		}
	}
}
