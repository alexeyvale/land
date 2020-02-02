using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Land.Core.Parsing.Tree;
using Land.Markup.CoreExtension;

namespace Land.Markup.Binding
{
	public enum ContextType { Header, Ancestors, Inner }

	public class ContextFinder
	{
		private class StringPair
		{
			private string StrA { get; set; }
			private string StrB { get; set; }

			public StringPair(string a, string b)
			{
				StrA = a;
				StrB = b;
			}

			public override bool Equals(object obj)
			{
				return obj is StringPair pair &&
					   StrA == pair.StrA &&
					   StrB == pair.StrB;
			}

			public override int GetHashCode()
			{
				return StrA.GetHashCode() ^ StrB.GetHashCode();
			}
		}

		private enum SearchType { SameFile, SimilarFiles, AllFiles }

		public const double FILE_SIMILARITY_THRESHOLD = 0.6;
		public const double CANDIDATE_SIMILARITY_THRESHOLD = 0.6;
		public const double SECOND_DISTANCE_GAP_COEFFICIENT = 1.5;

		public Func<string, ParsedFile> GetParsed { get; set; }

		public PointContextManager ContextManager { get; private set; } = new PointContextManager();

		public List<IWeightsHeuristic> TuningHeuristics { get; private set; } = 
			new List<IWeightsHeuristic>();
		public List<ISimilarityHeuristic> ScoringHeuristics { get; private set; } = 
			new List<ISimilarityHeuristic>();

		public void SetHeuristic(Type type)
		{
			void AddToList<T>(List<T> heuristics) where T: IHeuristic 
			{
				if (typeof(T).IsAssignableFrom(type))
				{
					var element = heuristics.FirstOrDefault(e => e.GetType().Equals(type));

					if (element == null)
					{
						var constructor = type.GetConstructor(Type.EmptyTypes);

						if (constructor != null)
						{
							heuristics.Add((T)constructor.Invoke(null));
							heuristics = heuristics.OrderByDescending(h => h.Priority).ToList();
						}
					}
				}
			}

			AddToList(TuningHeuristics);
			AddToList(ScoringHeuristics);
		}

		public void ResetHeuristic(Type type)
		{
			void RemoveFromList<T>(List<T> heuristics)
			{
				if (typeof(T).IsAssignableFrom(type))
				{
					var element = heuristics.FirstOrDefault(e => e.GetType().Equals(type));

					if (element != null)
					{
						heuristics.Remove(element);
					}
				}
			}

			RemoveFromList(TuningHeuristics);
			RemoveFromList(ScoringHeuristics);
		}

