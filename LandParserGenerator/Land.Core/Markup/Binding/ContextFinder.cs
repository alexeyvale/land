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
		private const double FILE_SIMILARITY_THRESHOLD = 0.8;
		private const double CANDIDATE_SIMILARITY_THRESHOLD = 0.6;

		public Func<string, ParsedFile> GetParsed { get; set; }

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

		private List<RemapCandidateInfo> LocalSearch(
			ConcernPoint point, 
			List<ParsedFile> searchArea)
		{
			/// Ищем файл с тем же названием
			var sameFile = searchArea.FirstOrDefault(f =>
				f.Name.ToLower() == point.Context.FileContext.Name.ToLower());

			if (sameFile != null)
			{
				if(!EnsureRootExists(sameFile))
					return null;

				/// Проверяем, насколько изменилось количество строк в файле
				var lineDifference = sameFile.BindingContext.LineCount -
					point.Context.FileContext.LineCount;

				/// Находим все сущности того же типа
				var visitor = new GroupNodesByTypeVisitor(new List<string> { point.Context.Type });
				sameFile.Root.Accept(visitor);

				bool InRange(int val, int left, int right) => val >= left && val <= right; 

				/// Среди них отбираем те, что отстоят от расположения помеченной сущности
				/// не более чем на количество добавленных/удалённых строк
				var candidates = visitor.Grouped[point.Context.Type]
					.Where(n =>
						lineDifference >= 0 && 
							InRange(n.Location.Start.Line.Value, point.Context.Line, point.Context.Line + lineDifference) ||
						lineDifference <= 0 && 
							InRange(n.Location.Start.Line.Value, point.Context.Line + lineDifference, point.Context.Line)
					)
					.Select(n => new RemapCandidateInfo
					{
						Node = n,
						File = sameFile,
						Context = PointContext.GetCoreContext(n, sameFile)
					}).ToList();

				return EvalCandidates(point, candidates, sameFile.MarkupSettings);
			}

			return new List<RemapCandidateInfo>();
		}

		public List<RemapCandidateInfo> GlobalSearch(
			ConcernPoint point, 
			List<ParsedFile> searchArea,
			bool similarOnly)
		{
			var files = searchArea
				.Where(f => !similarOnly || AreFilesSimilarEnough(f.BindingContext.Content, point.Context.FileContext.Content))
				.ToList();

			var candidates = new List<RemapCandidateInfo>();

			/// Находим все сущности того же типа
			foreach (var file in files)
			{
				if (!EnsureRootExists(file))
					continue;

				var visitor = new GroupNodesByTypeVisitor(new List<string> { point.Context.Type });
				file.Root.Accept(visitor);

				candidates.AddRange(visitor.Grouped[point.Context.Type]
					.Select(n => new RemapCandidateInfo
					{
						Node = n,
						File = file,
						Context = PointContext.GetCoreContext(n, file)
					})
					.ToList()
				);		
			}

			return files.Count > 0
				? EvalCandidates(point, candidates, files.First().MarkupSettings)
				: candidates;
		}

		private void CheckHorizontalContext(
			ConcernPoint point, 
			List<RemapCandidateInfo> candidates)
		{
			var first = candidates.FirstOrDefault();
			var second = candidates.Skip(1).FirstOrDefault();

			/// Проверку горизонтального контекста выполняем только если
			/// есть несколько кандидатов с одинаковыми оценками похожести
			if (first != null && !first.IsAuto &&
				IsSimilarEnough(first) && second != null)
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
						writer.WriteLine(point.Context.FileContext.Name);
						writer.WriteLine(String.Join(" ", point.Context.HeaderContext.Select(e => String.Join("", e.Value))));
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

		private List<RemapCandidateInfo> EvalCandidates(
			ConcernPoint point,
			List<RemapCandidateInfo> candidates,
			LanguageMarkupSettings markupSettings)
		{
			foreach (var candidate in candidates)
				ComputeCoreContextSimilarities(point.Context, candidate);

			ComputeTotalSimilarity(point.Context, candidates);

			if (!markupSettings.UseSiblingsContext)
			{
				/// Отсеиваем похожих кандидатов, которые существовали в момент привязки
				candidates = candidates
					.OrderByDescending(c => c.Similarity)
					.Where(c=>!point.Context.ClosestContext
						.Any(e=>e.HeaderInnerHash.SequenceEqual(c.Context.HeaderInnerHash) && e.AncestorsHash.SequenceEqual(c.Context.AncestorsHash)))
					.ToList();
			}

			var first = candidates.FirstOrDefault();
			var second = candidates.Skip(1).FirstOrDefault();

			if (first != null)
			{
				first.IsAuto = IsSimilarEnough(first)
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
				DispatchLevenshtein(point.InnerContext, candidate.Context.InnerContext);
		}

		/// <summary>
		/// Поиск узлов дерева, соответствующих точкам привязки
		/// </summary>
		public Dictionary<ConcernPoint, List<RemapCandidateInfo>> Find(
			List<ConcernPoint> points,
			List<ParsedFile> searchArea,
			bool localOnly)
		{
			var groupedPoints = points.GroupBy(p => p.Context.Type)
				.ToDictionary(e=>e.Key, e=>e.ToList());

			var result = new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();

			foreach (var pointType in groupedPoints.Keys)
			{
				foreach (var point in groupedPoints[pointType])
				{
					result[point] = Find(point, searchArea, localOnly);
				}
			}

			return result;
		}

		public List<RemapCandidateInfo> Find(
			ConcernPoint point, 
			List<ParsedFile> searchArea,
			bool localOnly)
		{
			var searchResult = LocalSearch(point, searchArea);

			if ((searchResult.Count == 0 || !searchResult.First().IsAuto) && !localOnly)
			{
				searchResult = GlobalSearch(point, searchArea, true);

				if (searchResult.Count == 0)
				{
					searchResult = GlobalSearch(point, searchArea, false);
				}
			}

			return searchResult;
		}

		public static bool AreFilesSimilarEnough(TextOrHash a, TextOrHash b) =>
			EvalSimilarity(a, b) > FILE_SIMILARITY_THRESHOLD;

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
			else if(a is InnerContext)
				return EvalSimilarity(a as InnerContext, b as InnerContext);
			else if (a is AncestorsContextElement)
				return EvalSimilarity(a as AncestorsContextElement, b as AncestorsContextElement);
			else
				return a.Equals(b) ? 1 : 0;
		}

		#region EvalSimilarity

		public static double EvalSimilarity(List<HeaderContextElement> a, List<HeaderContextElement> b) => 
			Levenshtein(a, b);

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

		public static double EvalSimilarity(InnerContext a, InnerContext b)
		{
			return EvalSimilarity(a.Content, b.Content);
		}

		public static double EvalSimilarity(AncestorsContextElement a, AncestorsContextElement b)
		{
			return a.Type == b.Type ? Levenshtein(a.HeaderContext, b.HeaderContext) : 0;
		}

		public static double EvalSimilarity(TextOrHash a, TextOrHash b)
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

		private bool IsSimilarEnough(RemapCandidateInfo candidate) =>
			candidate.Similarity >= CANDIDATE_SIMILARITY_THRESHOLD;

		private bool AreDistantEnough(RemapCandidateInfo first, RemapCandidateInfo second) =>
			second == null || second.Similarity != 1
				&& 1 - second.Similarity >= (1 - first.Similarity) * 1.5;

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
