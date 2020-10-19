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

	public class TuneHeaderWeightIfSimilar : IWeightsHeuristic
	{
		const double GARBAGE_THRESHOLD = 0.4;
		const double EXCELLENT_THRESHOLD = 0.9;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			void ReduceExternalContextsWeights()
			{
				DefaultWeightsProvider.Init(weights, ContextType.Ancestors);
				DefaultWeightsProvider.Init(weights, ContextType.Siblings);

				weights[ContextType.Ancestors] /= 2;
				weights[ContextType.Siblings] /= 2;
			}

			var maxSimilarityCandidates = candidates.Where(c => c.HeaderCoreSimilarity >= EXCELLENT_THRESHOLD).ToList();

			DefaultWeightsProvider.Init(weights, ContextType.HeaderNonCore);
			DefaultWeightsProvider.Init(weights, ContextType.HeaderCore);

			/// Если есть кандидаты с высокой похожестью ядра заголовка
			if (maxSimilarityCandidates.Count > 0)
			{
				/// Если такой кандидат один, дополнительно повышаем вес ядра
				if (maxSimilarityCandidates.Count == 1)
				{
					weights[ContextType.HeaderCore] *= 2;
				}
				/// Если их несколько, разобраться поможет остальная часть заголовка
				else
				{
					weights[ContextType.HeaderNonCore] = weights[ContextType.HeaderCore] * 2;

					ReduceExternalContextsWeights();
				}
			}
			/// Если у всех кандидатов похожесть ядра небольшая, остальная часть заголовка нас только запутает
			else if(candidates.Max(c=>c.HeaderCoreSimilarity) <= GARBAGE_THRESHOLD)
			{
				weights[ContextType.HeaderNonCore] /= 2;

				ReduceExternalContextsWeights();
			}

			//System.Diagnostics.Trace.WriteLine(
			//	$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
			//);

			return weights;
		}
	}

	public class TuneAncestorsWeightAsRarelyChanging : IWeightsHeuristic
	{
		const double GARBAGE_THRESHOLD = 0.9;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			DefaultWeightsProvider.Init(weights, ContextType.Ancestors);

			var maxSimilarity = candidates.Max(c => c.AncestorSimilarity);

			var coeff = 1 + Math.Sqrt((GARBAGE_THRESHOLD - Math.Min(maxSimilarity, GARBAGE_THRESHOLD)) / GARBAGE_THRESHOLD);

			weights[ContextType.Ancestors] *= coeff;

			return weights;
		}
	}

	public class TuneInnerWeightAsFrequentlyChanging : IWeightsHeuristic
	{
		const double LENGTH_THRESHOLD = 50;
		const double EXCELLENT_THRESHOLD = 0.9;
		const double GARBAGE_THRESHOLD = 0.6;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if (source.InnerContext.Content.TextLength > LENGTH_THRESHOLD)
			{
				DefaultWeightsProvider.Init(weights, ContextType.Inner);

				var bestCandidates = candidates.Where(c => c.InnerSimilarity >= EXCELLENT_THRESHOLD).ToList();
				var maxSimilarity = candidates.Max(c => c.InnerSimilarity);

				var coeff = bestCandidates.Count != 1
					 ? 0.5 + 0.5 * (Math.Max(maxSimilarity, GARBAGE_THRESHOLD) - GARBAGE_THRESHOLD) / (1 - GARBAGE_THRESHOLD)
					 : 2;

				weights[ContextType.Inner] *= coeff;
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
				DefaultWeightsProvider.Init(weights, ContextType.Siblings);

				var bestCandidates = candidates.Where(c => c.SiblingsSimilarity >= EXCELLENT_THRESHOLD).ToList();
				var maxSimilarity = candidates.Max(c => c.SiblingsSimilarity);

				var coeff = bestCandidates.Count != 1
					 ? 0.5 + 0.5 * (Math.Max(maxSimilarity, GARBAGE_THRESHOLD) - GARBAGE_THRESHOLD) / (1 - GARBAGE_THRESHOLD)
					 : 2;

				weights[ContextType.Siblings] *= coeff;

				//System.Diagnostics.Trace.WriteLine(
				//	$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
				//);
			}

			return weights;
		}
	}

	public class TuneInnerWeightAccordingToLength : IWeightsHeuristic
	{
		const double LENGTH_THRESHOLD = 50;

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
