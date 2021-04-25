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
			{ContextType.Siblings, 0.5},
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
				[ContextType.Siblings] = (candidates.FirstOrDefault()?.Node.Options.GetNotUnique() ?? false)
					&& (source.SiblingsContext?.Before.All.TextLength > 0
					|| source.SiblingsContext?.After.All.TextLength > 0)
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
			/// Если отсутствует ядро заголовка
			if(weights[ContextType.HeaderCore] == 0)
			{
				return weights;
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
				/// Если их несколько и остальная часть заголовка различается, она нам поможет
				else if(maxSimilarityCandidates.Select(c=>c.HeaderNonCoreSimilarity).Distinct().Count() > 1)
				{
					weights[ContextType.HeaderNonCore] = weights[ContextType.HeaderCore] * 2;
				}
			}
			/// Если у всех кандидатов похожесть ядра небольшая, остальная часть заголовка нас только запутает
			else if(candidates.Max(c=>c.HeaderCoreSimilarity) <= GARBAGE_THRESHOLD)
			{
				weights[ContextType.HeaderNonCore] /= 2;
			}

			//System.Diagnostics.Trace.WriteLine(
			//	$"{this.GetType().Name} HCore: {weights[ContextType.HeaderCore]}; HSeq: {weights[ContextType.HeaderSequence]}; I: {weights[ContextType.Inner]}; A: {weights[ContextType.Ancestors]}"
			//);

			return weights;
		}
	}

	public class TuneAncestorsWeight : IWeightsHeuristic
	{
		const double GARBAGE_THRESHOLD = 0.7;
		const double EXCELLENT_THRESHOLD = 0.9;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if(weights[ContextType.Ancestors].HasValue) return weights;

			var distinctSimilarities = candidates.Select(c => c.AncestorSimilarity)
				.Distinct().OrderByDescending(e=>e).ToList();

			if (distinctSimilarities.Count == 1)
			{
				weights[ContextType.Ancestors] = 
					2 - 1.5 * (Math.Max(distinctSimilarities[0], GARBAGE_THRESHOLD) - GARBAGE_THRESHOLD) / (1 - GARBAGE_THRESHOLD);
			}
			else if ((1 - distinctSimilarities[1]) >= ContextFinder.SECOND_DISTANCE_GAP_COEFFICIENT * (1 - distinctSimilarities[0]))
			{
				weights[ContextType.Ancestors] = 2;
			}
			else
			{
				weights[ContextType.Ancestors] = 1;
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
			if (weights[ContextType.Inner].HasValue) return weights;

			if (candidates.Count > 0)
			{
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
						&& (ordered.Count == 1 || 1 - ordered[1].InnerSimilarity >= ContextFinder.SECOND_DISTANCE_GAP_COEFFICIENT * (1 - maxSimilarity))))
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

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if (weights[ContextType.Siblings].HasValue
				|| !(candidates.FirstOrDefault()?.Node.Options.GetNotUnique() ?? false)) return weights;

			if (candidates.Count > 0)
			{
				var ordered = candidates.OrderByDescending(c => c.SiblingsSimilarity).ToList();

				/// Количество кандидатов с хорошей похожестью соседей
				var excellentCandidatesCount = ordered.TakeWhile(c => c.SiblingsSimilarity >= EXCELLENT_THRESHOLD).Count();
				/// Максимальная похожесть
				var maxSimilarity = ordered.Select(c => c.SiblingsSimilarity).First();

				/// Если все кандидаты сильно похожи соседями на исходный элемент
				if (candidates.Count > 1 && excellentCandidatesCount == candidates.Count)
				{
					weights[ContextType.Siblings] = 0;
				}
				/// Если максимальная похожесть - единица, или самый похожий достаточно отстоит от следующего
				else if (maxSimilarity == 1 || (maxSimilarity >= EXCELLENT_THRESHOLD
					&& (ordered.Count == 1 || 1 - ordered[1].SiblingsSimilarity >= ContextFinder.SECOND_DISTANCE_GAP_COEFFICIENT * (1 - maxSimilarity))))
				{
					weights[ContextType.Siblings] = 2;
				}
				else
				{
					weights[ContextType.Siblings] = maxSimilarity < GARBAGE_THRESHOLD ? 0
						: maxSimilarity > EXCELLENT_THRESHOLD ? 1
						: (Math.Max(maxSimilarity, GARBAGE_THRESHOLD) - GARBAGE_THRESHOLD) / (EXCELLENT_THRESHOLD - GARBAGE_THRESHOLD);
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
			DefaultWeightsProvider.Init(weights, ContextType.Inner);

			weights[ContextType.Inner] *= source.InnerContext.Content.TextLength /
				Math.Max(source.InnerContext.Content.TextLength, LENGTH_THRESHOLD);

			return weights;
		}
	}

	public class TuneSiblingsWeightAccordingToLength : IWeightsHeuristic
	{
		const double LENGTH_THRESHOLD = 200;

		public Dictionary<ContextType, double?> TuneWeights(
			PointContext source,
			List<RemapCandidateInfo> candidates,
			Dictionary<ContextType, double?> weights)
		{
			if(!(candidates.FirstOrDefault()?.Node.Options.GetNotUnique() ?? false)) return weights;

			if (source.SiblingsContext != null)
			{
				var maxLength = Math.Max(
					source.SiblingsContext.Before.All.TextLength,
					source.SiblingsContext.After.All.TextLength
				);

				DefaultWeightsProvider.Init(weights, ContextType.Siblings);

				weights[ContextType.Siblings] *= maxLength / Math.Max(maxLength, LENGTH_THRESHOLD);
			}

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

	public class TuneSimilarityByNeighbours : ISimilarityHeuristic
    {
		public const double SIMILARITY_THRESHOLD = 0.8;

		public List<RemapCandidateInfo> PredictSimilarity(
            PointContext source, 
            List<RemapCandidateInfo> candidates)
        {
			var sameAreaCandidates = candidates
				.Where(c => c.SiblingsSimilarity == 1
					&& c.Similarity >= SIMILARITY_THRESHOLD)
				.ToList();

			foreach(var c in sameAreaCandidates)
			{
				c.Similarity = (c.Similarity + 1) / 2;

				if(c.Context.Line == source.Line)
				{
					c.Similarity = (c.Similarity + 1) / 2;
				}
			}

            return candidates;
        }
    }
}
