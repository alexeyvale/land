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
			public int? LineBefore { get; set; }
			public int? LineAfter { get; set; }
			public int? Shift { get; set; }
			public double LocationWeight { get; set; }
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
				.ToDictionary(e => e.e, e => new LineInfo { Index = e.i, LineBefore = null, LineAfter = null });
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

				if (!currentContext.LineAfter.HasValue || currentContext.LineAfter > target.Line)
				{
					if (currentContext.LineBefore > target.Line)
					{
						currentContext.Permutation = true;
					}
					else
					{
						currentContext.LineAfter = target.Line;
						UpdateWeight(ContextsOrderedByLine[i]);
					}
				}
			}

			for (var i = ContextToLineInfo[source].Index + 1; i < ContextsOrderedByLine.Count; ++i)
			{
				var currentContext = ContextToLineInfo[ContextsOrderedByLine[i]];

				if (!currentContext.LineBefore.HasValue || currentContext.LineBefore < target.Line)
				{
					if (currentContext.LineAfter < target.Line)
					{
						currentContext.Permutation = true;
					}
					else
					{
						currentContext.LineBefore = target.Line;
						currentContext.Shift = target.Line - source.Line;
						UpdateWeight(ContextsOrderedByLine[i]);
					}
				}
			}
		}

		public int GetFrom(PointContext source) =>
			ContextToLineInfo[source].LineBefore ?? 0;

		public int GetTo(PointContext source) =>
			ContextToLineInfo[source].LineAfter ?? TargetFileLength;

		public int GetShift(PointContext source) =>
			ContextToLineInfo[source].Shift ?? 0;

		public bool InRange(PointContext source, PointContext target) =>
			GetFrom(source) <= target.Line
			&& GetTo(source) >= target.Line;

		public double GetWeight(PointContext source) =>
			ContextToLineInfo[source].LocationWeight;

		public double GetSimilarity(PointContext source, PointContext target)
		{
			if(ContextToLineInfo[source].Permutation)
			{
				return 0;
			}

			var expectedTargetLine = source.Line + GetShift(source);

			return InRange(source, target)
				? 1 - Math.Abs(target.Line - expectedTargetLine)
					/ (double)Math.Max(expectedTargetLine - GetFrom(source), GetTo(source) - expectedTargetLine)
				: 0;
		}

		private double UpdateWeight(PointContext source) =>
			ContextToLineInfo[source].LocationWeight = ContextToLineInfo[source].Permutation ? 0
				: 1 - Math.Max(0, (GetTo(source) - GetFrom(source)) / (double)TargetFileLength);
	}
}
