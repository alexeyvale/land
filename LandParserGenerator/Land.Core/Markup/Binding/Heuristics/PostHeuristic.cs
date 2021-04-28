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
			{ContextType.HeaderCore,  2},
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
		const double BAD_SIM = 0.4;
		const double GOOD_SIM = 0.9;
		const double INDENT = 0.1;

		public Dictionary<ContextType, double?> TuneWeights(
				PointContext source,
				List<RemapCandidateInfo> candidates,
				Dictionary<ContextType, double?> weights)
		{
			if (weights[ContextType.HeaderCore].HasValue
				|| weights[ContextType.HeaderNonCore].HasValue) return weights;

			if (candidates.Count == 0) return weights;

			var orderedByCore = candidates
				.OrderByDescending(c => c.HeaderCoreSimilarity)
				.ToList();
			var maxCoreSim = orderedByCore[0].HeaderCoreSimilarity;

			/// Повышаем вес ядра, если есть кандидаты с высокой его похожестью
			weights[ContextType.HeaderCore] = 4 - 2 * Math.Max(0, Math.Min(1, (GOOD_SIM - maxCoreSim) /INDENT));
			/// При сильно непохожем ядре бессмысленно рассматривать остальной заголовок
			weights[ContextType.HeaderNonCore] = Math.Max(0, Math.Min(1, (maxCoreSim - BAD_SIM) / INDENT));

			/// Уменьшаем вес ядра, если по нему кандидаты плохо разделяются
			if (candidates.Count > 1)
			{
				var coreDifferenceCoeff = maxCoreSim < 1 ? Math.Min(2, (1 - orderedByCore[1].HeaderCoreSimilarity) / (1 - maxCoreSim)) 
					: orderedByCore[1].HeaderCoreSimilarity == maxCoreSim ? 1 : 2;

				weights[ContextType.HeaderCore] /= 3 - coreDifferenceCoeff;

				/// Если у кандидатов различается остальная часть заголовка, увеливичиваем её вес
				if (orderedByCore[1].HeaderNonCoreSimilarity != orderedByCore[0].HeaderNonCoreSimilarity)
				{
					weights[ContextType.HeaderNonCore] *= 4 - 1.5 * coreDifferenceCoeff;
				}
			}

			return weights;
		}
	}

	public class TuneAncestorsWeight : IWeightsHeuristic
	{
		const double GARBAGE_THRESHOLD = 0.8;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if(weights[ContextType.Ancestors].HasValue) return weights;

			var distinctSimilarities = candidates.Select(c => c.AncestorSimilarity)
				.Distinct()
				.OrderByDescending(e=>e)
				.ToList();

			if (distinctSimilarities.Count == 1)
			{
				weights[ContextType.Ancestors] =
					2 - 1.5 * (Math.Max(distinctSimilarities[0], GARBAGE_THRESHOLD) - GARBAGE_THRESHOLD) / (1 - GARBAGE_THRESHOLD);
			}
			else
			{
				if (distinctSimilarities[0] == 1)
				{
					weights[ContextType.Ancestors] = 2;
				}
				else
				{
					weights[ContextType.Ancestors] = Math.Min(2, (1 - distinctSimilarities[1]) / (1 - distinctSimilarities[0]));
				}
			}

			return weights;
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
			if (candidates.Count > 0)
			{
				if (weights[ContextType.Inner].HasValue) return weights;

				var ordered = candidates.OrderByDescending(c => c.InnerSimilarity).ToList();
				/// Количество кандидатов с хорошей похожестью внутренности
				var excellentCandidatesCount = ordered.TakeWhile(c => c.InnerSimilarity >= EXCELLENT_THRESHOLD).Count();
				/// Маскимальная похожесть
				var maxSimilarity = ordered.Select(c => c.InnerSimilarity).First();

				/// Если у всех кандидатов хорошая похожесть, внутренний контекст нам ничего не даст
				if (candidates.Count > 1 && excellentCandidatesCount == candidates.Count)
				{
					weights[ContextType.Inner] = 0.5;
				}
				/// Если максимальная похожесть - единица, или самый похожий достаточно отстоит от следующего
				else if (maxSimilarity == 1
					|| (maxSimilarity >= EXCELLENT_THRESHOLD
						&& (ordered.Count == 1 || ContextFinder.AreDistantEnough(maxSimilarity, ordered[1].InnerSimilarity))))
				{
					weights[ContextType.Inner] = 2;
				}
				/// Иначе задаём вес тем ниже, чем ниже максимальная похожесть, поскольку данный контекст часто меняется и это нормально
				else
				{
					weights[ContextType.Inner] = maxSimilarity < GARBAGE_THRESHOLD ? 0.5
						: maxSimilarity > EXCELLENT_THRESHOLD ? 1
						: 0.5 + 0.5 * (Math.Max(maxSimilarity, GARBAGE_THRESHOLD) - GARBAGE_THRESHOLD) / (EXCELLENT_THRESHOLD - GARBAGE_THRESHOLD);
				}
			}

			return weights;
		}
	}

	public class TuneSiblingsWeightAsFrequentlyChanging : IWeightsHeuristic
	{
		const double EXCELLENT_THRESHOLD = 0.9;
		const double GARBAGE_THRESHOLD = 0.7;

		public static double ComputeTotalSimilarity(
			RemapCandidateInfo c,
			Dictionary<ContextType, double?> weights)
		{
			return ((weights[ContextType.Ancestors] ?? DefaultWeightsProvider.Get(ContextType.Ancestors)) * c.AncestorSimilarity
				+ (weights[ContextType.Inner] ?? DefaultWeightsProvider.Get(ContextType.Inner)) * c.InnerSimilarity
				+ (weights[ContextType.HeaderNonCore] ?? DefaultWeightsProvider.Get(ContextType.HeaderNonCore)) * c.HeaderNonCoreSimilarity
				+ (weights[ContextType.HeaderCore] ?? DefaultWeightsProvider.Get(ContextType.HeaderCore)) * c.HeaderCoreSimilarity)
				/ ((weights[ContextType.Ancestors] ?? DefaultWeightsProvider.Get(ContextType.Ancestors))
					+ (weights[ContextType.Inner] ?? DefaultWeightsProvider.Get(ContextType.Inner))
					+ (weights[ContextType.HeaderNonCore] ?? DefaultWeightsProvider.Get(ContextType.HeaderNonCore))
					+ (weights[ContextType.HeaderCore] ?? DefaultWeightsProvider.Get(ContextType.HeaderCore)));
		}

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

					var ordered = candidates.OrderByDescending(c => c.SiblingsAllSimilarity).ToList();

					/// Количество кандидатов с хорошей похожестью соседей
					var excellentCandidatesCount = ordered.TakeWhile(c => c.SiblingsAllSimilarity >= EXCELLENT_THRESHOLD).Count();
					/// Максимальная похожесть
					var maxSimilarity = ordered.Select(c => c.SiblingsAllSimilarity).First();

					/// Если все кандидаты сильно похожи соседями на исходный элемент
					if (candidates.Count > 1 && excellentCandidatesCount == candidates.Count)
					{
						weights[ContextType.SiblingsAll] = 0;
					}
					/// Если максимальная похожесть - единица, или самый похожий достаточно отстоит от следующего
					else if (maxSimilarity == 1 || (maxSimilarity >= EXCELLENT_THRESHOLD
						&& (ordered.Count == 1 || ContextFinder.AreDistantEnough(maxSimilarity, ordered[1].SiblingsAllSimilarity))))
					{
						weights[ContextType.SiblingsAll] = 2;
					}
					else
					{
						weights[ContextType.SiblingsAll] = maxSimilarity < GARBAGE_THRESHOLD ? 0
							: maxSimilarity > EXCELLENT_THRESHOLD ? 1
							: (Math.Max(maxSimilarity, GARBAGE_THRESHOLD) - GARBAGE_THRESHOLD) / (EXCELLENT_THRESHOLD - GARBAGE_THRESHOLD);
					}
				}
				else
				{
					if (weights[ContextType.SiblingsNearest].HasValue) return weights;

					var bestLocation = candidates
						.Where(c => c.SiblingsNearestSimilarity == 1)
						.ToList();

					if (bestLocation.Count > 0)
					{
						var maxTotalSimilarity = bestLocation
							.Max(c => ComputeTotalSimilarity(c, weights));

						weights[ContextType.SiblingsNearest] = maxTotalSimilarity <= 0.6 ? 0
							: maxTotalSimilarity >= 0.8 ? 2
								: 2 - 2 * (maxTotalSimilarity - 0.6) / 0.2;
					}
					else
					{
						weights[ContextType.SiblingsNearest] = 0;
					}
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
			if(weights[ContextType.SiblingsAll] == 0) return weights;

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

	//public class TuneSimilarityByNeighbours : ISimilarityHeuristic
 //   {
	//	public const double SIMILARITY_THRESHOLD = 0.8;

	//	public List<RemapCandidateInfo> PredictSimilarity(
 //           PointContext source, 
 //           List<RemapCandidateInfo> candidates)
 //       {
	//		var sameAreaCandidates = candidates
	//			.Where(c => c.SiblingsSimilarity == 1
	//				&& c.Similarity >= SIMILARITY_THRESHOLD)
	//			.ToList();

	//		foreach(var c in sameAreaCandidates)
	//		{
	//			c.Similarity = (c.Similarity + 1) / 2;

	//			if(c.Context.Line == source.Line)
	//			{
	//				c.Similarity = (c.Similarity + 1) / 2;
	//			}
	//		}

 //           return candidates;
 //       }
 //   }
}
