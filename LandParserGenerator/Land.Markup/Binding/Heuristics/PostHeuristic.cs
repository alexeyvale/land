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

	public class TuneHeaderWeight : IWeightsHeuristic
	{
		const double GOOD_SIM = 0.8;

		//const double BAD_SIM = 0.4;
		//const double INDENT = 0.1;

		//public double GetWeight(double good, double usual, double bad, double max) =>
		//	max >= GOOD_SIM ? good
		//	: max >= GOOD_SIM - INDENT ? good - (good - usual) * (GOOD_SIM - max) / INDENT
		//	: max >= BAD_SIM + INDENT ? usual
		//	: max >= BAD_SIM ? bad + (usual - bad) * (max - BAD_SIM) / INDENT
		//	: bad;

		public Dictionary<ContextType, double?> TuneWeights(
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
					.Where(c => c.HeaderCoreSimilarity >= GOOD_SIM)
					.OrderByDescending(c => c.HeaderCoreSimilarity)
					.ToList()
				: null;

			/// Отбираем кандидатов с хорошей похожестью остальной части заголовка
			var goodNonCore = shouldSetNonCore
				? (goodCore?.Count > 0 ? goodCore : candidates)
					.Where(c => c.HeaderNonCoreSimilarity >= GOOD_SIM)
					.OrderByDescending(c => c.HeaderNonCoreSimilarity)
					.ToList()
				: null;

			if (shouldSetCore)
			{
				weights[ContextType.HeaderCore] = 2;
			}

			if (shouldSetNonCore)
			{
				weights[ContextType.HeaderNonCore] = 1;
			}

			/// Подстраиваем вес заголовка, если есть "хорошие" кандидаты
			if (goodCore?.Count > 0)
			{
				weights[ContextType.HeaderCore] = 2 + 2 
					* ((goodCore[0].HeaderCoreSimilarity - GOOD_SIM) / (1 - GOOD_SIM));
			}

			/// Подстраиваем вес остальной части, если есть "хорошие" кандидаты среди хороших по ядру заголовка 
			/// или, если хороших по ядру нет, среди всех кандидатов.
			/// Дополнительно проверяем, что первый и второй по похожести кандидаты не совпадают
			if (goodNonCore?.Count > 0 
				&& (goodNonCore.Count == 1 
					|| ContextFinder.AreDistantEnough(goodNonCore[0].HeaderNonCoreSimilarity, goodNonCore[1].HeaderNonCoreSimilarity)))
			{
				weights[ContextType.HeaderNonCore] = 1 + 3
					* ((goodNonCore[0].HeaderNonCoreSimilarity - GOOD_SIM) / (1 - GOOD_SIM));
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
			if (weights[ContextType.Ancestors].HasValue) return weights;

			var distinctSimilarities = candidates
				.Select(c => c.AncestorSimilarity)
				.Distinct()
				.OrderByDescending(e => e)
				.ToList();

			weights[ContextType.Ancestors] = distinctSimilarities.Count == 1 
				? 0.5 : 0.5 + 0.5 * Math.Min(1, distinctSimilarities[0] / GOOD_SIM);

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

				candidates = candidates.OrderByDescending(c => c.InnerSimilarity).ToList();

				if (candidates.Count > 1 && !ContextFinder.AreDistantEnough(candidates[0].InnerSimilarity, candidates[1].InnerSimilarity))
				{
					weights[ContextType.Inner] = 0.5;
				}
				else
				{
					weights[ContextType.Inner] = candidates[0].InnerSimilarity < BAD_SIM ? 0.5
						: candidates[0].InnerSimilarity > GOOD_SIM ? 2
						: 0.5 + 1.5 * (Math.Max(candidates[0].InnerSimilarity, BAD_SIM) - BAD_SIM) / (GOOD_SIM - BAD_SIM);
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