		private Dictionary<ConcernPoint, List<RemapCandidateInfo>> DoSearch(
			List<ConcernPoint> points, 
			List<ParsedFile> searchArea,
			SearchType searchType)
		{
			var type = points[0].Context.Type;
			var file = points[0].Context.FileContext;

			List<ParsedFile> files = null;

			switch(searchType)
			{
				/// При поиске в том же файле ищем тот же файл по полному совпадению пути,
				/// если не находим - только по имени
				case SearchType.SameFile:
					files = searchArea.Where(f => f.Name == file.Name).ToList();

					if (files.Count == 0)
					{
						files = searchArea.Where(f => Path.GetFileName(f.Name) == 
							Path.GetFileName(file.Name)).ToList();
					}
					break;
				/// Похожие файлы ищем на основе хеша содержимого
				case SearchType.SimilarFiles:
					files = searchArea
						.Where(f => AreFilesSimilarEnough(f.BindingContext.Content, points[0].Context.FileContext.Content))
						.ToList();
					break;
				/// В противном случае проводим поиск по всей области
				default:
					files = searchArea;
					break;
			}

			var candidates = new List<RemapCandidateInfo>();

			/// Находим все сущности того же типа
			foreach (var currentFile in files)
			{
				if (!EnsureRootExists(currentFile))
					continue;

				var visitor = new GroupNodesByTypeVisitor(new List<string> { type });
				currentFile.Root.Accept(visitor);

				candidates.AddRange(visitor.Grouped[type]
					.Select(n => new RemapCandidateInfo
					{
						Node = n,
						File = currentFile,
						Context = ContextManager.GetContext(n, currentFile)
					})
					.ToList()
				);		
			}

			var contextsToPoints = points.GroupBy(p => p.Context)
				.ToDictionary(g=>g.Key, g=>g.ToList());

			/// Задаём граф списком смежностей - каждый из исходных контекстов 
			/// связан со всеми кандидатами, вес ребра - похожесть
			var graph = points.SelectMany(p => files.First().MarkupSettings.UseSiblingsContext ? new HashSet<PointContext> { p.Context } : new HashSet<PointContext>(p.Context.ClosestContext) { p.Context })
				.ToDictionary(p => p, p => candidates.Select(c => new RemapCandidateInfo { Node = c.Node, File = c.File, Context = c.Context }).ToList());

			foreach(var elem in graph)
			{
				EvalCandidates(elem.Key, elem.Value, files.First().MarkupSettings, 
					CANDIDATE_SIMILARITY_THRESHOLD);
			}

			var result = new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();

			/// Сразу обрабатываем стопроцентные совпадения, уменьшая размерность
			/// задачи поиска паросочетания максимального веса
			foreach (var src in graph.Keys.Where(k => contextsToPoints.ContainsKey(k)).ToList())
			{
				var perfectMatch = graph[src].FirstOrDefault(e => e.Similarity == 1);

				if(perfectMatch != null)
				{
					perfectMatch.IsAuto = true;

					var allCandidates = graph[src];
					graph.Remove(src);

					var candidateIndex = allCandidates.IndexOf(perfectMatch);

					foreach (var val in graph.Values)
						val.RemoveAt(candidateIndex);
					candidates.RemoveAt(candidateIndex);

					allCandidates = allCandidates.OrderByDescending(c => c.Similarity).ToList();

					foreach (var point in contextsToPoints[src])
						result[point] = allCandidates;
				}
			}

			if (graph.Keys.Any(k => contextsToPoints.ContainsKey(k)))
			{
				var scores = new double[graph.Count + 1, candidates.Count + 1];
				var indicesToContexts = new PointContext[scores.GetLength(0)];

				var i = 1;
				foreach (var from in graph)
				{
					indicesToContexts[i] = from.Key;

					var j = 1;
					foreach (var to in from.Value)
					{
						scores[i, j] = -to.Similarity ?? 0;
						++j;
					}
					++i;
				}

				var bestMatches = FindMaximumMatching(scores);

				for (i = 1; i < bestMatches.Length; ++i)
				{
					if (contextsToPoints.ContainsKey(indicesToContexts[i]))
					{
						foreach (var point in contextsToPoints[indicesToContexts[i]])
						{
							var bestMatch = graph[indicesToContexts[i]][bestMatches[i] - 1];
							var allCandidates = graph[indicesToContexts[i]].OrderByDescending(c => c.Similarity).ToList();

							allCandidates.ForEach(c => c.IsAuto = false);
							allCandidates.Remove(bestMatch);
							allCandidates.Insert(0, bestMatch);

							if (IsSimilarEnough(bestMatch, CANDIDATE_SIMILARITY_THRESHOLD))
							{
								bestMatch.IsAuto = true;
							}

							result[point] = graph[indicesToContexts[i]];
						}
					}
				}
			}

			return files.Count > 0 ? result : null;
		}

		private int[] FindMaximumMatching(double[,] similarities)
		{
			var lines = new double[similarities.GetLength(0)];
			var columns = new double[similarities.GetLength(1)];
			var matching = new int[columns.Length];
			var way = new int[columns.Length];

			for (int i = 1; i < lines.Length; ++i)
			{
				matching[0] = i;

				var j0 = 0;
				var minv = new double[columns.Length];
				var used = new bool[columns.Length];

				do
				{
					used[j0] = true;
					int i0 = matching[j0], j1 = 0;
					double delta = 0;

					for (int j = 1; j < columns.Length; ++j)
					{
						if (!used[j])
						{
							var cur = similarities[i0, j] - lines[i0] - columns[j];

							if (cur < minv[j])
							{
								minv[j] = cur;
								way[j] = j0;
							}

							if (minv[j] < delta)
							{
								delta = minv[j];
								j1 = j;
							}
						}
					}

					for (int j = 0; j < columns.Length; ++j)
					{
						if (used[j])
						{
							lines[matching[j]] += delta;
							columns[j] -= delta;
						}
						else
						{
							minv[j] -= delta;
						}
					}

					j0 = j1;
				}
				while (matching[j0] != 0);

				do
				{
					int j1 = way[j0];
					matching[j0] = matching[j1];
					j0 = j1;
				} while (j0 != 0);
			}

			var ans = new int[lines.Length];

			for (int j = 1; j < columns.Length; ++j)
			{
				ans[matching[j]] = j;
			}

			return ans;
		}

