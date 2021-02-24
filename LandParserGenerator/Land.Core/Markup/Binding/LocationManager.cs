using System;
using System.Collections.Generic;
using System.Linq;

namespace Land.Markup.Binding
{
	public class LocationManager
	{
		public class LineInfo
		{
			public int Index { get; set; }
			public int? OffsetBefore { get; set; }
			public int? OffsetAfter { get; set; }
			public int? Shift { get; set; }
			public double LocationSimilarity { get; set; }
			public bool Permutation { get; set; }
		}

		private List<PointContext> ContextsOrderedByLine { get; set; }
		private Dictionary<PointContext, LineInfo> ContextToLineInfo { get; set; }
		private int TargetFileLength { get; set; }

		public LocationManager(IEnumerable<PointContext> contexts, int targetFileLength)
		{
			TargetFileLength = targetFileLength;

			ContextsOrderedByLine = contexts
				.OrderBy(c => c.StartOffset)
				.ToList();
			ContextToLineInfo = ContextsOrderedByLine
				.Select((e, i) => new { e, i })
				.ToDictionary(e => e.e, e => new LineInfo { Index = e.i, OffsetBefore = null, OffsetAfter = null });
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

				if (!currentContext.OffsetAfter.HasValue || currentContext.OffsetAfter > target.StartOffset)
				{
					if (currentContext.OffsetBefore > target.StartOffset)
					{
						currentContext.Permutation = true;
					}
					else
					{
						currentContext.OffsetAfter = target.StartOffset;
						UpdateSimilarity(ContextsOrderedByLine[i]);
					}
				}
			}

			for (var i = ContextToLineInfo[source].Index + 1; i < ContextsOrderedByLine.Count; ++i)
			{
				var currentContext = ContextToLineInfo[ContextsOrderedByLine[i]];

				if (!currentContext.OffsetBefore.HasValue || currentContext.OffsetBefore < target.EndOffset)
				{
					if (currentContext.OffsetAfter < target.EndOffset)
					{
						currentContext.Permutation = true;
					}
					else
					{
						currentContext.OffsetBefore = target.EndOffset;
						currentContext.Shift = target.EndOffset - source.EndOffset;
						UpdateSimilarity(ContextsOrderedByLine[i]);
					}
				}
			}
		}

		public int GetFrom(PointContext source) =>
			ContextToLineInfo[source].OffsetBefore ?? 0;

		public int GetTo(PointContext source) =>
			ContextToLineInfo[source].OffsetAfter ?? TargetFileLength;

		public int GetShift(PointContext source) =>
			ContextToLineInfo[source].Shift ?? 0;

		public bool InRange(PointContext source, PointContext target) =>
			GetFrom(source) <= target.StartOffset
			&& GetTo(source) >= target.EndOffset;

		public double GetLocationSimilarity(PointContext source) =>
			ContextToLineInfo[source].LocationSimilarity;

		public double GetSimilarity(PointContext source, PointContext target)
		{
			if(ContextToLineInfo[source].Permutation)
			{
				return 0;
			}

			return InRange(source, target)
				? ContextToLineInfo[source].LocationSimilarity
				: 0;
		}

		private double UpdateSimilarity(PointContext source) =>
			ContextToLineInfo[source].LocationSimilarity = ContextToLineInfo[source].Permutation ? 0
				: 1 - Math.Max(0, (GetTo(source) - GetFrom(source)) / (double)TargetFileLength);
	}
}
