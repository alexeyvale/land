using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Land.Markup.CoreExtension;
using Land.Core.Parsing.Tree;

namespace Land.Markup.Binding
{
	public class BasicContextFinder : IContextFinder
	{
		public class InnerContextElementComparer : IEqualityComparer<InnerContextElement>
		{
			public bool Equals(InnerContextElement a, InnerContextElement b)
			{
				return a.HeaderContext.Select(h => String.Join("", h.Value)).SequenceEqual(
					b.HeaderContext.Select(h => String.Join("", h.Value)));
			}

			public int GetHashCode(InnerContextElement obj)
			{
				return obj.HeaderContext.Count;
			}
		}

		private const int INNER_CONTEXT_LIMIT = 10;

		/// <summary>
		/// Поиск узлов дерева, соответствующих точкам привязки
		/// </summary>
		/// <param name="points">Точки привязки, сгруппированные по типу связанного с ними узла</param>
		/// <param name="candidateNodes">Узлы дерева, среди которых нужно найти соответствующие точкам, также сгруппированные по типу</param>
		/// <returns></returns>
		public Dictionary<ConcernPoint, List<RemapCandidateInfo>> Find(
			Dictionary<string, List<ConcernPoint>> points,
			Dictionary<string, List<Node>> candidateNodes,
			ParsedFile candidateFileInfo
		)
		{
			var result = new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();

			foreach (var typePointsPair in points)
			{
				foreach (var point in typePointsPair.Value)
				{
					var candidates = candidateNodes.ContainsKey(typePointsPair.Key)
						? candidateNodes[typePointsPair.Key].Select(node =>
							new RemapCandidateInfo()
							{
								Node = node,
								Context = new PointContext()
								{
									FileName = candidateFileInfo.Name,
									NodeType = node.Type
								}
							}).ToList()
						: new List<RemapCandidateInfo>();

					foreach (var candidate in candidates)
					{
						candidate.Context.HeaderContext = PointContext.GetHeaderContext(candidate.Node);
						candidate.HeaderSimilarity = Levenshtein(point.Context.HeaderContext, candidate.Context.HeaderContext);
					}

					foreach (var candidate in candidates)
					{
						candidate.Context.AncestorsContext = PointContext.GetAncestorsContext(candidate.Node);
						candidate.AncestorSimilarity = Levenshtein(point.Context.AncestorsContext, candidate.Context.AncestorsContext);
					}

					foreach (var candidate in candidates)
					{
						var inner = PointContext.GetInnerContext(
							new ParsedFile() { Name = candidateFileInfo.Name, Text = candidateFileInfo.Text, Root = candidate.Node }
						);
						candidate.Context.InnerContext = inner.Item1;
						candidate.Context.InnerContextElement = inner.Item2;

						candidate.InnerSimilarity = EvalSimilarity(
							point.Context.InnerContext.Take(INNER_CONTEXT_LIMIT).ToList(), 
							candidate.Context.InnerContext.Take(INNER_CONTEXT_LIMIT).ToList()
						);
					}

					candidates.ForEach(c => c.Similarity = 
						(3 * c.HeaderSimilarity + 2 * c.AncestorSimilarity + c.InnerSimilarity) / 6);

					result[point] = candidates.OrderByDescending(c => c.Similarity).ToList();

					var first = result[point].FirstOrDefault();
					var second = result[point].Skip(1).FirstOrDefault();

					if (first != null)
					{
						first.IsAuto = first.Similarity >= 0.6
							&& (second == null || 1 - second.Similarity >= (1 - first.Similarity) * 1.5);
					}
				}
			}

			return result;
		}
		
		public List<RemapCandidateInfo> Find(ConcernPoint point, ParsedFile targetInfo)
		{
			var visitor = new GroupNodesByTypeVisitor(new List<string> { point.Context.NodeType });
			targetInfo.Root.Accept(visitor);

			return Find(
				new Dictionary<string, List<ConcernPoint>> { { point.Context.NodeType, new List<ConcernPoint> { point } } },
				visitor.Grouped, targetInfo
			)[point];
		}

