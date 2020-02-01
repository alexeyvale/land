using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Land.Markup.Binding
{
	public class EmptyContextHeuristic : IWeightsHeuristic
	{
		public long Priority => 100;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source, 
			List<RemapCandidateInfo> candidates, Dictionary<ContextType, double?> weights)
		{
			/// Это можно сделать статическими проверками на этапе формирования грамматики
			var useInner = candidates.Any(c => c.Context.InnerContext.Content.TextLength > 0)
					|| source.InnerContext.Content.TextLength > 0;
			var useHeader = candidates.Any(c => c.Context.HeaderContext.Count > 0)
				|| source.HeaderContext.Count > 0;
			var useAncestors = (candidates.Any(c => c.Context.AncestorsContext.Count > 0)
				|| source.AncestorsContext.Count > 0);

			if (!useHeader)
				weights[ContextType.Header] = 0;
			if (!useAncestors)
				weights[ContextType.Ancestors] = 0;
			if (!useInner)
				weights[ContextType.Inner] = 0;

			System.Diagnostics.Trace.WriteLine(
				$"{this.GetType().Name} H: {weights[ContextType.Header]} I: {weights[ContextType.Inner]} A: {weights[ContextType.Ancestors]}"
			);

			return weights;
		}
	}

	/// <summary>
	/// Приоитезирует контексты в зависимости от того, наколько их оценки различны между кандидатами
	/// </summary>
	public class PrioritizeByGapHeuristic : IWeightsHeuristic
	{
		public long Priority => 10;

		public class ContextFeatures
		{
			public double MaxValue { get; set; }
			public double GapFromMax { get; set; }
			public double MedianGap { get; set; }
		}

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			var MAX_WEIGHT = weights.Count;

			var features = new Dictionary<ContextType, ContextFeatures>
			{
				{ ContextType.Ancestors, GetFeatures(candidates, (c)=>c.AncestorSimilarity) },
				{ ContextType.Header,  GetFeatures(candidates, (c)=>c.HeaderSimilarity) },
				{ ContextType.Inner,  GetFeatures(candidates, (c)=>c.InnerSimilarity) }
			};

			/// Контексты с почти одинаковыми значениями похожести имеют минимальный вес,
			/// остальные сортируем в зависимости от того, насколько по ним различаются кандидаты
			var contextsToPrioritize = new List<ContextType>();

			foreach (var kvp in features.Where(f => !weights[f.Key].HasValue))
			{
				if (kvp.Value.MaxValue < ContextFinder.CANDIDATE_SIMILARITY_THRESHOLD ||
					(1 - kvp.Value.MaxValue) * ContextFinder.SECOND_DISTANCE_GAP_COEFFICIENT > kvp.Value.GapFromMax)
					weights[kvp.Key] = 1;
				else
					contextsToPrioritize.Add(kvp.Key);
			}

			contextsToPrioritize = contextsToPrioritize
				.OrderByDescending(c => features[c].MedianGap).ToList();

			for (var i = 0; i < contextsToPrioritize.Count; ++i)
				weights[contextsToPrioritize[i]] = MAX_WEIGHT - i;

			System.Diagnostics.Trace.WriteLine(
				$"{this.GetType().Name} H: {weights[ContextType.Header]} I: {weights[ContextType.Inner]} A: {weights[ContextType.Ancestors]}"
			);

			return weights;
		}

		private ContextFeatures GetFeatures(
			List<RemapCandidateInfo> candidates,
			Func<RemapCandidateInfo, double> getSimilarity)
		{
			/// Сортируем кандидатов по похожести каждого из контекстов
			var ordered = candidates.OrderByDescending(c => getSimilarity(c)).ToList();

			/// Считаем разности между последовательно идущими отсортированными по похожести элементами
			var gaps = new List<double>(ordered.Count - 1);
			for (var i = 0; i < ordered.Count - 1; ++i)
				gaps.Add(getSimilarity(ordered[i]) - getSimilarity(ordered[i + 1]));
			gaps = gaps.OrderByDescending(e => e).ToList();

			return new ContextFeatures
			{
				MaxValue = ordered.First().HeaderSimilarity,
				GapFromMax = ordered[0].HeaderSimilarity - ordered[1].HeaderSimilarity,
				MedianGap = gaps.Count % 2 == 0
							? (gaps[gaps.Count / 2] + gaps[gaps.Count / 2 - 1]) / 2
							: gaps[gaps.Count / 2]
			};
		}
	}

	/// <summary>
	/// Понижает приоритет внутреннего контекста, если нет сильно похожего элемента
	/// </summary>
	public class LowerChangedInnerPriority : IWeightsHeuristic
	{
		const double GARBAGE_INNER_THRESHOLD = 0.6;

		public long Priority => 20;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if (candidates.Max(c => c.InnerSimilarity) <= GARBAGE_INNER_THRESHOLD)
				weights[ContextType.Inner] = 0;

			System.Diagnostics.Trace.WriteLine(
				$"{this.GetType().Name} H: {weights[ContextType.Header]} I: {weights[ContextType.Inner]} A: {weights[ContextType.Ancestors]}"
			);

			return weights;
		}
	}

	public class DefaultWeightsHeuristic : IWeightsHeuristic
	{
		public long Priority => 0;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if (weights[ContextType.Header] == null)
				weights[ContextType.Header] = 3;
			if (weights[ContextType.Ancestors] == null)
				weights[ContextType.Ancestors] = 2;
			if (weights[ContextType.Inner] == null)
				weights[ContextType.Inner] = 1;

			return weights;
		}
	}
}