using Land.Core.Parsing.Tree;
using Land.Markup;
using Land.Markup.Binding;
using Land.Markup.CoreExtension;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DatasetToTrainConverter.CopyPaste
{
	public enum ContextType { Header, Ancestors, Inner }

	public class ContextFinder
	{
		public enum SearchType { Local, Global }

		public const double FILE_SIMILARITY_THRESHOLD = 0.8;
		public const double CANDIDATE_SIMILARITY_THRESHOLD = 0.65;
		public const double SECOND_DISTANCE_GAP_COEFFICIENT = 1.5;

		public Func<string, ParsedFile> GetParsed { get; set; }

		public PointContextManager ContextManager { get; private set; } = new PointContextManager();

		private Dictionary<ConcernPoint, List<RemapCandidateInfo>> DoMultiTypeSearch(
			Dictionary<string, List<ConcernPoint>> points,
			List<ParsedFile> searchArea,
			SearchType searchType)
		{
			var candidates = new Dictionary<string, List<RemapCandidateInfo>>();
			var ancestorsCache = new Dictionary<Node, AncestorCacheElement>();
			var candidateAncestor = new Dictionary<RemapCandidateInfo, Node>();

			/// Анализируем контекст соседей только при локальном поиске
			var checkSiblings = searchType == SearchType.Local;

			/// Инициализируем коллекции кандидатов для каждого типа
			foreach (var type in points.Keys)
			{
				candidates[type] = new List<RemapCandidateInfo>();
			}

			/// В каждом файле находим кандидатов нужного типа
			foreach (var currentFile in searchArea)
			{
				if (!EnsureRootExists(currentFile))
					continue;

				var visitor = new GroupNodesByTypeVisitor(points.Keys.ToList());
				currentFile.Root.Accept(visitor);

				foreach (var type in points.Keys)
				{
					candidates[type].AddRange(visitor.Grouped[type]
						.Select(n =>
						{
							var candidate = new RemapCandidateInfo
							{
								Node = n,
								File = currentFile,
								Context = ContextManager.GetContext(n, currentFile)
							};

							AncestorSiblingsPair pair = null;

							/// Если нужно проверить соседей, проверяем, не закешировали ли их
							if (checkSiblings)
							{
								/// Ищем предка, относительно которого нужно искать соседей
								var ancestor = PointContext.GetAncestor(n) 
									?? (n != currentFile.Root ? currentFile.Root : null);

								/// Если таковой есть, пытаемся найти инфу о нём в кеше
								if (ancestor != null)
								{
									pair = ancestorsCache.ContainsKey(ancestor)
										? new AncestorSiblingsPair
										{
											Ancestor = ancestor,
											Siblings = ancestorsCache[ancestor].Children
										}
										: new AncestorSiblingsPair
										{
											Ancestor = ancestor
										};
								}

								candidate.Context.SiblingsContext = PointContext.GetSiblingsContext(n, currentFile, pair);
								candidateAncestor[candidate] = ancestor;

								if (ancestor != null && !ancestorsCache.ContainsKey(ancestor))
								{
									ancestorsCache[ancestor] = new AncestorCacheElement
									{
										Children = pair.Siblings,
										PreprocessedChildren = pair.Siblings.ToLookup(e => e.Type, e =>
											  new Tuple<int, byte[]>(e.Location.Start.Offset, PointContext.GetHash(e, currentFile)))
									};
								}
							}

							return candidate;
						})
						.ToList()
					);
				}
			}

			var result = new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();

			foreach (var type in points.Keys)
			{
				var currentResult = DoSingleTypeSearch(points[type], candidates[type], 
					searchType, ancestorsCache, candidateAncestor);

				foreach(var kvp in currentResult)
				{
					result[kvp.Key] = kvp.Value;
				}
			}

			return result;
		}

		private Dictionary<ConcernPoint, List<RemapCandidateInfo>> DoSingleTypeSearch(
			List<ConcernPoint> points, 
			List<RemapCandidateInfo> candidates,
			SearchType searchType,
			Dictionary<Node, AncestorCacheElement> ancestorsCache,
			Dictionary<RemapCandidateInfo, Node> candidateAncestor)
		{
			var checkSiblings = searchType == SearchType.Local;
			var checkClosest = searchType == SearchType.Local;

			/// Запоминаем соответствие контекстов точкам привязки
			var contextsToPoints = points.GroupBy(p => p.Context)
				.ToDictionary(g => g.Key, g => g.ToList());

			var result = new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();
			var evaluated = new Dictionary<PointContext, List<RemapCandidateInfo>>();

			/// Для каждой точки оцениваем похожесть кандидатов, 
			/// если находим 100% соответствие, исключаем кандидата из списка
			foreach (var pointContext in contextsToPoints.Keys)
			{
				evaluated[pointContext] = candidates
					.Select(c => new RemapCandidateInfo
					{
						Node = c.Node,
						File = c.File,
						Context = c.Context,
						SiblingsSearchResult = checkSiblings ? new SiblingsSearchResult
						{
							BeforeSiblingOffset = pointContext.SiblingsContext.Before.IsNotEmpty && candidateAncestor[c] != null
							  ? ancestorsCache[candidateAncestor[c]].PreprocessedChildren[pointContext.SiblingsContext.Before.EntityType]
								  .FirstOrDefault(t => t.Item2.SequenceEqual(pointContext.SiblingsContext.Before.EntityHash))?.Item1
							  : null,
							AfterSiblingOffset = pointContext.SiblingsContext.After.IsNotEmpty && candidateAncestor[c] != null
							  ? ancestorsCache[candidateAncestor[c]].PreprocessedChildren[pointContext.SiblingsContext.After.EntityType]
								  .FirstOrDefault(t => t.Item2.SequenceEqual(pointContext.SiblingsContext.After.EntityHash))?.Item1
							  : null
						} : null
					})
					.ToList();

				ComputeSimilarities(
					pointContext,
					evaluated[pointContext],
					checkSiblings
				);
			}

			foreach (var elem in evaluated)
			{
				foreach (var point in contextsToPoints[elem.Key])
				{
					result[point] = elem.Value;
				}
			}

			return result;
		}

		public void ComputeCoreSimilarities(PointContext point, RemapCandidateInfo candidate)
		{
			candidate.HeaderSequenceSimilarity =
				Levenshtein(point.HeaderContext.Sequence, candidate.Context.HeaderContext.Sequence);
			candidate.HeaderCoreSimilarity =
				Levenshtein(point.HeaderContext.Core, candidate.Context.HeaderContext.Core);

			candidate.AncestorSimilarity =
				Levenshtein(point.AncestorsContext, candidate.Context.AncestorsContext);
			candidate.InnerSimilarity =
				EvalSimilarity(point.InnerContext, candidate.Context.InnerContext);
		}

		public List<RemapCandidateInfo> ComputeCoreSimilarities(PointContext point, List<RemapCandidateInfo> candidates)
		{
			Parallel.ForEach(
				candidates,
				c => ComputeCoreSimilarities(point, c)
			);

			return candidates;
		}

		public List<RemapCandidateInfo> ComputeSimilarities(
			PointContext point,
			List<RemapCandidateInfo> candidates,
			bool checkSiblings)
		{
			Parallel.ForEach(
				candidates,
				c =>
				{
					ComputeCoreSimilarities(point, c);

					if (checkSiblings)
					{
						c.SiblingsSimilarity =
							EvalSimilarity(point.SiblingsContext, c.Context.SiblingsContext);
					}
				}
			);

			return candidates;
		}

		/// <summary>
		/// Поиск узлов дерева, соответствующих точкам привязки
		/// </summary>
		public Dictionary<ConcernPoint, List<RemapCandidateInfo>> Find(
			List<ConcernPoint> points,
			List<ParsedFile> searchArea,
			SearchType searchType)
		{
			List<ParsedFile> files = null;
			Dictionary<string, List<ConcernPoint>> groupedByType = null;

			var overallResult = new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();

			switch (searchType)
			{
				case SearchType.Global:
					groupedByType = points
						.GroupBy(p => p.Context.Type)
						.ToDictionary(e => e.Key, e => e.ToList());
					files = searchArea;

					var globalResult = DoMultiTypeSearch(groupedByType, files, searchType);

					foreach (var elem in globalResult)
					{
						overallResult[elem.Key] = elem.Value;
					}

					break;
				case SearchType.Local:
					var groupedPoints = points
						.GroupBy(p => p.Context.FileContext)
						.ToDictionary(e => e.Key, e =>
							e.GroupBy(p => p.Context.Type).ToDictionary(el => el.Key, el => el.ToList())
						);

					foreach (var file in groupedPoints.Keys)
					{
						/// При поиске в том же файле ищем тот же файл по полному совпадению пути,
						/// если не находим - берём файлы, похожие по содержимому
						files = searchArea.Where(f => f.Name == file.Name).ToList();

						if (files.Count == 0)
						{
							files = searchArea
								.Where(f => AreFilesSimilarEnough(f.BindingContext, file))
								.ToList();
						}

						var localResult = DoMultiTypeSearch(groupedPoints[file], files, searchType);

						foreach (var elem in localResult)
						{
							overallResult[elem.Key] = elem.Value;
						}
					}

					break;
			}

			return overallResult;
		}

		public bool AreFilesSimilarEnough(FileContext a, FileContext b) =>
			EvalSimilarity(a.Content, b.Content) > FILE_SIMILARITY_THRESHOLD;

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

		public double EvalSimilarity(HeaderContextElement a, HeaderContextElement b)
		{
			if (a.EqualsIgnoreValue(b))
			{
				return a.ExactMatch
					? a.Value == b.Value ? 1 : 0
					: Levenshtein(a.Value, b.Value);
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

		public double EvalSimilarity(SiblingsContext a, SiblingsContext b)
		{
			if (a.Before.Global.TextLength == 0 && a.After.Global.TextLength == 0)
			{
				return b.Before.Global.TextLength == 0 && b.After.Global.TextLength == 0 ? 1 : 0;
			}

			var beforeSimilarity = EvalSimilarity(a.Before.Global, b.Before.Global);
			var afterSimilarity = EvalSimilarity(a.After.Global, b.After.Global);

			return (beforeSimilarity * a.Before.Global.TextLength + afterSimilarity * a.After.Global.TextLength) /
				(double)(a.Before.Global.TextLength + a.After.Global.TextLength);
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

		private bool IsSimilarEnough(RemapCandidateInfo candidate) =>
			candidate.Similarity >= CANDIDATE_SIMILARITY_THRESHOLD;

		private bool AreDistantEnough(RemapCandidateInfo first, RemapCandidateInfo second) =>
			second == null || first.Similarity == 1 && second.Similarity != 1
				|| 1 - second.Similarity >= (1 - first.Similarity) * SECOND_DISTANCE_GAP_COEFFICIENT;

		#endregion
	}
}
