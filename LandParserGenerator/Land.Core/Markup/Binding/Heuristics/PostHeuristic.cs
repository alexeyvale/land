using System;
using System.Collections.Generic;
using System.Linq;

namespace Land.Markup.Binding
{
	public static class DefaultWeightsProvider
	{
		private static Dictionary<ContextType, double> NaiveWeights { get; set; } = new Dictionary<ContextType, double>
		{
			{ContextType.HeaderCore,  2},
			{ContextType.HeaderSequence,  1},
			{ContextType.Inner, 2},
			{ContextType.Ancestors, 1},
			{ContextType.Siblings, 1},
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

			//System.Diagnostics.Trace.WriteLine(
			//	$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
			//);

			return weights;
		}
	}

	public class TuneHeaderWeightIfSimilar : IWeightsHeuristic
	{
		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			var maxSimilarityCandidates = candidates.Where(c => c.HeaderCoreSimilarity == 1).ToList();

			InitWithDefault(weights, ContextType.HeaderSequence);
			InitWithDefault(weights, ContextType.HeaderCore);

			if (maxSimilarityCandidates.Count > 0)
			{
				weights[ContextType.HeaderSequence] *= 4;
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

		private void InitWithDefault(Dictionary<ContextType, double?> weights, ContextType type)
		{
			if(!weights[type].HasValue)
			{
				weights[type] = DefaultWeightsProvider.Get(type);
			}
		}
	}

	public class TuneInnerWeightAsFrequentlyChanging : IWeightsHeuristic
	{
		const double EXCELLENT_THRESHOLD = 0.9;
		const double GARBAGE_THRESHOLD = 0.6;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			var maxInnerSimilarity = candidates.Max(c => c.InnerSimilarity);

			var coeff = maxInnerSimilarity < EXCELLENT_THRESHOLD
				 ? (Math.Max(maxInnerSimilarity, GARBAGE_THRESHOLD) - GARBAGE_THRESHOLD) / (1 - GARBAGE_THRESHOLD)
				 : 2;

			weights[ContextType.Inner] = coeff * DefaultWeightsProvider.Get(ContextType.Inner);

			//System.Diagnostics.Trace.WriteLine(
			//	$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
			//);

			return weights;
		}
	}

	public class TuneSiblingsWeightAsFrequentlyChanging : IWeightsHeuristic
	{
		const double EXCELLENT_THRESHOLD = 0.95;
		const double GARBAGE_THRESHOLD = 0.8;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			var maxInnerSimilarity = candidates.Max(c => c.SiblingsSimilarity);

			var coeff = maxInnerSimilarity < EXCELLENT_THRESHOLD
				 ? (Math.Max(maxInnerSimilarity, GARBAGE_THRESHOLD) - GARBAGE_THRESHOLD) / (1 - GARBAGE_THRESHOLD)
				 : 2;

			weights[ContextType.Siblings] = coeff * DefaultWeightsProvider.Get(ContextType.Siblings);

			//System.Diagnostics.Trace.WriteLine(
			//	$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
			//);

			return weights;
		}
	}

	public class TuneInnerWeightAccordingToLength : IWeightsHeuristic
	{
		const double THRESHOLD = 20;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			weights[ContextType.Inner] *= source.InnerContext.Content.TextLength /
				Math.Max(source.InnerContext.Content.TextLength, THRESHOLD);

			//System.Diagnostics.Trace.WriteLine(
			//	$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
			//);

			return weights;
		}
	}

	public class TuneSiblingsWeightAccordingToLength : IWeightsHeuristic
	{
		const double THRESHOLD = 500;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			var maxLength = Math.Max(source.SiblingsContext.Before.GlobalHash.TextLength, source.SiblingsContext.After.GlobalHash.TextLength);

			weights[ContextType.Siblings] *= maxLength / Math.Max(maxLength, THRESHOLD);

			//System.Diagnostics.Trace.WriteLine(
			//	$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
			//);

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
