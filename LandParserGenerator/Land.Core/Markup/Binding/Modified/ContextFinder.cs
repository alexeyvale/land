using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Parsing.Tree;
using Land.Markup.CoreExtension;

namespace Land.Markup.Binding
{
	public enum ContextType { Header, Ancestors, Inner }

	public class ModifiedContextFinder: IContextFinder
	{
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
							new ParsedFile() { Name = candidateFileInfo.Name, Text = candidateFileInfo.Text, Root = candidate.Node }
						);
						candidate.Context.InnerContext = inner.Item1;
						candidate.Context.InnerContextElement = inner.Item2;

						candidate.InnerSimilarity = DispatchLevenshtein(point.Context.InnerContextElement, 
							candidate.Context.InnerContextElement);
					}

					EvaluateSimilarity(point.Context, candidates);

					result[point] = candidates = 
						candidates.OrderByDescending(c=>c.Similarity).ToList();

					var first = result[point].FirstOrDefault();
					var second = result[point].Skip(1).FirstOrDefault();

					if (first != null)
					{
						first.IsAuto = IsSimilarEnough(first) 
							&& AreDistantEnough(first, second);
					}

					/// Проверку горизонтального контекста выполняем только если
					/// есть несколько кандидатов с одинаковыми оценками похожести
					if (first != null && !first.IsAuto 
						&& IsSimilarEnough(first) && second != null)
					{
						var identicalCandidates = candidates.TakeWhile(c =>
							c.HeaderSimilarity == first.HeaderSimilarity &&
							c.InnerSimilarity == first.InnerSimilarity &&
							c.AncestorSimilarity == first.AncestorSimilarity).ToList();

						if (identicalCandidates.Count > 1)
						{
							var nextClosestCandidate = candidates.Skip(identicalCandidates.Count).FirstOrDefault();

							if (nextClosestCandidate == null || AreDistantEnough(first, nextClosestCandidate))
							{
								foreach(var candidate in identicalCandidates)
								{
									candidate.Context.SiblingsContext = PointContext.GetSiblingsContext(
										new ParsedFile() { Name = candidateFileInfo.Name, Text = candidateFileInfo.Text, Root = candidate.Node }
									);
								}

								var writer = System.IO.File.AppendText("log.txt");
								writer.WriteLine(point.Context.FileName);
								writer.WriteLine(String.Join(" ", point.Context.HeaderContext.Select(e=>String.Join("", e.Value))));
								writer.Close();

								var siblingsSimilarities = identicalCandidates.Select(c => new
								{
									BeforeSimilarity = EvalSimilarity(point.Context.SiblingsContext.Before, c.Context.SiblingsContext.Before),
									AfterSimilarity = EvalSimilarity(point.Context.SiblingsContext.After, c.Context.SiblingsContext.After),
									Candidate = c
								}).ToList();

								var bestBefore = siblingsSimilarities.OrderByDescending(e => e.BeforeSimilarity).First();
								var bestAfter = siblingsSimilarities.OrderByDescending(e => e.AfterSimilarity).First();

								if (bestBefore == bestAfter)
								{
									bestBefore.Candidate.IsAuto = true;
									candidates.Remove(bestBefore.Candidate);
									candidates.Insert(0, bestBefore.Candidate);
								}
							}
						}
					}
				}
			}

			return result;
		}

		private bool IsSimilarEnough(RemapCandidateInfo candidate) => 
			candidate.Similarity >= 0.6;

		private bool AreDistantEnough(RemapCandidateInfo first, RemapCandidateInfo second) =>
			second == null || second.Similarity != 1 
				&& 1 - second.Similarity >= (1 - first.Similarity) * 1.5;

		private void EvaluateSimilarity(PointContext sourceContext,
			List<RemapCandidateInfo> candidates)
		{
			if (candidates.Count == 0)
				return;

			/// Проверяем, какие контексты не задействованы, их вес равен 0
			var weights = new Dictionary<ContextType, double?> {
				{ ContextType.Ancestors, null },
				{ ContextType.Header, null },
				{ ContextType.Inner, null }
			};

			/// Если кандидат один, выводим его оценку с весами по умолчанию
			if (candidates.Count == 1)
			{
				var defaultHeuristic = new DefaultWeightsHeuristic();
				defaultHeuristic.TuneWeights(sourceContext, candidates, weights);
			}
			else
			{
				var tuningHeuristicType = typeof(IWeightsHeuristic);

				var tuningHeuristics = AppDomain.CurrentDomain.GetAssemblies()
					.SelectMany(s => s.GetTypes())
					.Where(p => p.IsClass && tuningHeuristicType.IsAssignableFrom(p))
					.Select(t=>(IWeightsHeuristic)t.GetConstructor(Type.EmptyTypes).Invoke(null))
					.OrderByDescending(h=>h.Priority)
					.ToList();

				var scoringheuristicType = typeof(ISimilarityHeuristic);

				var scoringheuristics = AppDomain.CurrentDomain.GetAssemblies()
					.SelectMany(s => s.GetTypes())
					.Where(p => p.IsClass && scoringheuristicType.IsAssignableFrom(p))
					.Select(t => (ISimilarityHeuristic)t.GetConstructor(Type.EmptyTypes).Invoke(null))
					.OrderByDescending(h => h.Priority)
					.ToList();

				foreach (var h in tuningHeuristics)
					h.TuneWeights(sourceContext, candidates, weights);

				foreach (var h in scoringheuristics)
					h.PredictSimilarity(sourceContext, candidates);
			}

			/// Если какие-то веса остались неустановленными, обнуляем
			foreach (var key in weights.Keys)
				if (weights[key] == null)
					weights[key] = 0;

			candidates.ForEach(c => c.Similarity = c.Similarity ??
				(weights[ContextType.Ancestors] * c.AncestorSimilarity + weights[ContextType.Inner] * c.InnerSimilarity + weights[ContextType.Header] * c.HeaderSimilarity)
				/ weights.Values.Sum());
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

		public static double EvalSimilarity(HeaderContextElement a, HeaderContextElement b)
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

		public static double EvalSimilarity(InnerContextElement a, InnerContextElement b)
		{
			return a.Type == b.Type ? EvalSimilarity(a.Content, b.Content) : 0;
		}

		public static double EvalSimilarity(TextOrHash a, TextOrHash b)
		{
			var score = a.Text != null && b.Text != null
				? Levenshtein(a.Text, b.Text)
				: a.Hash != null && b.Hash != null
					? FuzzyHashing.CompareHashes(a.Hash, b.Hash)
					: 0;

			return score < FuzzyHashing.MIN_TEXT_LENGTH / (double)TextOrHash.MAX_TEXT_LENGTH
				? 0 : score;
		}

		public static double EvalSimilarity(AncestorsContextElement a, AncestorsContextElement b)
		{
			return a.Type == b.Type ? Levenshtein(a.HeaderContext, b.HeaderContext) : 0;
		}
	}
}
