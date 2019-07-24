using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public class ModifiedContextFinder: IContextFinder
	{
		public class CandidateComparer : IComparer<IRemapCandidateInfo>
		{
			public double HeaderDistanceThreshold = 0.05;
			public double InnerGarbageThreshold = 0.5;

			public int Compare(IRemapCandidateInfo x, IRemapCandidateInfo y)
			{
				var xSim = (x.AncestorSimilarity * x.HeaderSimilarity).Value;
				var ySim = (y.AncestorSimilarity * y.HeaderSimilarity).Value;

				double headerSimilarityDifference = xSim - ySim,
					innerSimilarityDifference = (x.InnerSimilarity - y.InnerSimilarity).Value;

				/// Сравниваем элементы только по похожести заголовка и предка,
				/// если заголовки сильно непохожи,
				/// или внутренние контексты одинаково похожи друг на друга,
				/// или внутренние контексты сильно непохожи на оригинал
				return Math.Abs(headerSimilarityDifference) > 1 - Math.Max(xSim, ySim) 
						|| innerSimilarityDifference == 0
						|| x.InnerSimilarity < InnerGarbageThreshold 
							&& y.InnerSimilarity < InnerGarbageThreshold
					? Math.Sign(headerSimilarityDifference)
					: Math.Sign(innerSimilarityDifference);
			}
		}

		/// <summary>
		/// Поиск узлов дерева, соответствующих точкам привязки
		/// </summary>
		/// <param name="points">Точки привязки, сгруппированные по типу связанного с ними узла</param>
		/// <param name="candidateNodes">Узлы дерева, среди которых нужно найти соответствующие точкам, также сгруппированные по типу</param>
		/// <returns></returns>
		public Dictionary<ConcernPoint, List<IRemapCandidateInfo>> Find(
			Dictionary<string, List<ConcernPoint>> points, 
			Dictionary<string, List<Node>> candidateNodes, 
			TargetFileInfo candidateFileInfo
		)
		{
			var result = new Dictionary<ConcernPoint, List<IRemapCandidateInfo>>();
			var comparer = new CandidateComparer();

			foreach (var typePointsPair in points)
			{
				foreach (var point in typePointsPair.Value)
				{
					var candidates = candidateNodes.ContainsKey(typePointsPair.Key)
						? candidateNodes[typePointsPair.Key].Select(node =>
							new ModifiedRemapCandidateInfo()
							{
								Node = node,
								Context = new PointContext()
								{
									FileName = candidateFileInfo.FileName,
									NodeType = node.Type
								}
							} as IRemapCandidateInfo).ToList()
						: new List<IRemapCandidateInfo>();

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
							new TargetFileInfo() { FileName = candidateFileInfo.FileName, FileText = candidateFileInfo.FileText, TargetNode = candidate.Node }
						);
						candidate.Context.InnerContext = inner.Item1;
						candidate.Context.InnerContextElement = inner.Item2;

						candidate.InnerSimilarity = DispatchLevenshtein(point.Context.InnerContextElement, 
							candidate.Context.InnerContextElement);
					}

					result[point] = candidates.OrderByDescending(c=>c.AncestorSimilarity * c.HeaderSimilarity)
						.ThenByDescending(c => c, comparer).ToList();

					var first = result[point].FirstOrDefault();
					var second = result[point].Skip(1).FirstOrDefault();

					if (first != null)
					{
						first.IsAuto = second == null
							|| comparer.Compare(first, second) != 0;
					}
				}
			}

			return result;
		}

		public List<IRemapCandidateInfo> Find(ConcernPoint point, TargetFileInfo targetInfo)
		{
			var visitor = new GroupNodesByTypeVisitor(new List<string> { point.Context.NodeType });
			targetInfo.TargetNode.Accept(visitor);

			return Find(
				new Dictionary<string, List<ConcernPoint>> { { point.Context.NodeType, new List<ConcernPoint> { point } } },
				visitor.Grouped, targetInfo
			)[point];
		}

		///  Похожесть на основе расстояния Левенштейна
		private static double Levenshtein<T>(IEnumerable<T> a, IEnumerable<T> b)
		{
			if (a.Count() == 0 ^ b.Count() == 0)
				return 0;
			if (a.Count() == 0 && b.Count() == 0)
				return 1;

			var denominator = 0.0;

			if (a is IEnumerable<TypedPrioritizedContextElement>)
			{
				var comparer = new EqualsIgnoreValueComparer();

				var aSockets = (a as IEnumerable<IEqualsIgnoreValue>)
					.GroupBy(e => e, comparer).ToDictionary(g => g.Key, g => g.Count());
				var bSockets = (b as IEnumerable<IEqualsIgnoreValue>)
					.GroupBy(e => e, comparer).ToDictionary(g => g.Key, g => g.Count());

				denominator += aSockets.Sum(kvp 
					=> ((TypedPrioritizedContextElement)kvp.Key).Priority * kvp.Value);

				foreach (var kvp in aSockets)
				{
					var sameKey = bSockets.Keys.FirstOrDefault(e => e.EqualsIgnoreValue(kvp.Key));

					if (sameKey != null)
						bSockets[sameKey] -= kvp.Value;
				}

				denominator += bSockets.Where(kvp => kvp.Value > 0)
					.Sum(kvp => ((TypedPrioritizedContextElement)kvp.Key).Priority * kvp.Value);
			}
			else
			{
				denominator = Math.Max(a.Count(), b.Count());
			}

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
				distances[i, 0] = distances[i - 1, 0] + PriorityCoefficient(a.ElementAt(i - 1));
			for (int j = 1; j <= b.Count(); ++j)
				distances[0, j] = distances[0, j - 1] + PriorityCoefficient(b.ElementAt(j - 1));

			for (int i = 1; i <= a.Count(); i++)
				for (int j = 1; j <= b.Count(); j++)
				{
					/// Если элементы - это тоже перечислимые наборы элементов, считаем для них расстояние
					double cost = 1 - DispatchLevenshtein(a.ElementAt(i - 1), b.ElementAt(j - 1));
					distances[i, j] = Math.Min(Math.Min(
						distances[i - 1, j] + PriorityCoefficient(a.ElementAt(i - 1)),
						distances[i, j - 1] + PriorityCoefficient(b.ElementAt(j - 1))),
						distances[i - 1, j - 1] + PriorityCoefficient(a.ElementAt(i - 1)) * cost);
				}

			return 1 - distances[a.Count(), b.Count()] / denominator;
		}

		private static double PriorityCoefficient(object elem)
		{
			switch(elem)
			{
				case HeaderContextElement headerContext:
					return headerContext.Priority;
				case InnerContextElement innerContext:
					return innerContext.Priority;
				default:
					return 1;
			}
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
			else if(a is InnerContextElement)
				return EvalSimilarity(a as InnerContextElement, b as InnerContextElement);
			else if (a is AncestorsContextElement)
				return EvalSimilarity(a as AncestorsContextElement, b as AncestorsContextElement);
			else
				return a.Equals(b) ? 1 : 0;
		}

		private static double EvalSimilarity(HeaderContextElement a, HeaderContextElement b)
		{
			if (a.EqualsIgnoreValue(b))
			{
				return a.ExactMatch
					? String.Join("", a.Value) == String.Join("", b.Value) ? 1 : 0
					: Levenshtein(String.Join("", a.Value), String.Join("", b.Value));
			}
			else
				return 0;
		}

		private static double EvalSimilarity(InnerContextElement a, InnerContextElement b)
		{
			var score = a.Text != null && b.Text != null
				? Levenshtein(a.Text, b.Text)
				: a.Hash != null && b.Hash != null
					? FuzzyHashing.CompareHashes(a.Hash, b.Hash)
					: 0;

			return score < FuzzyHashing.MIN_TEXT_LENGTH / (double)InnerContextElement.MAX_TEXT_LENGTH 
				? 0 : score;
		}

		private static double EvalSimilarity(AncestorsContextElement a, AncestorsContextElement b)
		{
			return a.Type == b.Type ? Levenshtein(a.HeaderContext, b.HeaderContext) : 0;
		}
	}
}