		private static double Levenshtein<T>(IEnumerable<T> a, IEnumerable<T> b)
		{
			if (a.Count() == 0 ^ b.Count() == 0)
				return 0;
			if (a.Count() == 0 && b.Count() == 0)
				return 1;

			var denominator = Math.Max(a.Count(), b.Count());

			/// Сразу отбрасываем общие префиксы и суффиксы
			var commonPrefixLength = 0;
			while (commonPrefixLength < a.Count() && commonPrefixLength < b.Count()
				&& a.ElementAt(commonPrefixLength).Equals(b.ElementAt(commonPrefixLength)))
				++commonPrefixLength;
			a = a.Skip(commonPrefixLength).ToList();
			b = b.Skip(commonPrefixLength).ToList();

			var commonSuffixLength = 0;
			while (commonSuffixLength < a.Count() && commonSuffixLength < b.Count()
				&& a.ElementAt(a.Count() - 1 - commonSuffixLength).Equals(b.ElementAt(b.Count() - 1 - commonSuffixLength)))
				++commonSuffixLength;
			a = a.Take(a.Count() - commonSuffixLength).ToList();
			b = b.Take(b.Count() - commonSuffixLength).ToList();

			if (a.Count() == 0 && b.Count() == 0)
				return 1;

			/// Согласно алгоритму Вагнера-Фишера, вычисляем матрицу расстояний
			var distances = new double[a.Count() + 1, b.Count() + 1];
			distances[0, 0] = 0;

			/// Заполняем первую строку и первый столбец
			for (int i = 1; i <= a.Count(); ++i)
				distances[i, 0] = i;
			for (int j = 1; j <= b.Count(); ++j)
				distances[0, j] = j;

			for (int i = 1; i <= a.Count(); i++)
				for (int j = 1; j <= b.Count(); j++)
				{
					/// Если элементы - это тоже перечислимые наборы элементов, считаем для них расстояние
					double cost = 1 - DispatchLevenshtein(a.ElementAt(i - 1), b.ElementAt(j - 1));
					distances[i, j] = Math.Min(Math.Min(
						distances[i - 1, j] + 1,
						distances[i, j - 1] + 1),
						distances[i - 1, j - 1] + cost);
				}

			return 1 - distances[a.Count(), b.Count()] / denominator;
		}

		/// Похожесть новой последовательности на старую 
		/// при переходе от последовательности a к последовательности b
		private static double DispatchLevenshtein<T>(T a, T b)
		{
			if (a is IEnumerable<string>)
				return Levenshtein((IEnumerable<string>)a, (IEnumerable<string>)b);
			if (a is IEnumerable<HeaderContextElement>)
				return Levenshtein((IEnumerable<HeaderContextElement>)a, (IEnumerable<HeaderContextElement>)b);
			else if (a is string)
				return Levenshtein((IEnumerable<char>)a, (IEnumerable<char>)b);
			else if (a is HeaderContextElement)
				return EvalSimilarity(a as HeaderContextElement, b as HeaderContextElement);
			else if (a is AncestorsContextElement)
				return EvalSimilarity(a as AncestorsContextElement, b as AncestorsContextElement);
			else
				return a.Equals(b) ? 1 : 0;
		}

		private static double EvalSimilarity(IEnumerable<InnerContextElement> a, IEnumerable<InnerContextElement> b)
		{
			var denominator = (double)Math.Max(a.Count(), b.Count());

			return denominator == 0 ? 1 
				: a.Intersect(b, new InnerContextElementComparer()).Count() / denominator;
		}

		private static double EvalSimilarity(HeaderContextElement a, HeaderContextElement b)
		{
			return Levenshtein(String.Join("", a.Value), String.Join("", b.Value));
		}

		private static double EvalSimilarity(AncestorsContextElement a, AncestorsContextElement b)
		{
			return Levenshtein(a.HeaderContext, b.HeaderContext);
		}
	}
}
