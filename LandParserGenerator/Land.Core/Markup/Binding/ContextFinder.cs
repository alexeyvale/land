﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Land.Core.Parsing.Tree;
using Land.Markup.CoreExtension;
using System.Threading.Tasks;

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

		public enum SearchType { Local, Global }

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
			void AddToList<T>(ref List<T> heuristics) where T: IHeuristic 
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

			var tuningHeuristics = TuningHeuristics;
			AddToList(ref tuningHeuristics);
			TuningHeuristics = tuningHeuristics;

			var scoringHeuristics = ScoringHeuristics;
			AddToList(ref scoringHeuristics);
			ScoringHeuristics = scoringHeuristics;
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
				/// если не находим - берём файлы, похожие по содержимому
				case SearchType.Local:
					files = searchArea.Where(f => f.Name == file.Name).ToList();

					if (files.Count == 0)
					{
						files = searchArea
							.Where(f => AreFilesSimilarEnough(f.BindingContext, file))
							.ToList();
					}
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

			/// Запоминаем соответствие контекстов точкам привязки
			var contextsToPoints = points.GroupBy(p => p.Context)
				.ToDictionary(g=>g.Key, g=>g.ToList());

			var result = new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();
			var evaluated = new Dictionary<PointContext, List<RemapCandidateInfo>>();

			/// Для каждой точки оцениваем похожесть кандидатов, 
			/// если находим 100% соответствие, исключаем кандидата из списка
			foreach (var pointContext in contextsToPoints.Keys)
			{
				var currentCandidates = candidates
					.Select(c => new RemapCandidateInfo { Node = c.Node, File = c.File, Context = c.Context })
					.ToList();

				EvalCandidates(
					pointContext, 
					currentCandidates, 
					files.First().MarkupSettings,
					searchType == SearchType.Global
				);

				var bestMatch = currentCandidates.FirstOrDefault(c=>c.Similarity == 1);

				if (bestMatch?.Similarity == 1)
				{
					var bestMatchIdx = currentCandidates.IndexOf(bestMatch);

					candidates.RemoveAt(bestMatchIdx);
					foreach (var list in evaluated.Values)
					{
						list.RemoveAt(bestMatchIdx);
					}
					foreach(var list in result.Values.Distinct())
					{
						list.RemoveAll(e=>e.Context == bestMatch.Context);
					}

					currentCandidates = currentCandidates.OrderByDescending(c => c.Similarity).ToList();
					currentCandidates[0].IsAuto = true;

					foreach (var point in contextsToPoints[pointContext])
					{
						result[point] = currentCandidates;
					}
				}
				else
				{
					evaluated[pointContext] = currentCandidates;
				}
			}

			/// Признак того, что не нужно искать оптимальное паросочетание
			var ignoreClosest = files.First().MarkupSettings.UseSiblingsContext
				|| searchType == SearchType.Global;

			if (!ignoreClosest)
			{
				/// Для точек, 100% соответствие которым не найдено, 
				/// считаем похожести ближайших на кандидатов
				foreach (var pointContext in evaluated.Keys
					.SelectMany(p=>p.ClosestContext).Distinct()
					.Except(evaluated.Keys)
					.Except(result.Select(e=>e.Key.Context))
					.ToList())
				{
					evaluated[pointContext] = EvalCandidates(
						pointContext,
						candidates.Select(c => new RemapCandidateInfo { Node = c.Node, File = c.File, Context = c.Context }).ToList(), 
						files.First().MarkupSettings,
						false
					);
				}

				var resultsForEvaluated = OptimizeEvaluationResults(evaluated, contextsToPoints);

				foreach(var elem in resultsForEvaluated)
				{
					result[elem.Key] = elem.Value; 
				}
			}
			else
			{
				foreach (var elem in evaluated)
				{
					foreach (var point in contextsToPoints[elem.Key])
					{
						result[point] = elem.Value;
					}
				}
			}

			return result;
		}

		private Dictionary<ConcernPoint, List<RemapCandidateInfo>> OptimizeEvaluationResults(
			Dictionary<PointContext, List<RemapCandidateInfo>> evaluationResults,
			Dictionary<PointContext, List<ConcernPoint>> contextsToPoints)
		{
			var result = new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();

			if(evaluationResults.Count == 0)
			{
				return result;
			}

			/// На данном этапе в словаре результаты сравнения точек, которые не смогли перепривязать
			/// со стопроцентной вероятностью, и сравнения  ближайших к ним. 
			/// Сразу обрабатываем стопроцентные совпадения ближайших,
			/// уменьшая размерность задачи поиска паросочетания максимального веса
			foreach (var src in evaluationResults.Keys.ToList())
			{
				var perfectMatch = evaluationResults[src].FirstOrDefault(e => e.Similarity == 1);

				if(perfectMatch != null)
				{
					var candidateIndex = evaluationResults[src].IndexOf(perfectMatch);
					evaluationResults.Remove(src);

					foreach (var val in evaluationResults.Values)
					{
						val.RemoveAt(candidateIndex);
					}
				}
			}

			/// Убираем из списка кандидатов тех, которые ни на что не похожи в достаточной степени
			var lowSimilarityCandidates = Enumerable.Range(0, evaluationResults.First().Value.Count)
				.Where(i => evaluationResults.All(e => e.Value[i].Similarity < CANDIDATE_SIMILARITY_THRESHOLD))
				.Reverse()
				.ToList();

			foreach (var idx in lowSimilarityCandidates)
			{
				foreach (var list in evaluationResults.Values)
				{
					list.RemoveAt(idx);
				}
			}

			/// Если остались кандидаты
			if (evaluationResults.First().Value.Count > 0)
			{
				var scores = new int[
					evaluationResults.Count,
					evaluationResults.First().Value.Count
				];
				var indicesToContexts = new PointContext[evaluationResults.Count];

				var i = 0;
				foreach (var from in evaluationResults)
				{
					indicesToContexts[i] = from.Key;

					for (var j = 0; j < from.Value.Count; ++j)
					{
						scores[i, j] = (int)(1000 * (1 - (from.Value[j].Similarity ?? 0)));
					}
					++i;
				}

				/// Запускаем венгерский алгоритм для поиска паросочетания минимального веса
				var bestMatchesFinder = new AssignmentProblem();
				var bestMatches = bestMatchesFinder.Compute1(scores);

				var bestContextMatches = indicesToContexts
					.Select((context, idx) => new
					{
						context,
						bestCandidateContext =
						bestMatches[idx] != -1 ? evaluationResults[context][bestMatches[idx]].Context : null
					})
					.ToList();

				Parallel.ForEach(
					evaluationResults.Keys.ToList(),
					key => evaluationResults[key] = evaluationResults[key].OrderByDescending(c => c.Similarity).ToList()
				);

				int oldCount;

				do
				{
					oldCount = bestContextMatches.Count;

					/// Проходим по найденным наилучшим соответствиям
					for (i = 0; i < bestContextMatches.Count; ++i)
					{
						/// Если найденное алгоритмом поиска паросочетания соответствие
						/// наилучшее в смысле общей похожести
						if (evaluationResults[bestContextMatches[i].context].Count > 0
							&& bestContextMatches[i].bestCandidateContext ==
								evaluationResults[bestContextMatches[i].context][0].Context)
						{
							/// проверяем для него условие автоматического принятия решения.
							var first = evaluationResults[bestContextMatches[i].context][0];
							var second = evaluationResults[bestContextMatches[i].context].Count > 1
								? evaluationResults[bestContextMatches[i].context][1] : null;

							/// Если оно выполняется, удаляем наилучшего кандидата 
							/// из списков кандидатов для остальных элементов
							if (IsSimilarEnough(first)
								&& (second == null || AreDistantEnough(first, second)))
							{
								first.IsAuto = true;

								foreach (var key in evaluationResults.Keys)
								{
									if (key != bestContextMatches[i].context)
									{
										var itemToRemove = evaluationResults[key].Single(e => e.Context == first.Context);
										evaluationResults[key].Remove(itemToRemove);
									}
								}

								/// Автоматически приняли решение для данного соответствия.
								bestContextMatches.RemoveAt(i);
								--i;
							}
						}
					}
				}
				while (bestContextMatches.Count != oldCount);

				foreach (var kvp in evaluationResults.Where(kvp => contextsToPoints.ContainsKey(kvp.Key)))
				{
					foreach (var point in contextsToPoints[kvp.Key])
					{
						result[point] = kvp.Value;
					}
				}
			}
			/// Остались точки, которые нужно перепривязать, но не осталось кандидатов
			else
			{
				foreach (var context in evaluationResults.Keys.Where(k => contextsToPoints.ContainsKey(k)))
				{
					foreach (var point in contextsToPoints[context])
					{
						result[point] = new List<RemapCandidateInfo>();
					}
				}
			}

			return result;
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
			bool orderAndSetAuto = true)
		{
			Parallel.ForEach(
				candidates, 
				c => ComputeCoreContextSimilarities(point, c)
			);

			ComputeTotalSimilarity(point, candidates);

			if(orderAndSetAuto)
			{
				candidates = candidates.OrderByDescending(c => c.Similarity).ToList();

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
			}

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
			List<ParsedFile> searchArea,
			SearchType searchType)
		{
			var groupedPoints = points
				.GroupBy(p => new { p.Context.Type, FileName = p.Context.FileContext.Name })
				.ToDictionary(e=>e.Key, e=>e.ToList());

			var overallResult = new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();

			foreach (var groupKey in groupedPoints.Keys)
			{
				var groupResult = DoSearch(groupedPoints[groupKey], searchArea, searchType);

				foreach(var elem in groupResult)
				{
					overallResult[elem.Key] = elem.Value;
				}
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

		private bool IsSimilarEnough(RemapCandidateInfo candidate) =>
			candidate.Similarity >= CANDIDATE_SIMILARITY_THRESHOLD;

		private bool AreDistantEnough(RemapCandidateInfo first, RemapCandidateInfo second) =>
			second == null || first.Similarity == 1 && second.Similarity != 1
				|| 1 - second.Similarity >= (1 - first.Similarity) * SECOND_DISTANCE_GAP_COEFFICIENT;

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

			foreach (var h in TuningHeuristics)
				h.TuneWeights(sourceContext, candidates, weights);

			foreach (var h in ScoringHeuristics)
				h.PredictSimilarity(sourceContext, candidates);

			var finalWeights = weights.ToDictionary(e => e.Key, e => e.Value ?? 0);

			candidates.ForEach(c =>
			{
				c.Similarity = c.Similarity ??
					(finalWeights[ContextType.Ancestors] * c.AncestorSimilarity + finalWeights[ContextType.Inner] * c.InnerSimilarity + finalWeights[ContextType.Header] * c.HeaderSimilarity)
					/ finalWeights.Values.Sum();
				c.Weights = finalWeights;
			});
		}

		#endregion
	}
}