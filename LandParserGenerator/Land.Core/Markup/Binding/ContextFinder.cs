using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Land.Core.Parsing.Tree;
using Land.Markup.CoreExtension;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Land.Markup.Binding
{
	public enum ContextType { HeaderCore, HeaderSequence, Ancestors, Inner, Siblings }

	public class ContextFinder
	{
		public enum SearchType { Local, Global }

		public const double FILE_SIMILARITY_THRESHOLD = 0.8;
		public const double CANDIDATE_SIMILARITY_THRESHOLD = 0.65;
		public const double SECOND_DISTANCE_GAP_COEFFICIENT = 1.5;

		public Func<string, ParsedFile> GetParsed { get; set; }

		public PointContextManager ContextManager { get; private set; } = new PointContextManager();

		public IHeuristic Heuristic { get; set; }

		public bool UseNaiveAlgorithm { get; set; }

		public Dictionary<ContextType, double> NaiveWeights { get; private set; } = new Dictionary<ContextType, double>
		{
			{ContextType.HeaderCore,  3},
			{ContextType.HeaderSequence,  1},
			{ContextType.Inner, 1},
			{ContextType.Ancestors, 2},
			{ContextType.Siblings, 0.5},
		};

		public List<CandidateFeatures> GetFeatures(
			PointContext point,
			List<RemapCandidateInfo> candidates)
		{
			if (candidates.Count > 0)
			{
				double CountRatio<T1, T2>(List<T1> num, List<T2> denom) =>
					denom.Count > 1 ? num.Count / (double)denom.Count : 0;

				double CountRatioConditional<T>(List<T> list, Func<T, bool> checkFunction) =>
					list.Count > 1 ? list.Where(e => checkFunction(e)).Count() / (double)list.Count : 0;

				double MaxIfAny(List<RemapCandidateInfo> list, Func<RemapCandidateInfo, double> getValue) =>
					list.Count > 0 ? list.Max(getValue) : 0;

				int BoolToInt(bool val) => val ? 1 : 0;

				return candidates.Select(c =>
				{
					var sameAncestorsCandidates = candidates.Where(cd => cd.AncestorSimilarity == c.AncestorSimilarity).ToList();
					sameAncestorsCandidates.Remove(c);

					var candidatesExceptCurrent = candidates.Except(new List<RemapCandidateInfo> { c }).ToList();

					var existsA_Point = point.AncestorsContext?.Count > 0;
					var existsHSeq_Point = point.HeaderContext?.Sequence?.Count > 0;
					var existsHCore_Point = point.HeaderContext?.Core?.Count > 0;
					var existsI_Point = point.InnerContext?.Content.TextLength > 0;
					var existsS_Point = point.SiblingsContext?.Before.Global.TextLength > 0 
						|| point.SiblingsContext?.After.Global.TextLength > 0;

					var existsA_Candidate = c.Context.AncestorsContext?.Count > 0;
					var existsHSeq_Candidate = c.Context.HeaderContext?.Sequence?.Count > 0;
					var existsHCore_Candidate = c.Context.HeaderContext?.Core?.Count > 0;
					var existsI_Candidate = c.Context.InnerContext?.Content.TextLength > 0;
					var existsS_Candidate = c.Context.SiblingsContext?.Before.Global.TextLength > 0 
						|| point.SiblingsContext?.After.Global.TextLength > 0;

					var maxSimA = MaxIfAny(candidatesExceptCurrent, cd => cd.AncestorSimilarity);
					var maxSimHSeq = MaxIfAny(candidatesExceptCurrent, cd => cd.HeaderSequenceSimilarity);
					var maxSimHCore = MaxIfAny(candidatesExceptCurrent, cd => cd.HeaderCoreSimilarity);
					var maxSimI = MaxIfAny(candidatesExceptCurrent, cd => cd.InnerSimilarity);
					var maxSimS = MaxIfAny(candidatesExceptCurrent, cd => cd.SiblingsSimilarity);

					var candidatesWithMaxSimA = candidatesExceptCurrent.Where(cd => cd.AncestorSimilarity == maxSimA).ToList();
					var candidatesWithMaxSimH = candidatesExceptCurrent.Where(cd => cd.HeaderSequenceSimilarity == maxSimHSeq).ToList();
					var candidatesWithMaxSimI = candidatesExceptCurrent.Where(cd => cd.InnerSimilarity == maxSimI).ToList();

					return new CandidateFeatures
					{
						ExistsA_Point = BoolToInt(existsA_Point),
						ExistsHSeq_Point = BoolToInt(existsHSeq_Point),
						ExistsHCore_Point = BoolToInt(existsHCore_Point),
						ExistsI_Point = BoolToInt(existsI_Point),
						ExistsS_Point = BoolToInt(existsS_Point),

						ExistsA_Candidate = BoolToInt(existsA_Candidate),
						ExistsHSeq_Candidate = BoolToInt(existsHSeq_Candidate),
						ExistsHCore_Candidate = BoolToInt(existsHCore_Candidate),
						ExistsI_Candidate = BoolToInt(existsI_Candidate),
						ExistsS_Candidate = BoolToInt(existsS_Candidate),

						SimHSeq = c.HeaderSequenceSimilarity,
						SimHCore = c.HeaderCoreSimilarity,
						SimI = c.InnerSimilarity,
						SimA = c.AncestorSimilarity,
						SimS = c.SiblingsSimilarity,
						
						AncestorHasBeforeSibling = BoolToInt(c.SiblingsSearchResult?.BeforeSiblingOffset.HasValue ?? false),
						AncestorHasAfterSibling = BoolToInt(c.SiblingsSearchResult?.AfterSiblingOffset.HasValue ?? false),
						CorrectBefore = BoolToInt(c.SiblingsSearchResult?.BeforeSiblingOffset < c.Node.Location.Start.Offset),
						CorrectAfter = BoolToInt(c.SiblingsSearchResult?.AfterSiblingOffset > c.Node.Location.Start.Offset),

						MaxSimA = maxSimA,
						MaxSimHSeq = maxSimHSeq,
						MaxSimHCore = maxSimHCore,
						MaxSimI = maxSimI,
						MaxSimS = maxSimS,

						MaxSimA_MaxSimH = MaxIfAny(candidatesWithMaxSimH, cd => cd.AncestorSimilarity),
						MaxSimI_MaxSimH = MaxIfAny(candidatesWithMaxSimH, cd => cd.InnerSimilarity),
						MaxSimA_MaxSimI = MaxIfAny(candidatesWithMaxSimI, cd => cd.AncestorSimilarity),
						MaxSimH_MaxSimI = MaxIfAny(candidatesWithMaxSimI, cd => cd.HeaderSequenceSimilarity),
						MaxSimH_MaxSimA = MaxIfAny(candidatesWithMaxSimA, cd => cd.HeaderSequenceSimilarity),
						MaxSimI_MaxSimA = MaxIfAny(candidatesWithMaxSimA, cd => cd.InnerSimilarity),

						MaxSimHSeq_SameA = MaxIfAny(sameAncestorsCandidates, cd => cd.HeaderSequenceSimilarity),
						MaxSimHCore_SameA = MaxIfAny(sameAncestorsCandidates, cd => cd.HeaderCoreSimilarity),
						MaxSimI_SameA = MaxIfAny(sameAncestorsCandidates, cd => cd.InnerSimilarity),
						MaxSimS_SameA = MaxIfAny(sameAncestorsCandidates, cd => cd.SiblingsSimilarity),

						RatioBetterSimA = CountRatioConditional(candidatesExceptCurrent, cd => cd.AncestorSimilarity > c.AncestorSimilarity),
						RatioBetterSimI = CountRatioConditional(candidatesExceptCurrent, cd => cd.InnerSimilarity > c.InnerSimilarity),
						RatioBetterSimH = CountRatioConditional(candidatesExceptCurrent, cd => cd.HeaderSequenceSimilarity > c.HeaderSequenceSimilarity),
						RatioBetterSimS = CountRatioConditional(candidatesExceptCurrent, cd => cd.SiblingsSimilarity > c.SiblingsSimilarity),

						RatioSameAncestor = CountRatio(sameAncestorsCandidates, candidates),

						RatioBetterSimI_SameA = CountRatioConditional(sameAncestorsCandidates, cd => cd.InnerSimilarity > c.InnerSimilarity),
						RatioBetterSimH_SameA = CountRatioConditional(sameAncestorsCandidates, cd => cd.HeaderSequenceSimilarity > c.HeaderSequenceSimilarity),
						RatioBetterSimS_SameA = CountRatioConditional(sameAncestorsCandidates, cd => cd.SiblingsSimilarity > c.SiblingsSimilarity),

						IsCandidateInnerContextLonger = BoolToInt(existsI_Candidate && (!existsI_Point
							|| c.Context.InnerContext.Content.TextLength > point.InnerContext.Content.TextLength)),
						InnerLengthRatio = existsI_Candidate && existsI_Point
							? Math.Min(c.Context.InnerContext.Content.TextLength, point.InnerContext.Content.TextLength)
								/ (double)Math.Max(c.Context.InnerContext.Content.TextLength, point.InnerContext.Content.TextLength)
							: 0,
						InnerLengthRatio1000_Point = Math.Min(point.InnerContext.Content.TextLength / (double)1000, 1),
						InnerLengthRatio1000_Candidate = Math.Min(c.Context.InnerContext.Content.TextLength / (double)1000, 1),

						IsCandidateHeaderCoreLonger = BoolToInt(existsHCore_Candidate && (!existsHCore_Point
							|| c.Context.HeaderContext.Words.Count > point.HeaderContext.Words.Count)),
						HeaderCoreLengthRatio = existsHCore_Point && existsHCore_Candidate
							? Math.Min(c.Context.HeaderContext.Words.Count, point.HeaderContext.Words.Count)
								/ (double)Math.Max(c.Context.HeaderContext.Words.Count, point.HeaderContext.Words.Count)
							: 0,

						IsAuto = BoolToInt(c.IsAuto),
					};
				}).ToList();
			}

			return new List<CandidateFeatures>();
		}

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

			/// Ближайшие элементы, которые не являются точками привязки
			var closestContexts = checkClosest
				? contextsToPoints.Keys
					.SelectMany(p => p.ClosestContext).Distinct()
					.Except(contextsToPoints.Keys)
					.ToList()
				: null;

			/// Результаты поиска элемента, которые вернём пользователю
			var result = new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();

			/// Контексты, которые нашлись на первом этапе (эвристикой)
			var heuristicallyMatched = new HashSet<PointContext>();

			if (Heuristic != null)
			{
				/// Для каждой точки оцениваем похожесть кандидатов, 
				/// если находим 100% соответствие, исключаем кандидата из списка
				foreach (var pointContext in contextsToPoints.Keys)
				{
					var perfectMatch = Heuristic.GetSameElement(pointContext, candidates);

					if (perfectMatch != null)
					{
						candidates.Remove(perfectMatch);
						heuristicallyMatched.Add(pointContext);

						perfectMatch.IsAuto = true;
						perfectMatch.Similarity = 1;

						ComputeSimilarities(pointContext, new List<RemapCandidateInfo> { perfectMatch }, checkSiblings);			

						foreach (var point in contextsToPoints[pointContext])
						{
							result[point] = new List<RemapCandidateInfo> { perfectMatch };
						}
					}
				}
			}

			/// Если всё перепривязали эвристикой, можно вернуть результат
			if (contextsToPoints.Keys.Except(heuristicallyMatched).Count() == 0)
			{
				return result;
			}

			/// Контексты, для которых первично посчитаны похожести кандидатов
			var evaluated = new Dictionary<PointContext, List<RemapCandidateInfo>>();

			foreach (var closestContext in closestContexts)
			{
				evaluated[closestContext] = ComputeSimilarities(
					closestContext,
					candidates.Select(c => new RemapCandidateInfo { Node = c.Node, File = c.File, Context = c.Context }).ToList(),
					false
				);
			}

			foreach (var pointContext in contextsToPoints.Keys.Except(heuristicallyMatched))
			{
				var currentCandidates = candidates
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

				evaluated[pointContext] = ComputeSimilarities(
					pointContext,
					currentCandidates,
					checkSiblings
				);
			}

			/// Считаем итоговые похожести
			if (UseNaiveAlgorithm)
			{
				foreach (var context in evaluated.Keys)
				{
					foreach (var candidate in evaluated[context])
					{
						candidate.Similarity = ComputeNaiveTotalSimilarity(candidate);
					}
				}
			}
			else
			{
				/// Запускаем ML для подсчёта итоговой похожести
				RunML(evaluated);
			}

			if (searchType == SearchType.Local)
			{
				/// Ищем оптимальное сопоставление кандидатов точкам и ближайшим
				OptimizeEvaluationResults(evaluated);
			}

			foreach (var kvp in evaluated.Where(kvp => contextsToPoints.ContainsKey(kvp.Key)))
			{
				foreach (var point in contextsToPoints[kvp.Key])
				{
					result[point] = kvp.Value;
				}
			}

			return result;
		}

		private void OptimizeEvaluationResults(
			Dictionary<PointContext, List<RemapCandidateInfo>> evaluationResults)
		{
			if (evaluationResults.Count > 0 && evaluationResults.First().Value.Count > 0)
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
			}
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

		public double ComputeNaiveTotalSimilarity(RemapCandidateInfo candidate) =>
			(NaiveWeights[ContextType.HeaderCore] * candidate.HeaderCoreSimilarity
			+ NaiveWeights[ContextType.HeaderSequence] * candidate.HeaderSequenceSimilarity
			+ NaiveWeights[ContextType.Ancestors] * candidate.AncestorSimilarity
			+ NaiveWeights[ContextType.Inner] * candidate.InnerSimilarity
			+ NaiveWeights[ContextType.Siblings] * candidate.SiblingsSimilarity)
			/ NaiveWeights.Values.Sum();

		public void RunML(Dictionary<PointContext, List<RemapCandidateInfo>> elements)
		{
			/// Создаём временный файл с данными о кандидатах для перепривязываемых точек
			var featuresFilePath = Path.GetTempFileName();

			/// Записываем информацию обо всех кандидатах
			using (var fileWriter = new StreamWriter(featuresFilePath))
			{
				fileWriter.WriteLine(CandidateFeatures.ToHeaderString(";"));

				foreach(var kvp in elements)
				{
					foreach(var str in GetFeatures(kvp.Key, kvp.Value).Select(e => e.ToString(";")))
					{
						fileWriter.WriteLine(str);
					}
				}
			}

			var predictionsFilePath = Path.GetTempFileName();

			/// Ищем подходящую модель для предсказания
			var availableModels = Directory.GetFiles("Resources/Models")
				.Select(m => Path.GetFullPath(m).ToLower())
				.ToList();
			/// Сначала ищем модель для типа файла и типа сущности
			var modelPath = availableModels.FirstOrDefault(m => 
				m.Contains(Path.GetExtension(elements.Keys.First().FileContext.Name).Trim('.'))
			);

			/// TODO если не нашли, нужно подгружать общую модель

			/// Запускаем питоновский скрипт
			var process = new Process();
			ProcessStartInfo startInfo = new ProcessStartInfo()
			{
				FileName = "python",
				Arguments = $"\"{Path.GetFullPath("Resources/apply_ml.py")}\" \"{modelPath}\" \"{featuresFilePath}\" \"{predictionsFilePath}\"",
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				UseShellExecute = false
			};
			process.StartInfo = startInfo;
			process.Start();

			process.WaitForExit();

			var predictions = File.ReadAllText(predictionsFilePath.Trim())
				.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(l => double.Parse(l))
				.ToList();

			var idx = 0;
			foreach (var kvp in elements)
			{
				foreach (var candidate in kvp.Value)
				{
					candidate.Similarity = predictions[idx++];
				}
			}
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
			return a.Type == b.Type ? Levenshtein(a.HeaderContext.Sequence, b.HeaderContext.Sequence) : 0;
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
