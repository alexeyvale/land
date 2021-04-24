﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Land.Markup.Binding
{
	public class LocationManager
	{
		public class LineInfo
		{
			public int Index { get; set; }

			public int? CountBefore { get; set; }
			public int? CountAfter { get; set; }
			public int OffsetBefore { get; set; }
			public int OffsetAfter { get; set; }
			public bool ImmediateBeforeFound { get; set; }
			public bool ImmediateAfterFound { get; set; }

			public double LocationSimilarity { get; set; }

			public bool Permutation { get; set; }
		}

		private List<PointContext> ContextsOrderedByLine { get; set; }
		private Dictionary<PointContext, LineInfo> ContextToLineInfo { get; set; }

		public LocationManager(IEnumerable<PointContext> contexts, int targetFileLength)
		{
			ContextsOrderedByLine = contexts
				.OrderBy(c => c.StartOffset)
				.ToList();
			ContextToLineInfo = ContextsOrderedByLine
				.Select((e, i) => new { e, i })
				.ToDictionary(e => e.e, e => new LineInfo { 
					Index = e.i, 
					OffsetBefore = 0, 
					OffsetAfter = targetFileLength,
					ImmediateAfterFound = e.e.SiblingsContext?.After.Nearest.Count == 0,
					ImmediateBeforeFound = e.e.SiblingsContext?.Before.Nearest.Count == 0,
				});
		}

		/// <summary>
		/// Фиксация знания о том, что ранее помеченную точка source
		/// в новом файле соответствует точке target
		/// </summary>
		public void Mapped(PointContext source, PointContext target)
		{
			for (var i = 0; i < ContextToLineInfo[source].Index; ++i)
			{
				var currentContext = ContextToLineInfo[ContextsOrderedByLine[i]];

				if (currentContext.OffsetAfter > target.StartOffset)
				{
					if (currentContext.OffsetBefore <= target.StartOffset)
					{
						currentContext.CountAfter = ContextToLineInfo.Count - ContextToLineInfo[source].Index;
						currentContext.OffsetAfter = target.StartOffset;
						currentContext.ImmediateAfterFound |=
							(ContextsOrderedByLine[i]?.SiblingsContext?.After.Nearest.Contains(source) ?? false);

						UpdateSimilarity(ContextsOrderedByLine[i]);
					}
					else
					{
						currentContext.Permutation = true;
					}
				}
			}

			for (var i = ContextToLineInfo[source].Index + 1; i < ContextsOrderedByLine.Count; ++i)
			{
				var currentContext = ContextToLineInfo[ContextsOrderedByLine[i]];

				if (currentContext.OffsetBefore < target.EndOffset)
				{
					if (currentContext.OffsetAfter >= target.EndOffset)
					{
						currentContext.CountBefore = ContextToLineInfo[source].Index + 1;
						currentContext.OffsetBefore = target.EndOffset;
						currentContext.ImmediateBeforeFound |=
							(ContextsOrderedByLine[i]?.SiblingsContext?.Before.Nearest.Contains(source) ?? false);

						UpdateSimilarity(ContextsOrderedByLine[i]);
					}
					else
					{
						currentContext.Permutation = true;
					}
				}
			}
		}

		public int GetFrom(PointContext source) =>
			ContextToLineInfo[source].OffsetBefore;

		public int GetTo(PointContext source) =>
			ContextToLineInfo[source].OffsetAfter;

		public bool InRange(PointContext source, PointContext target) =>
			GetFrom(source) <= target.StartOffset
			&& GetTo(source) >= target.EndOffset;

		public double GetLocationSimilarity(PointContext source) =>
			ContextToLineInfo[source].LocationSimilarity;

		public double GetSimilarity(PointContext source, PointContext target) =>
			!ContextToLineInfo[source].Permutation && InRange(source, target) 
				? ContextToLineInfo[source].LocationSimilarity : 0;

		private void UpdateSimilarity(PointContext source)
		{
			var beforeCount = ContextToLineInfo[source].Index;
			var afterCount = ContextToLineInfo.Count - ContextToLineInfo[source].Index - 1;

			if (beforeCount > 0 || afterCount > 0)
			{
				var step = 0.8 / (beforeCount + afterCount);

				ContextToLineInfo[source].LocationSimilarity = step * (ContextToLineInfo[source].CountBefore ?? 0)
						+ step * (ContextToLineInfo[source].CountAfter ?? 0)
						+ ((ContextToLineInfo[source].ImmediateAfterFound || afterCount == 0) ? 0.1 : 0)
						+ ((ContextToLineInfo[source].ImmediateBeforeFound || beforeCount == 0) ? 0.1 : 0);
			}
			else
			{
				ContextToLineInfo[source].LocationSimilarity = 0;
			}
		}
	}
}
