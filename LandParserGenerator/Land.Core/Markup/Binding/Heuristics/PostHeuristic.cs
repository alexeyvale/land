using System;
using System.Collections.Generic;
using System.Linq;

namespace Land.Markup.Binding
{
	public static class DefaultWeightsProvider
	{
		private static Dictionary<ContextType, double> NaiveWeights { get; set; } = new Dictionary<ContextType, double>
		{
			{ContextType.HeaderCore,  3},
			{ContextType.HeaderSequence,  1},
			{ContextType.Inner, 1},
			{ContextType.Ancestors, 2},
			{ContextType.Siblings, 0.5},
		};

		public static double Get(ContextType contextType) =>
			NaiveWeights[contextType];

		public static double SumWeights() =>
			NaiveWeights.Values.Sum(e => e);
	}

	public interface IPostHeuristic { }

    public interface IWeightsHeuristic : IPostHeuristic
    {
        Dictionary<ContextType, double?> TuneWeights(
            PointContext source,
            List<RemapCandidateInfo> candidates,
            Dictionary<ContextType, double?> weights
        );
    }

    public interface ISimilarityHeuristic : IPostHeuristic
    {
        List<RemapCandidateInfo> PredictSimilarity(
            PointContext source,
            List<RemapCandidateInfo> candidates
        );
    }

	/// <summary>
	/// Устанавливает нулевой вес для пустых контекстов
	/// </summary>
	public class EmptyContextHeuristic : IWeightsHeuristic
	{
		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates, Dictionary<ContextType, double?> weights)
		{
			var existenceFlags = new Dictionary<ContextType, bool>
			{
				[ContextType.Inner] = candidates.Any(c => c.Context.InnerContext.Content.TextLength > 0)
					|| source.InnerContext.Content.TextLength > 0,
				[ContextType.HeaderCore] = candidates.Any(c => c.Context.HeaderContext.Core.Count > 0)
					|| source.HeaderContext.Core.Count > 0,
				[ContextType.HeaderSequence] = candidates.Any(c => c.Context.HeaderContext.Sequence.Count > 0)
					|| source.HeaderContext.Sequence.Count > 0,
				[ContextType.Ancestors] = (candidates.Any(c => c.Context.AncestorsContext.Count > 0)
					|| source.AncestorsContext.Count > 0),
				[ContextType.Siblings] = source.SiblingsContext?.Before.GlobalHash.TextLength > 0
					|| source.SiblingsContext?.After.GlobalHash.TextLength > 0
			};

			foreach (var kvp in existenceFlags)
			{
				if (!kvp.Value)
				{
					weights[kvp.Key] = 0;
				}
			}

			System.Diagnostics.Trace.WriteLine(
				$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
			);

			return weights;
		}
	}

	/// <summary>
	/// Приоитезирует контексты в зависимости от того, наколько их оценки различны между кандидатами
	/// </summary>
	public class PrioritizeByGapHeuristic : IWeightsHeuristic
	{
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
			if (candidates.Count > 1)
			{
				var features = new Dictionary<ContextType, ContextFeatures>
				{
					{ ContextType.HeaderCore,  GetFeatures(candidates, (c)=>c.HeaderCoreSimilarity) },
					{ ContextType.HeaderSequence,  GetFeatures(candidates, (c)=>c.HeaderSequenceSimilarity) },
					{ ContextType.Ancestors, GetFeatures(candidates, (c)=>c.AncestorSimilarity) },
					{ ContextType.Inner,  GetFeatures(candidates, (c)=>c.InnerSimilarity) }
				};

				/// Контексты с почти одинаковыми значениями похожести имеют минимальный вес,
				/// остальные сортируем в зависимости от того, насколько по ним различаются кандидаты
				var contextsToPrioritize = new List<ContextType>();

				foreach (var kvp in features.Where(f => !weights[f.Key].HasValue))
				{
					if (kvp.Value.MaxValue < ContextFinder.CANDIDATE_SIMILARITY_THRESHOLD 
						|| (1 - kvp.Value.MaxValue) * ContextFinder.SECOND_DISTANCE_GAP_COEFFICIENT > kvp.Value.GapFromMax)
					{
						weights[kvp.Key] = 1;
					}
					else
					{
						weights[kvp.Key] = DefaultWeightsProvider.Get(kvp.Key);
					}
				}

				System.Diagnostics.Trace.WriteLine(
					$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
				);
			}

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
			{
				gaps.Add(getSimilarity(ordered[i]) - getSimilarity(ordered[i + 1]));
			}
			gaps = gaps.OrderByDescending(e => e).ToList();

			return new ContextFeatures
			{
				MaxValue = getSimilarity(ordered.First()),
				GapFromMax = getSimilarity(ordered[0]) - getSimilarity(ordered[1]),
				MedianGap = gaps.Count % 2 == 0
							? (gaps[gaps.Count / 2] + gaps[gaps.Count / 2 - 1]) / 2
							: gaps[gaps.Count / 2]
			};
		}
	}

	/// <summary>
	/// Понижает приоритет внутреннего контекста, если нет сильно похожего элемента
	/// </summary>
	public class LowerFrequentlyChangingPriority : IWeightsHeuristic
	{
		const double GARBAGE_THRESHOLD = 0.6;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if (candidates.Max(c => c.InnerSimilarity) <= GARBAGE_THRESHOLD)
			{
				weights[ContextType.Inner] = 0;
			}

			if (candidates.Max(c => (c.SiblingsBeforeGlobalSimilarity + c.SiblingsAfterGlobalSimilarity) / 2.0) <= GARBAGE_THRESHOLD)
			{
				weights[ContextType.Siblings] = 0;
			}

			System.Diagnostics.Trace.WriteLine(
				$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
			);

			return weights;
		}
	}

	public class TuneInnerPriorityAccordingToLength : IWeightsHeuristic
	{
		const double INNER_LENGTH_THRESHOLD = 50;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			weights[ContextType.Inner] *= source.InnerContext.Content.TextLength /
				(source.InnerContext.Content.TextLength + INNER_LENGTH_THRESHOLD);

			System.Diagnostics.Trace.WriteLine(
				$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
			);

			return weights;
		}
	}

	public class DefaultWeightsHeuristic : IWeightsHeuristic
	{
		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if (weights[ContextType.HeaderCore] == null)
				weights[ContextType.HeaderCore] = 3;
			if (weights[ContextType.HeaderSequence] == null)
				weights[ContextType.HeaderSequence] = 1;
			if (weights[ContextType.Ancestors] == null)
				weights[ContextType.Ancestors] = 2;
			if (weights[ContextType.Inner] == null)
				weights[ContextType.Inner] = 1;
			if (weights[ContextType.Siblings] == null)
				weights[ContextType.Siblings] = 0.5;

			return weights;
		}
	}
}
