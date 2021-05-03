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
				.OrderByDescending(c=>c.HeaderCoreSimilarity)
				.ToList();
			var goodNonCore = (goodCore.Count > 0 ? goodCore : candidates)
				.Where(c => c.HeaderNonCoreSimilarity >= GOOD_SIM)
				.OrderByDescending(c => c.HeaderNonCoreSimilarity)
				.ToList();

			weights[ContextType.HeaderCore] = 2;
			weights[ContextType.HeaderNonCore] = 1;

			/// Если есть кандидаты с высокой похожестью ядра заголовка
			if (goodCore.Count > 0)
			{
				/// Если такой кандидат один, дополнительно повышаем вес ядра
				if (goodCore.Count == 1)
				{
					weights[ContextType.HeaderCore] = 2 + 2 * Math.Sqrt((goodCore[0].HeaderCoreSimilarity - GOOD_SIM) / (1 - GOOD_SIM));
				}
				else
				{
					if(ContextFinder.AreDistantEnough(goodCore[0].HeaderCoreSimilarity, goodCore[1].HeaderCoreSimilarity))
					{
						weights[ContextType.HeaderCore] = 4;
					}
				}
			}

			if (goodNonCore.Count > 0)
			{
				if (goodNonCore.Count == 1)
				{
					weights[ContextType.HeaderNonCore] = 1 + 3 * Math.Sqrt((goodNonCore[0].HeaderNonCoreSimilarity - GOOD_SIM) / (1 - GOOD_SIM));
				}
				else
				{
					if (ContextFinder.AreDistantEnough(goodNonCore[0].HeaderNonCoreSimilarity, goodNonCore[1].HeaderNonCoreSimilarity))
					{
						weights[ContextType.HeaderNonCore] = 4;
					}
				}
			}

			return weights;
		}
	}

	public class TuneAncestorsWeight : IWeightsHeuristic
	{
		const double GOOD_SIM = 0.8;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if(weights[ContextType.Ancestors].HasValue) return weights;

			var distinctSimilarities = candidates
				.Select(c => c.AncestorSimilarity)
				.Distinct()
				.OrderByDescending(e => e)
				.ToList();

			if (distinctSimilarities.Count == 1)
			{
				weights[ContextType.Ancestors] = 2 - 2 * (Math.Max(distinctSimilarities[0], GOOD_SIM) - GOOD_SIM) / (1 - GOOD_SIM);
			}
			else
			{
				weights[ContextType.Ancestors] = distinctSimilarities[0] == 1 ? 2
					: ContextFinder.AreDistantEnough(distinctSimilarities[0], distinctSimilarities[1]) ? 2 : 1;
			}

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

				var ordered = candidates.OrderByDescending(c => c.InnerSimilarity).ToList();
				/// Количество кандидатов с хорошей похожестью внутренности
				var excellentCandidatesCount = ordered.TakeWhile(c => c.InnerSimilarity >= GOOD_SIM).Count();
				/// Маскимальная похожесть
				var maxSimilarity = ordered.Select(c => c.InnerSimilarity).First();

				/// Если у всех кандидатов хорошая похожесть, внутренний контекст нам ничего не даст
				if (candidates.Count > 1 && excellentCandidatesCount == candidates.Count)
				{
					weights[ContextType.Inner] = 0.5;
				}
				/// Если максимальная похожесть - единица, или самый похожий достаточно отстоит от следующего
				else if (maxSimilarity == 1
					|| (maxSimilarity >= GOOD_SIM
						&& (ordered.Count == 1 || ContextFinder.AreDistantEnough(maxSimilarity, ordered[1].InnerSimilarity))))
				{
					weights[ContextType.Inner] = 2;
				}
				/// Иначе задаём вес тем ниже, чем ниже максимальная похожесть, поскольку данный контекст часто меняется и это нормально
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

					var ordered = candidates.OrderByDescending(c => c.SiblingsAllSimilarity).ToList();

					/// Количество кандидатов с хорошей похожестью соседей
					var excellentCandidatesCount = ordered.TakeWhile(c => c.SiblingsAllSimilarity >= GOOD_SIM).Count();
					/// Максимальная похожесть
					var maxSimilarity = ordered.Select(c => c.SiblingsAllSimilarity).First();

					/// Если все кандидаты сильно похожи соседями на исходный элемент
					if (candidates.Count > 1 && excellentCandidatesCount == candidates.Count)
					{
						weights[ContextType.SiblingsAll] = 0;
					}
					/// Если максимальная похожесть - единица, или самый похожий достаточно отстоит от следующего
					else if (maxSimilarity == 1 || (maxSimilarity >= GOOD_SIM
						&& (ordered.Count == 1 || ContextFinder.AreDistantEnough(maxSimilarity, ordered[1].SiblingsAllSimilarity))))
					{
						weights[ContextType.SiblingsAll] = 2;
					}
					else
					{
						weights[ContextType.SiblingsAll] = maxSimilarity < BAD_SIM ? 0
							: maxSimilarity > GOOD_SIM ? 1
							: (Math.Max(maxSimilarity, BAD_SIM) - BAD_SIM) / (GOOD_SIM - BAD_SIM);
					}
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

					weights[ContextType.SiblingsNearest] = similarity == 1 ? 1 : 0;
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
}
