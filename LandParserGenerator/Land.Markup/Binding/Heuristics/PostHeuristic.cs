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

	public abstract class MinMaxTuningHeurisic : IWeightsHeuristic
	{
		public abstract Dictionary<ContextType, double?> TuneWeights(PointContext source, List<RemapCandidateInfo> candidates, Dictionary<ContextType, double?> weights);

		protected double GetWeight(double minW, double maxW, double minSim, double maxSim, double bestSim) =>
			minW + (maxW - minW) * Math.Max(0, Math.Min(1, (bestSim - minSim)/(maxSim - minSim)));
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
				[ContextType.SiblingsAll] = (candidates.FirstOrDefault()?.Node.Options.GetNotUnique() ?? false),
				[ContextType.SiblingsNearest] = true
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

	public class TuneHeaderWeight : MinMaxTuningHeurisic, IWeightsHeuristic
	{
		const double MIN_W_CORE = 2;
		const double MIN_W_NON_CORE = 1;

		const double MAX_W_CORE = 4;
		const double MAX_W_NON_CORE = 4;

		const double MIN_SIM = 0.8;
		const double MAX_SIM = 1;

		public override Dictionary<ContextType, double?> TuneWeights(
				PointContext source,
				List<RemapCandidateInfo> candidates,
				Dictionary<ContextType, double?> weights)
		{
			var shouldSetCore = !weights[ContextType.HeaderCore].HasValue;
			var shouldSetNonCore = !weights[ContextType.HeaderNonCore].HasValue;

			if (!shouldSetCore && !shouldSetNonCore) return weights;

			if (candidates.Count == 0) return weights;

			/// Отбираем кандидатов с хорошей похожестью ядра
			var goodCore = shouldSetCore
				? candidates
					.Where(c => c.HeaderCoreSimilarity >= MIN_SIM)
					.OrderByDescending(c => c.HeaderCoreSimilarity)
					.ToList()
				: null;

			/// Отбираем кандидатов с хорошей похожестью остальной части заголовка
			var goodNonCore = shouldSetNonCore
				? (goodCore?.Count > 0 ? goodCore : candidates)
					.Where(c => c.HeaderNonCoreSimilarity >= MIN_SIM)
					.OrderByDescending(c => c.HeaderNonCoreSimilarity)
					.ToList()
				: null;

			if (shouldSetCore)
			{
				weights[ContextType.HeaderCore] = MIN_W_CORE;
			}

			if (shouldSetNonCore)
			{
				weights[ContextType.HeaderNonCore] = MIN_W_NON_CORE;
			}

			/// Подстраиваем вес заголовка, если есть "хорошие" кандидаты
			if (goodCore?.Count > 0)
			{
				weights[ContextType.HeaderCore] = 
					GetWeight(MIN_W_CORE, MAX_W_CORE, MIN_SIM, MAX_SIM, goodCore[0].HeaderCoreSimilarity);
			}

			/// Подстраиваем вес остальной части, если есть "хорошие" кандидаты среди хороших по ядру заголовка 
			/// или, если хороших по ядру нет, среди всех кандидатов.
			/// Дополнительно проверяем, что первый и второй по похожести кандидаты не совпадают
			if (goodNonCore?.Count > 0 
				&& (goodNonCore.Count == 1 
					|| ContextFinder.AreDistantEnough(goodNonCore[0].HeaderNonCoreSimilarity, goodNonCore[1].HeaderNonCoreSimilarity)))
			{
				weights[ContextType.HeaderNonCore] =
					GetWeight(MIN_W_NON_CORE, MAX_W_NON_CORE, MIN_SIM, MAX_SIM, goodNonCore[0].HeaderNonCoreSimilarity);
			}

			return weights;
		}
	}

	public class TuneAncestorsWeight : MinMaxTuningHeurisic, IWeightsHeuristic
	{
		const double MIN_W = 0.5;
		const double MAX_W = 1;
		const double MIN_SIM = 0;
		const double MAX_SIM = 0.8;

		public override Dictionary<ContextType, double?> TuneWeights(
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
				? MIN_W : GetWeight(MIN_W, MAX_W, MIN_SIM, MAX_SIM, distinctSimilarities[0]);

			return weights;
		}
	}

	public class TuneInnerWeightAsFrequentlyChanging : MinMaxTuningHeurisic, IWeightsHeuristic
	{
		const double MAX_W = 2;
		const double MIN_W = 0.5;
		const double MAX_SIM = 0.9;
		const double MIN_SIM = 0.6;

		public override Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if (candidates.Count > 0)
			{
				if (weights[ContextType.Inner].HasValue) return weights;

				candidates = candidates.OrderByDescending(c => c.InnerSimilarity).ToList();

				weights[ContextType.Inner] =
					GetWeight(MIN_W, MAX_W, MIN_SIM, MAX_SIM, candidates[0].InnerSimilarity);
			}

			return weights;
		}
	}

	public class TuneSiblingsAllWeightAsFrequentlyChanging : MinMaxTuningHeurisic, IWeightsHeuristic
	{
		const double MAX_W = 1;
		const double MIN_W = 0;
		const double MAX_SIM = 0.8;
		const double MIN_SIM = 0.6;

		public override Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if (candidates.Count > 0)
			{
				if (candidates.FirstOrDefault()?.Node.Options.GetNotUnique() ?? false)
				{
					if (weights[ContextType.SiblingsAll].HasValue) return weights;

					/// Отбираем кандидатов с наилучшей похожестью предков
					var bestAncestorCandidates = candidates
						.GroupBy(c => c.Context.AncestorsContext)
						.OrderByDescending(g => g.First().AncestorSimilarity)
						.First()
						.ToList();
					/// Среди них отбираем наиболее похожих
					var bestCandidates = bestAncestorCandidates
						.GroupBy(c => c.HeaderCoreSimilarity * (weights[ContextType.HeaderCore] ?? DefaultWeightsProvider.Get(ContextType.HeaderCore)) 
							+ c.HeaderNonCoreSimilarity * (weights[ContextType.HeaderNonCore] ?? DefaultWeightsProvider.Get(ContextType.HeaderNonCore))
							+ c.InnerSimilarity * (weights[ContextType.Inner] ?? DefaultWeightsProvider.Get(ContextType.Inner)))
						.OrderByDescending(g => g.Key)
						.First()
						.ToList();
					
					if (bestCandidates.Count > 1)
					{
						var maxSimilarity = bestCandidates.Max(c => c.SiblingsAllSimilarity);

						weights[ContextType.SiblingsAll] = 
							GetWeight(MIN_W, MAX_W, MIN_SIM, MAX_SIM, maxSimilarity);
					}
					else
					{
						weights[ContextType.SiblingsAll] = 0;
					}
				}
			}

			return weights;
		}
	}

	public class TuneSiblingsNearestWeight : MinMaxTuningHeurisic, IWeightsHeuristic
	{
		const double MAX_W = 2;
		const double MIN_W = 0;
		const double MIN_SIM = 0.9;
		const double MAX_SIM = 1;

		public override Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if (candidates.Count > 0)
			{
				if (weights[ContextType.SiblingsNearest].HasValue) return weights;

				var maxSimilarity = candidates.Max(c => c.SiblingsNearestSimilarity);

				weights[ContextType.SiblingsNearest] = 
					GetWeight(MIN_W, MAX_W, MIN_SIM, MAX_SIM, maxSimilarity);
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
