using Land.Markup.CoreExtension;
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
			{ContextType.SiblingsAll, 0.5},
			{ContextType.SiblingsNearest, 0.5}
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
				[ContextType.SiblingsAll] = (candidates.FirstOrDefault()?.Node.Options.GetNotUnique() ?? false)
					&& (source.SiblingsContext?.Before.All.TextLength > 0
					|| source.SiblingsContext?.After.All.TextLength > 0),
				[ContextType.SiblingsNearest] = !(candidates.FirstOrDefault()?.Node.Options.GetNotUnique() ?? false)
			};

			foreach (var kvp in existenceFlags)
			{
				if (!kvp.Value)
				{
					weights[kvp.Key] = 0;
				}
			}

			return weights;
		}
	}

	public class TuneHeaderWeightIfSimilar : IWeightsHeuristic
	{
		const double GOOD_SIM = 0.8;

		public Dictionary<ContextType, double?> TuneWeights(
				PointContext source,
				List<RemapCandidateInfo> candidates,
				Dictionary<ContextType, double?> weights)
		{
			if (weights[ContextType.HeaderCore].HasValue
				|| weights[ContextType.HeaderNonCore].HasValue) return weights;

			if (candidates.Count == 0) return weights;

			var goodCore = candidates
				.Where(c => c.HeaderCoreSimilarity >= GOOD_SIM)
				.OrderByDescending(c => c.HeaderCoreSimilarity)
				.ToList();
			var goodNonCore = (goodCore.Count > 0 ? goodCore : candidates)
				.Where(c => c.HeaderNonCoreSimilarity >= GOOD_SIM)
				.OrderByDescending(c => c.HeaderNonCoreSimilarity)
				.ToList();

			weights[ContextType.HeaderCore] = 2;
			weights[ContextType.HeaderNonCore] = 1;

			if (goodCore.Count > 0)
			{
				weights[ContextType.HeaderCore] = 2 + 2 
					* ((goodCore[0].HeaderCoreSimilarity - GOOD_SIM) / (1 - GOOD_SIM));
			}

			var maxNonCoreWeight = goodCore.Count > 0 ? 4 : 2;

			if (goodNonCore.Count == 1)
			{
				weights[ContextType.HeaderNonCore] = 1 + (maxNonCoreWeight - 1) 
					* ((goodNonCore[0].HeaderNonCoreSimilarity - GOOD_SIM) / (1 - GOOD_SIM));
			}

			return weights;
		}
	}

	public class TuneAncestorsWeight : IWeightsHeuristic
	{
		const double GOOD_SIM = 0.7;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if (weights[ContextType.Ancestors].HasValue) return weights;

			var distinctSimilarities = candidates
				.Select(c => c.AncestorSimilarity)
				.Distinct()
				.OrderByDescending(e => e)
				.ToList();

			weights[ContextType.Ancestors] = distinctSimilarities.Count == 1
				? 1 - Math.Max(0, (distinctSimilarities[0] - GOOD_SIM) / (1 - GOOD_SIM))
				: 2;

			return weights;
		}
	}

	public class TuneInnerWeightAsFrequentlyChanging : IWeightsHeuristic
	{
		const double GOOD_SIM = 0.9;
		const double BAD_SIM = 0.6;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if (candidates.Count > 0)
			{
				if (weights[ContextType.Inner].HasValue) return weights;

				var goodCandidates = candidates.Where(c => c.InnerSimilarity >= GOOD_SIM).ToList();
				var maxSimilarity = candidates.Max(c => c.InnerSimilarity);

				if (candidates.Count > 1 && goodCandidates.Count == candidates.Count)
				{
					weights[ContextType.Inner] = 0.5;
				}
				else
				{
					weights[ContextType.Inner] = maxSimilarity < BAD_SIM ? 0.5
						: maxSimilarity > GOOD_SIM ? 2
						: 0.5 + 1.5 * (Math.Max(maxSimilarity, BAD_SIM) - BAD_SIM) / (GOOD_SIM - BAD_SIM);
				}
			}

			return weights;
		}
	}

	public class TuneSiblingsAllWeightAsFrequentlyChanging : IWeightsHeuristic
	{
		const double GOOD_SIM = 0.9;
		const double BAD_SIM = 0.7;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if (candidates.Count > 0)
			{
				if (candidates.FirstOrDefault()?.Node.Options.GetNotUnique() ?? false)
				{
					if (weights[ContextType.SiblingsAll].HasValue) return weights;

					var maxSimilarity = candidates.Max(c => c.SiblingsAllSimilarity);

					weights[ContextType.SiblingsAll] = maxSimilarity < BAD_SIM ? 0
						: maxSimilarity > GOOD_SIM ? 1
						: (Math.Max(maxSimilarity, BAD_SIM) - BAD_SIM) / (GOOD_SIM - BAD_SIM);
				}
				else
				{ }
			}

			return weights;
		}
	}

	public class TuneSiblingsNearestWeight : IWeightsHeuristic
	{
		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if (candidates.Count > 0)
			{
				if (candidates.FirstOrDefault()?.Node.Options.GetNotUnique() ?? false)
				{ }
				else
				{
					if (weights[ContextType.SiblingsNearest].HasValue) return weights;

					var similarity = candidates.Max(c => c.SiblingsNearestSimilarity);

					weights[ContextType.SiblingsNearest] = similarity == 1 ? 2 : 0;
				}
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
			if (weights[ContextType.Inner] == 0) return weights;

			DefaultWeightsProvider.Init(weights, ContextType.Inner);

			weights[ContextType.Inner] *= source.InnerContext.Content.TextLength /
				Math.Max(source.InnerContext.Content.TextLength, LENGTH_THRESHOLD);

			return weights;
		}
	}

	public class TuneSiblingsAllWeightAccordingToLength : IWeightsHeuristic
	{
		const double LENGTH_THRESHOLD = 200;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if (weights[ContextType.SiblingsAll] == 0) return weights;

			DefaultWeightsProvider.Init(weights, ContextType.SiblingsAll);

			var maxLength = Math.Max(
				source.SiblingsContext.Before.All.TextLength,
				source.SiblingsContext.After.All.TextLength
			);

			weights[ContextType.SiblingsAll] *= maxLength / Math.Max(maxLength, LENGTH_THRESHOLD);

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
			foreach (var val in Enum.GetValues(typeof(ContextType)).Cast<ContextType>())
			{
				if (weights[val] == null)
				{
					weights[val] = DefaultWeightsProvider.Get(val);
				}
			}

			return weights;
		}
	}
}
