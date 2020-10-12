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
			{ContextType.HeaderNonCore,  1},
			{ContextType.Inner, 1},
			{ContextType.Ancestors, 2},
			{ContextType.Siblings, 1},
		};

		public static double Get(ContextType contextType) =>
			NaiveWeights[contextType];

		public static double SumWeights() =>
			NaiveWeights.Values.Sum(e => e);

		public static void Init(Dictionary<ContextType, double?> weights, ContextType type)
		{
			if (!weights[type].HasValue)
			{
				weights[type] = Get(type);
			}
		}
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
				[ContextType.HeaderNonCore] = candidates.Any(c => c.Context.HeaderContext.NonCore.Count > 0)
					|| source.HeaderContext.NonCore.Count > 0,
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

			//System.Diagnostics.Trace.WriteLine(
			//	$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
			//);

			return weights;
		}
	}

	//public class TuneHeaderWeightByPriority : IWeightsHeuristic
	//{
	//	public Dictionary<ContextType, double?> TuneWeights(
	//		PointContext source,
	//		List<RemapCandidateInfo> candidates,
	//		Dictionary<ContextType, double?> weights)
	//	{
	//		//if (source.HeaderContext.Core.Count > 0)
	//		//{
	//		//	var prioritiesSum = source.HeaderContext.Core.Concat(source.HeaderContext.NonCore).Sum(e => e.Priority);

	//		//	weights[ContextType.HeaderSequence] = DefaultWeightsProvider.Get(ContextType.HeaderSequence)
	//		//		+ source.HeaderContext.NonCore.Sum(e => e.Priority) / prioritiesSum;
	//		//	weights[ContextType.HeaderCore] = DefaultWeightsProvider.Get(ContextType.HeaderCore)
	//		//		+ source.HeaderContext.Core.Sum(e => e.Priority) / prioritiesSum;
	//		//}

	//		return weights;
	//	}
	//}

	public class TuneHeaderWeightIfSimilar : IWeightsHeuristic
	{
		const double EXCELLENT_THRESHOLD = 0.9;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			var maxSimilarityCandidates = candidates.Where(c => c.HeaderCoreSimilarity >= EXCELLENT_THRESHOLD).ToList();

			DefaultWeightsProvider.Init(weights, ContextType.HeaderNonCore);
			DefaultWeightsProvider.Init(weights, ContextType.HeaderCore);

			if (maxSimilarityCandidates.Count > 0)
			{
				var maxHeaderNonCoreSimilarity = maxSimilarityCandidates.Max(c => c.HeaderNonCoreSimilarity);

				if (maxHeaderNonCoreSimilarity >= EXCELLENT_THRESHOLD
					&& maxSimilarityCandidates.Where(e => e.HeaderNonCoreSimilarity == maxHeaderNonCoreSimilarity).Count() == 1)
				{
					weights[ContextType.HeaderNonCore] = weights[ContextType.HeaderCore] * 2;
				}
			}

			if (maxSimilarityCandidates.Count == 1)
			{
				weights[ContextType.HeaderCore] *= 2;
			}

			//System.Diagnostics.Trace.WriteLine(
			//	$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
			//);

			return weights;
		}
	}

	public class TuneInnerWeightAsFrequentlyChanging : IWeightsHeuristic
	{
		const double LENGTH_THRESHOLD = 30;
		const double EXCELLENT_THRESHOLD = 0.9;
		const double GARBAGE_THRESHOLD = 0.6;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if (source.InnerContext.Content.TextLength > LENGTH_THRESHOLD)
			{
				var bestCandidates = candidates.Where(c => c.InnerSimilarity >= EXCELLENT_THRESHOLD).ToList();
				var maxSimilarity = candidates.Max(c => c.InnerSimilarity);

				var coeff = bestCandidates.Count != 1
					 ? 0.5 + 0.5 * (Math.Max(maxSimilarity, GARBAGE_THRESHOLD) - GARBAGE_THRESHOLD) / (1 - GARBAGE_THRESHOLD)
					 : 2;

				weights[ContextType.Inner] = coeff * DefaultWeightsProvider.Get(ContextType.Inner);
			}

			//System.Diagnostics.Trace.WriteLine(
			//	$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
			//);

			return weights;
		}
	}

	public class TuneSiblingsWeightAsFrequentlyChanging : IWeightsHeuristic
	{
		const double LENGTH_THRESHOLD = 500;
		const double EXCELLENT_THRESHOLD = 0.9;
		const double GARBAGE_THRESHOLD = 0.8;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if (Math.Max(source.SiblingsContext.Before.GlobalHash.TextLength,
				source.SiblingsContext.After.GlobalHash.TextLength) > LENGTH_THRESHOLD)
			{
				var bestCandidates = candidates.Where(c => c.SiblingsSimilarity >= EXCELLENT_THRESHOLD).ToList();
				var maxSimilarity = candidates.Max(c => c.SiblingsSimilarity);

				var coeff = bestCandidates.Count != 1
					 ? 0.5 + 0.5 * (Math.Max(maxSimilarity, GARBAGE_THRESHOLD) - GARBAGE_THRESHOLD) / (1 - GARBAGE_THRESHOLD)
					 : 2;

				weights[ContextType.Siblings] = coeff * DefaultWeightsProvider.Get(ContextType.Siblings);

				//System.Diagnostics.Trace.WriteLine(
				//	$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
				//);
			}

			return weights;
		}
	}

	public class TuneInnerWeightAccordingToLength : IWeightsHeuristic
	{
		const double LENGTH_THRESHOLD = 30;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			DefaultWeightsProvider.Init(weights, ContextType.Inner);

			weights[ContextType.Inner] *= source.InnerContext.Content.TextLength /
				Math.Max(source.InnerContext.Content.TextLength, LENGTH_THRESHOLD);

			//System.Diagnostics.Trace.WriteLine(
			//	$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
			//);

			return weights;
		}
	}

	public class TuneSiblingsWeightAccordingToLength : IWeightsHeuristic
	{
		const double LENGTH_THRESHOLD = 500;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			var maxLength = Math.Max(
				source.SiblingsContext.Before.GlobalHash.TextLength, 
				source.SiblingsContext.After.GlobalHash.TextLength
			);

			DefaultWeightsProvider.Init(weights, ContextType.Siblings);

			weights[ContextType.Siblings] *= maxLength / Math.Max(maxLength, LENGTH_THRESHOLD);

			//System.Diagnostics.Trace.WriteLine(
			//	$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
			//);

			return weights;
		}
	}

	//public class TuneWeightsIfCanBeusedForDecision : IWeightsHeuristic
	//{
	//	public class ContextFeatures
	//	{
	//		public double MaxValue { get; set; }
	//		public double GapFromMax { get; set; }
	//		public double MedianGap { get; set; }
	//	}

	//	public Dictionary<ContextType, double?> TuneWeights(
	//		PointContext source,
	//		List<RemapCandidateInfo> candidates,
	//		Dictionary<ContextType, double?> weights)
	//	{
	//		if (candidates.Count > 1)
	//		{
	//			var features = new Dictionary<ContextType, ContextFeatures>
	//			{
	//				{ ContextType.HeaderCore,  GetFeatures(candidates, (c)=>c.HeaderCoreSimilarity) },
	//				{ ContextType.HeaderNonCore,  GetFeatures(candidates, (c)=>c.HeaderNonCoreSimilarity) },
	//				{ ContextType.Ancestors, GetFeatures(candidates, (c)=>c.AncestorSimilarity) },
	//				{ ContextType.Inner,  GetFeatures(candidates, (c)=>c.InnerSimilarity) }
	//			};

	//			var contextsToPrioritize = new List<ContextType>();

	//			foreach (var kvp in features)
	//			{
	//				DefaultWeightsProvider.Init(weights, kvp.Key);

	//				if (kvp.Value.MaxValue > ContextFinder.CANDIDATE_SIMILARITY_THRESHOLD
	//					&& (1 - kvp.Value.MaxValue) * ContextFinder.SECOND_DISTANCE_GAP_COEFFICIENT < kvp.Value.GapFromMax)
	//				{			
	//					weights[kvp.Key] *= 2;
	//				}
	//				else
	//				{
	//					weights[kvp.Key] /= 2;
	//				}
	//			}
	//		}

	//		return weights;
	//	}

	//	private ContextFeatures GetFeatures(
	//		List<RemapCandidateInfo> candidates,
	//		Func<RemapCandidateInfo, double> getSimilarity)
	//	{
	//		/// Сортируем кандидатов по похожести каждого из контекстов
	//		var ordered = candidates.OrderByDescending(c => getSimilarity(c)).ToList();

	//		/// Считаем разности между последовательно идущими отсортированными по похожести элементами
	//		var gaps = new List<double>(ordered.Count - 1);
	//		for (var i = 0; i < ordered.Count - 1; ++i)
	//		{
	//			gaps.Add(getSimilarity(ordered[i]) - getSimilarity(ordered[i + 1]));
	//		}
	//		gaps = gaps.OrderByDescending(e => e).ToList();

	//		return new ContextFeatures
	//		{
	//			MaxValue = getSimilarity(ordered.First()),
	//			GapFromMax = getSimilarity(ordered[0]) - getSimilarity(ordered[1]),
	//			MedianGap = gaps.Count % 2 == 0
	//						? (gaps[gaps.Count / 2] + gaps[gaps.Count / 2 - 1]) / 2
	//						: gaps[gaps.Count / 2]
	//		};
	//	}
	//}

	public class DefaultWeightsHeuristic : IWeightsHeuristic
	{
		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			foreach(var val in Enum.GetValues(typeof(ContextType)).Cast<ContextType>())
			{
				if(weights[val] == null)
				{
					weights[val] = DefaultWeightsProvider.Get(val);
				}
			}

			return weights;
		}
	}
}