		private void CheckHorizontalContext(
			PointContext point, 
			List<RemapCandidateInfo> candidates)
		{
			var first = candidates.FirstOrDefault();
			var second = candidates.Skip(1).FirstOrDefault();

			/// Проверку горизонтального контекста выполняем только если
			/// есть несколько кандидатов с одинаковыми оценками похожести
			if (first != null && !first.IsAuto &&
				IsSimilarEnough(first, CANDIDATE_SIMILARITY_THRESHOLD) && second != null)
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
						foreach (var candidate in identicalCandidates)
						{
							candidate.Context.SiblingsContext =
								PointContext.GetSiblingsContext(candidate.Node, candidate.File);
						}

						var writer = System.IO.File.AppendText("log.txt");
						writer.WriteLine(point.FileContext.Name);
						writer.WriteLine(String.Join(" ", point.HeaderContext.Select(e => String.Join("", e.Value))));
						writer.Close();

						var siblingsSimilarities = identicalCandidates.Select(c => new
						{
							BeforeSimilarity = EvalSimilarity(point.SiblingsContext.Before, c.Context.SiblingsContext.Before),
							AfterSimilarity = EvalSimilarity(point.SiblingsContext.After, c.Context.SiblingsContext.After),
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

		public List<RemapCandidateInfo> EvalCandidates(
			PointContext point,
			List<RemapCandidateInfo> candidates,
			LanguageMarkupSettings markupSettings,
			double similarityThreshold)
		{
			foreach (var candidate in candidates)
				ComputeCoreContextSimilarities(point, candidate);

			ComputeTotalSimilarity(point, candidates);

			candidates = candidates.OrderByDescending(c => c.Similarity).ToList();

			var first = candidates.FirstOrDefault();
			var second = candidates.Skip(1).FirstOrDefault();

			if (first != null)
			{
				first.IsAuto = IsSimilarEnough(first, similarityThreshold)
					&& AreDistantEnough(first, second);
			}

			/// Отсеиваем очень похожих кандидатов с непохожими соседями
			if (markupSettings.UseSiblingsContext)
				CheckHorizontalContext(point, candidates);

			return candidates;
		}

		private void ComputeCoreContextSimilarities(PointContext point, RemapCandidateInfo candidate)
		{
			candidate.HeaderSimilarity =
				Levenshtein(point.HeaderContext, candidate.Context.HeaderContext);
			candidate.AncestorSimilarity =
				Levenshtein(point.AncestorsContext, candidate.Context.AncestorsContext);
			candidate.InnerSimilarity =
				EvalSimilarity(point.InnerContext, candidate.Context.InnerContext);
		}

		/// <summary>
		/// Поиск узлов дерева, соответствующих точкам привязки
		/// </summary>
		public Dictionary<ConcernPoint, List<RemapCandidateInfo>> Find(
			List<ConcernPoint> points,
			List<ParsedFile> searchArea)
		{
			var groupedPoints = points
				.GroupBy(p => new { p.Context.Type, FileName = p.Context.FileContext.Name })
				.ToDictionary(e=>e.Key, e=>e.ToList());

			var overallResult = new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();

			foreach (var groupKey in groupedPoints.Keys)
			{
				var groupResult = FindGroup(groupedPoints[groupKey], searchArea);

				foreach(var elem in groupResult)
				{
					overallResult[elem.Key] = elem.Value;
				}
			}

			return overallResult;
		}

		private Dictionary<ConcernPoint, List<RemapCandidateInfo>> FindGroup(
			List<ConcernPoint> points, 
			List<ParsedFile> searchArea)
		{
			var searchResult = DoSearch(points, searchArea, SearchType.SimilarFiles);

			return searchResult;
		}

		public bool AreFilesSimilarEnough(TextOrHash a, TextOrHash b) =>
			EvalSimilarity(a, b) > FILE_SIMILARITY_THRESHOLD;

		/// Похожесть новой последовательности на старую 
		/// при переходе от последовательности a к последовательности b
		private double DispatchLevenshtein<T>(T a, T b)
		{
			if (a is IEnumerable<string>)
				return Levenshtein((IEnumerable<string>)a, (IEnumerable<string>)b);
			if (a is IEnumerable<HeaderContextElement>)
				return Levenshtein((IEnumerable<HeaderContextElement>)a, (IEnumerable<HeaderContextElement>)b);
			else if (a is string)
				return Levenshtein(a as string, b as string);
			else if (a is HeaderContextElement)
				return EvalSimilarity(a as HeaderContextElement, b as HeaderContextElement);
			else if (a is InnerContext)
				return EvalSimilarity(a as InnerContext, b as InnerContext);
			else if (a is AncestorsContextElement)
				return EvalSimilarity(a as AncestorsContextElement, b as AncestorsContextElement);
			else
				return a.Equals(b) ? 1 : 0;
		}

		#region EvalSimilarity

		public double EvalSimilarity(List<HeaderContextElement> a, List<HeaderContextElement> b) => 
			Levenshtein(a, b);

		public double EvalSimilarity(HeaderContextElement a, HeaderContextElement b)
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

		public double EvalSimilarity(InnerContext a, InnerContext b)
		{
			return EvalSimilarity(a.Content, b.Content);
		}

		public double EvalSimilarity(AncestorsContextElement a, AncestorsContextElement b)
		{
			return a.Type == b.Type ? Levenshtein(a.HeaderContext, b.HeaderContext) : 0;
		}

		public double EvalSimilarity(TextOrHash a, TextOrHash b)
		{
			var score = a.Hash != null && b.Hash != null
					? FuzzyHashing.CompareHashes(a.Hash, b.Hash)
					: a.Text != null && b.Text != null
						? Levenshtein(a.Text, b.Text) : 0;

			return score < FuzzyHashing.MIN_TEXT_LENGTH / (double)TextOrHash.MAX_TEXT_LENGTH
				? 0 : score;
		}

		#endregion

		#region Methods 

		///  Похожесть на основе расстояния Левенштейна
		private double Levenshtein<T>(IEnumerable<T> a, IEnumerable<T> b)
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

		private double Levenshtein(string a, string b)
		{
			if (a.Length == 0 ^ b.Length == 0)
				return 0;
			if (a.Length == 0 && b.Length == 0)
				return 1;

			var denominator = (double)Math.Max(a.Length, b.Length);

			/// Сразу отбрасываем общие префиксы и суффиксы
			var commonPrefixLength = 0;
			while (commonPrefixLength < a.Length && commonPrefixLength < b.Length
				&& a[commonPrefixLength].Equals(b[commonPrefixLength]))
				++commonPrefixLength;
			a = a.Substring(commonPrefixLength);
			b = b.Substring(commonPrefixLength);

			var commonSuffixLength = 0;
			while (commonSuffixLength < a.Length && commonSuffixLength < b.Length
				&& a[a.Length - 1 - commonSuffixLength].Equals(b[b.Length - 1 - commonSuffixLength]))
				++commonSuffixLength;
			a = a.Substring(0, a.Length - commonSuffixLength);
			b = b.Substring(0, b.Length - commonSuffixLength);

			if (a.Length == 0 && b.Length == 0)
				return 1;

			/// Согласно алгоритму Вагнера-Фишера, вычисляем матрицу расстояний
			var distances = new double[a.Length + 1, b.Length + 1];
			distances[0, 0] = 0;

			/// Заполняем первую строку и первый столбец
			for (int i = 1; i <= a.Length; ++i)
				distances[i, 0] = distances[i - 1, 0] + 1;
			for (int j = 1; j <= b.Length; ++j)
				distances[0, j] = distances[0, j - 1] + 1;

			for (int i = 1; i <= a.Length; i++)
				for (int j = 1; j <= b.Length; j++)
				{
					/// Если элементы - это тоже перечислимые наборы элементов, считаем для них расстояние
					double cost = a[i - 1] == b[j - 1] ? 0 : 1;
					distances[i, j] = Math.Min(Math.Min(
						distances[i - 1, j] + 1,
						distances[i, j - 1] + 1),
						distances[i - 1, j - 1] + cost);
				}

			return 1 - distances[a.Length, b.Length] / denominator;
		}

		private static double PriorityCoefficient(object elem)
		{
			switch (elem)
			{
				case HeaderContextElement headerContext:
					return headerContext.Priority;
				default:
					return 1;
			}
		}

		private bool EnsureRootExists(ParsedFile file)
		{
			if (file.Root == null)
				file.Root = GetParsed(file.Name)?.Root;

			return file.Root != null;
		}

		private bool IsSimilarEnough(RemapCandidateInfo candidate, double threshold) =>
			candidate.Similarity >= threshold;

		private bool AreDistantEnough(RemapCandidateInfo first, RemapCandidateInfo second) =>
			second == null || second.Similarity != 1
				&& 1 - second.Similarity >= (1 - first.Similarity) * SECOND_DISTANCE_GAP_COEFFICIENT;

		private void ComputeTotalSimilarity(PointContext sourceContext,
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
				foreach (var h in TuningHeuristics)
					h.TuneWeights(sourceContext, candidates, weights);

				foreach (var h in ScoringHeuristics)
					h.PredictSimilarity(sourceContext, candidates);
			}

			candidates.ForEach(c => c.Similarity = c.Similarity ??
				(weights[ContextType.Ancestors] * c.AncestorSimilarity + weights[ContextType.Inner] * c.InnerSimilarity + weights[ContextType.Header] * c.HeaderSimilarity)
				/ weights.Values.Sum());
		}

		#endregion
	}
}
