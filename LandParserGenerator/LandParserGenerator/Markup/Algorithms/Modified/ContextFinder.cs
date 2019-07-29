using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public class ModifiedContextFinder: IContextFinder
	{
		public enum ContextType { Header, Ancestors, Inner }

		public class ContextFeatures
		{
			public double MaxValue { get; set; }
			public double GapFromMax { get; set; }
			public double MedianGap { get; set; }
		}

		/// <summary>
		/// Поиск узлов дерева, соответствующих точкам привязки
		/// </summary>
		/// <param name="points">Точки привязки, сгруппированные по типу связанного с ними узла</param>
		/// <param name="candidateNodes">Узлы дерева, среди которых нужно найти соответствующие точкам, также сгруппированные по типу</param>
		/// <returns></returns>
		public Dictionary<ConcernPoint, List<RemapCandidateInfo>> Find(
			Dictionary<string, List<ConcernPoint>> points, 
			Dictionary<string, List<Node>> candidateNodes, 
			TargetFileInfo candidateFileInfo
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
									FileName = candidateFileInfo.FileName,
									NodeType = node.Type
								}
							} as RemapCandidateInfo).ToList()
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
							new TargetFileInfo() { FileName = candidateFileInfo.FileName, FileText = candidateFileInfo.FileText, TargetNode = candidate.Node }
						);
						candidate.Context.InnerContext = inner.Item1;
						candidate.Context.InnerContextElement = inner.Item2;

						candidate.InnerSimilarity = DispatchLevenshtein(point.Context.InnerContextElement, 
							candidate.Context.InnerContextElement);
					}

					SetTotalSimilarity(point.Context, candidates);

					result[point] = candidates.OrderByDescending(c=>c.Similarity).ToList();

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

		private void SetTotalSimilarity(PointContext sourceContext,
			List<RemapCandidateInfo> candidates)
		{
			if (candidates.Count == 0)
				return;

			const int MAX_CONTEXT_WEIGHT = 3;

			/// Это можно сделать статическими проверками на этапе формирования грамматики
			var useInner = candidates.Any(c => c.Context.InnerContextElement.TextLength > 0)
					|| sourceContext.InnerContextElement.TextLength > 0;
			var useHeader = candidates.Any(c => c.Context.HeaderContext.Count > 0)
				|| sourceContext.HeaderContext.Count > 0;
			var useAncestors = (candidates.Any(c => c.Context.AncestorsContext.Count > 0)
				|| sourceContext.AncestorsContext.Count > 0);

			/// Проверяем, какие контексты не задействованы, их вес равен 0
			var weights = new Dictionary<ContextType, double> { { ContextType.Ancestors, useAncestors ? 1 : 0 },
				{ ContextType.Header, useHeader ? 1 : 0 }, { ContextType.Inner, useInner ? 1 : 0 } };

			if (candidates.Count == 1)
			{
				candidates[0].Similarity = (weights[ContextType.Header] * candidates[0].HeaderSimilarity
					+ weights[ContextType.Ancestors] * candidates[0].AncestorSimilarity
					+ weights[ContextType.Inner] * candidates[0].InnerSimilarity) / weights.Values.Sum();
				return;
			}

			/// Сортируем кандидатов по похожести каждого из контекстов
			var orderedByHeader = candidates.OrderByDescending(c => c.HeaderSimilarity).ToList();
			var orderedByAncestors = candidates.OrderByDescending(c => c.AncestorSimilarity).ToList();
			var orderedByInner = candidates.OrderByDescending(c => c.InnerSimilarity).ToList();

			/// Считаем разности между последовательно идущими отсортированными по похожести элементами
			var headerGaps = new List<double>(orderedByHeader.Count - 1);
			for (var i = 0; i < orderedByHeader.Count - 1; ++i)
				headerGaps.Add(orderedByHeader[i].HeaderSimilarity - orderedByHeader[i + 1].HeaderSimilarity);
			headerGaps = headerGaps.OrderByDescending(e => e).ToList();
			var ancestorGaps = new List<double>(orderedByAncestors.Count - 1);
			for (var i = 0; i < orderedByAncestors.Count - 1; ++i)
				ancestorGaps.Add(orderedByAncestors[i].AncestorSimilarity - orderedByAncestors[i + 1].AncestorSimilarity);
			ancestorGaps = ancestorGaps.OrderByDescending(e => e).ToList();
			var innerGaps = new List<double>(orderedByInner.Count - 1);
			for (var i = 0; i < orderedByHeader.Count - 1; ++i)
				innerGaps.Add(orderedByInner[i].InnerSimilarity - orderedByInner[i + 1].InnerSimilarity);
			innerGaps = innerGaps.OrderByDescending(e => e).ToList();

			var features = new Dictionary<ContextType, ContextFeatures>
			{
				{
					ContextType.Ancestors, new ContextFeatures
					{
						MaxValue = orderedByAncestors.First().AncestorSimilarity,
						GapFromMax = orderedByAncestors[0].AncestorSimilarity - orderedByAncestors[1].AncestorSimilarity,
						MedianGap = ancestorGaps.Count % 2 == 0
							? (ancestorGaps[ancestorGaps.Count / 2] + ancestorGaps[ancestorGaps.Count / 2 - 1]) / 2
							: ancestorGaps[ancestorGaps.Count / 2]
					}
				},
				{
					ContextType.Header, new ContextFeatures
					{
						MaxValue = orderedByAncestors.First().HeaderSimilarity,
						GapFromMax = orderedByHeader[0].HeaderSimilarity - orderedByHeader[1].HeaderSimilarity,
						MedianGap = headerGaps.Count % 2 == 0
							? (headerGaps[headerGaps.Count / 2] + headerGaps[headerGaps.Count / 2 - 1]) / 2
							: headerGaps[headerGaps.Count / 2]
					}
				},
				{
					ContextType.Inner, new ContextFeatures
					{
						MaxValue = orderedByInner.First().InnerSimilarity,
						GapFromMax = orderedByInner[0].InnerSimilarity - orderedByInner[1].InnerSimilarity,
						MedianGap = innerGaps.Count % 2 == 0
							? (innerGaps[innerGaps.Count / 2] + innerGaps[innerGaps.Count / 2 - 1]) / 2
							: innerGaps[innerGaps.Count / 2]
					}
				}
			};

			/// Контексты с почти одинаковыми значениями похожести имеют минимальный вес,
			/// остальные сортируем в зависимости от того, насколько по ним различаются кандидаты
			var contextsToPrioritize = new List<ContextType>();

			foreach (var kvp in features)
			{
				if (kvp.Value.MedianGap < 0.04 && kvp.Value.GapFromMax < 0.04)
					weights[kvp.Key] = 1;
				else
					contextsToPrioritize.Add(kvp.Key);
			}

			/// Доп.проверка для внутреннего контекста
			if (features[ContextType.Inner].MaxValue <= 0.65)
			{
				weights[ContextType.Inner] = 1;
				contextsToPrioritize.Remove(ContextType.Inner);
			}

			contextsToPrioritize = contextsToPrioritize.OrderByDescending(c => features[c].MedianGap).ToList();
			for (var i = 0; i < contextsToPrioritize.Count; ++i)
				weights[contextsToPrioritize[i]] = MAX_CONTEXT_WEIGHT - i;

			candidates.ForEach(c => ((RemapCandidateInfo)c).Similarity =
				(weights[ContextType.Ancestors] * c.AncestorSimilarity + weights[ContextType.Inner] * c.InnerSimilarity + weights[ContextType.Header] * c.HeaderSimilarity)
				/ weights.Values.Sum());
		}


		public List<RemapCandidateInfo> Find(ConcernPoint point, TargetFileInfo targetInfo)
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
				return double.MinValue;
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
