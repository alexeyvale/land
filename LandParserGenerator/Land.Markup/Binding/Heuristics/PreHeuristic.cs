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

		RemapCandidateInfo GetSameElement_old(
			PointContext point,
			List<RemapCandidateInfo> candidates);
	}

	public class ContextsEqualityHeuristic: IPreHeuristic
	{
		private static readonly Func<PointContext, PointContext, bool> HeaderCorePredicate = (a, b) =>
			a.HeaderContext.Core.SequenceEqual(b.HeaderContext.Core);
		private static readonly Func<PointContext, PointContext, bool> HeaderSequencePredicate = (a, b) =>
			a.HeaderContext.Sequence.SequenceEqual(b.HeaderContext.Sequence);
		private static readonly Func<PointContext, PointContext, bool> InnerPredicate = (a, b) =>
			a.InnerContext.Content.Text == b.InnerContext.Content.Text
				&& (a.InnerContext.Content.Hash?.SequenceEqual(b.InnerContext.Content.Hash) ?? true);
		private static readonly Func<PointContext, PointContext, bool> AncestorsSequencePredicate = (a, b) =>
			a.AncestorsContext.SequenceEqual(b.AncestorsContext);

		public RemapCandidateInfo GetSameElement(
			PointContext point,
			List<RemapCandidateInfo> candidates)
		{
			var basePredicates = new Func<PointContext, PointContext, bool>[] 
			{ 
				HeaderCorePredicate, HeaderSequencePredicate, InnerPredicate 
			};

			/// Базовый предикат, которому должны удовлетворять похожие элементы,
			/// выбираем, основываясь на том, как вычисляли контекст ближайших
			var baseIdx = point.HeaderContext.Core.Count > 0
				? 0 : point.HeaderContext.NonCore.Count > 0
					? 1 : point.InnerContext.Content?.TextLength > 0
						? 2 : (int?)null;

			if (!baseIdx.HasValue)
			{
				return null;
			}

			/// Проверяем, были ли в исходном файле элементы,
			/// совпадающие с искомым при легковесном сравнении
			var wereAlmostSame = point.ClosestContext != null
				? point.ClosestContext.Where(e => AncestorsSequencePredicate(e, point)).ToList()
				: new List<PointContext>();

			/// Если были совпадающие для более простого базового предиката,
			/// используем более сложный
			for (var i = baseIdx.Value; i < basePredicates.Length; ++i)
			{
				wereAlmostSame = wereAlmostSame
					.Where(e => basePredicates[i](e, point))
					.ToList();

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
						&& AncestorsSequencePredicate(c.Context, point))
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
				}
			}

			return null;
		}

		public RemapCandidateInfo GetSameElement_old(
			PointContext point,
			List<RemapCandidateInfo> candidates)
		{
			if (point.HeaderContext.Sequence.Count == 0)
			{
				return null;
			}

			/// Проверяем, были ли в исходном файле элементы,
			/// совпадающие с искомым при легковесном сравнении
			var wereAlmostSame = point.ClosestContext != null
				? point.ClosestContext.Where(e => AncestorsSequencePredicate(e, point)).ToList()
				: new List<PointContext>();

			wereAlmostSame = wereAlmostSame
				.Where(e => HeaderSequencePredicate(e, point))
				.ToList();

			if (wereAlmostSame.Count == 0)
			{
				var similarCandidates = candidates
					.Where(c => HeaderSequencePredicate(point, c.Context)
						&& AncestorsSequencePredicate(c.Context, point))
					.ToList();

				if (similarCandidates.Count <= 1)
				{
					return similarCandidates.FirstOrDefault();
				}
			}

			return null;
		}
	}
}
