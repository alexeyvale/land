using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.Markup.Binding
{
	public interface IPreHeuristic
	{
		RemapCandidateInfo GetSameElement(
			PointContext point,
			List<RemapCandidateInfo> candidates);
	}

	public class ProgrammingLanguageHeuristic: IPreHeuristic
	{
		private static readonly Func<PointContext, PointContext, bool> HeaderCorePredicate = (a, b) =>
			a.HeaderContext.Core.SequenceEqual(b.HeaderContext.Core);
		private static readonly Func<PointContext, PointContext, bool> HeaderSequencePredicate = (a, b) =>
			a.HeaderContext.Equals(b.HeaderContext);
		private static readonly Func<PointContext, PointContext, bool> InnerPredicate = (a, b) =>
			a.InnerContext.Content.Text == b.InnerContext.Content.Text
				&& (a.InnerContext.Content.Hash?.SequenceEqual(b.InnerContext.Content.Hash) ?? true);
		private static readonly Func<PointContext, PointContext, bool> AncestorsCorePredicate = (a, b) =>
			a.AncestorsContext.SequenceEqual(b.AncestorsContext, AncestorsCoreComparer);
		private static readonly Func<PointContext, PointContext, bool> AncestorsSequencePredicate = (a, b) =>
			a.AncestorsContext.SequenceEqual(b.AncestorsContext);

		private static readonly AncestorsCoreEqualityComparer AncestorsCoreComparer = new AncestorsCoreEqualityComparer();

		private class AncestorsCoreEqualityComparer : IEqualityComparer<AncestorsContextElement>
		{
			public bool Equals(AncestorsContextElement x, AncestorsContextElement y)
			{
				return x.HeaderContext.Core.Count > 0 && x.HeaderContext.EqualsByCore(y.HeaderContext)
					|| x.HeaderContext.Equals(y.HeaderContext);
			}

			public int GetHashCode(AncestorsContextElement obj)
			{
				throw new NotImplementedException();
			}
		}

		public RemapCandidateInfo GetSameElement(
			PointContext point,
			List<RemapCandidateInfo> candidates)
		{
			if(point.ClosestContext == null)
			{
				return null;
			}

			var basePredicates = new Func<PointContext, PointContext, bool>[] 
			{ 
				HeaderCorePredicate, HeaderSequencePredicate, InnerPredicate 
			};
			var ancestorsPredicates = new Func<PointContext, PointContext, bool>[]
			{
				AncestorsCorePredicate, AncestorsSequencePredicate
			};

			var ancestorsIdx = 0;
			/// Базовый предикат, которому должны удовлетворять похожие элементы,
			/// выбираем, основываясь на том, как вычисляли контекст ближайших
			var baseIdx = point.HeaderContext.Core.Count > 0
				? 0 : point.HeaderContext.Sequence.Count > 0
					? 1 : point.InnerContext.Content?.TextLength > 0
						? 2 : (int?)null;

			if (!baseIdx.HasValue)
			{
				return null;
			}

			/// Проверяем, были ли в исходном файле элементы,
			/// совпадающие с искомым при легковесном сравнении
			var wereAlmostSame = point.ClosestContext
				.Where(e => ancestorsPredicates[0](e, point))
				.ToList();

			/// Если были совпадающие для более простого базового предиката,
			/// используем более сложный
			for (var i = baseIdx.Value; i < basePredicates.Length; ++i)
			{
				wereAlmostSame = wereAlmostSame
					.Where(e => basePredicates[i](e, point))
					.ToList();

				if (wereAlmostSame.Count > 0)
				{
					for (var j = 1; j < ancestorsPredicates.Length; ++j)
					{
						var strictComparison = wereAlmostSame
							.Where(e => ancestorsPredicates[j](e, point))
							.ToList();

						if(strictComparison.Count == 0)
						{
							wereAlmostSame = strictComparison;
							ancestorsIdx = j;

							break;
						}
					}
				}

				if(wereAlmostSame.Count == 0)
				{
					baseIdx = i;
					break;
				}
			}

			if (wereAlmostSame.Count == 0)
			{
				var similarCandidates = candidates
					.Where(c => basePredicates.Take(baseIdx.Value + 1).All(p => p(point, c.Context)) 
						&& ancestorsPredicates[ancestorsIdx](c.Context, point))
					.ToList();

				if(similarCandidates.Count <= 1)
				{
					return similarCandidates.FirstOrDefault();
				}

				for (var i = baseIdx.Value + 1; i < basePredicates.Length; ++i)
				{
					similarCandidates = similarCandidates
						.Where(c => basePredicates[i](c.Context, point))
						.ToList();

					if (similarCandidates.Count <= 1)
					{
						return similarCandidates.FirstOrDefault();
					}

					for (var j = ancestorsIdx + 1; j < ancestorsPredicates.Length; ++j)
					{
						var strictComparison = similarCandidates
							.Where(c => ancestorsPredicates[j](c.Context, point))
							.ToList();

						if (strictComparison.Count <= 1)
						{
							return similarCandidates.FirstOrDefault();
						}
					}
				}
			}

			return null;
		}
	}
}
