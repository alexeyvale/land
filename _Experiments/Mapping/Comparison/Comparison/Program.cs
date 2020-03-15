using Land.Core.Parsing.Tree;
using Land.Markup;
using Land.Markup.Binding;
using Land.Markup.CoreExtension;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Comparison
{
	class Program
	{
		const string MarkupFolder = @"D:\Repositories\_mapping\Roslyn\Common\base";
		const string RelinkFolder = @"D:\Repositories\_mapping\Roslyn\Common\modified";
		const int EntitiesPerFile = 3;
		const int FilesTake = 1000;
		static readonly Random RandomGen = new Random(7);

		public class GetNodeSequenceVisitor: BaseTreeVisitor
		{
			public List<Node> Sequence = new List<Node>();

			public override void Visit(Node node)
			{
				Sequence.Add(node);
				base.Visit(node);
			}
		}

		/// <summary>
		/// Парсинг набора файлов и получение их АСТ в форматах LanD и Core
		/// </summary>
		static List<ParsedFile> GetSearchArea(
			Land.Core.Parsing.BaseParser landParser,
			List<string> files,
			List<string> landErrors)
		{
			var landSearchArea = new List<ParsedFile>();

			var counter = 0;
			var start = DateTime.Now;

			foreach (var file in files)
			{
				/// Читаем текст из файла
				var text = File.ReadAllText(file);

				/// Парсим при помощи LanD
				var landRoot = landParser.Parse(text);
				if (landParser.Log.Any(l => l.Type == Land.Core.MessageType.Error))
					landErrors.Add(file);

				var visitor = new NodeRetypingVisitor(null);
				landRoot.Accept(visitor);
				landRoot = visitor.Root;

				var landParsed = new ParsedFile
				{
					BindingContext = PointContext.GetFileContext(Path.GetFileName(file), text),
					MarkupSettings = new LanguageMarkupSettings(new Land.Core.Specification.SymbolOptionsManager()),
					Root = landRoot,
					Text = text
				};

				landSearchArea.Add(landParsed);

				++counter;
				if (counter % 600 == 0)
					Console.WriteLine($"{counter} out of {files.Count}...");
			}

			Console.WriteLine($"LanD parsing done in {DateTime.Now - start}");

			return landSearchArea;
		}

		static void Main(string[] args)
		{
			var entities = new Dictionary<string, MarkupManager>
			{
				{ "class_struct_interface", new MarkupManager(null) },
				{ "method", new MarkupManager(null) },
				{ "field", new MarkupManager(null) },
				{ "property", new MarkupManager(null) },
			};

			/// Создаём парсер C# и менеджер разметки из библиотеки LanD	
			var landParser = sharp.ParserProvider.GetParser(false);
			var markupSettings = new LanguageMarkupSettings(landParser.GrammarObject.Options.GetOptions());
			landParser.SetVisitor(g => new MarkupOptionsProcessingVisitor(g));
			landParser.SetPreprocessor(new SharpPreprocessing.ConditionalCompilation.SharpPreprocessor());

			var landErrors = new List<string>();

			/////////////////////////////////////////////// STAGE 1

			var counter = 0;
			var filesList = Directory.GetFiles(MarkupFolder, "*.cs").ToList();

			/// Случайным образом отбираем заданное количество файлов
			var files = new HashSet<string>();
			if (filesList.Count > FilesTake)
			{
				while (files.Count < FilesTake)
					files.Add(filesList[RandomGen.Next(filesList.Count)]);
			}
			else
			{
				files.UnionWith(filesList);
			}

			if (Directory.Exists("./test"))
				Directory.Delete("./test", true);
			Directory.CreateDirectory("test");
			foreach (var file in files)
				File.Copy(file, $"./test/{Path.GetFileName(file)}");

			/// Парсим отобранные файлы
			var searchArea = GetSearchArea(landParser, files.ToList(), landErrors);

			/// Привязываемся к сущностям, случайным образом выбирая нужное их количество в каждом файле
			for(var j=0; j< searchArea.Count; ++j)
			{		
				var visitor = new GetNodeSequenceVisitor();
				searchArea[j].Root.Accept(visitor);

				foreach(var key in entities.Keys)
				{
					var subseq = visitor.Sequence.Where(n => n.Type == key).ToList();

					for (var i = 0; i < EntitiesPerFile; ++i)
					{
						if (subseq.Count == 0) break;
						var index = RandomGen.Next(0, subseq.Count);

						entities[key].AddConcernPoint(
							subseq[index], 
							searchArea[j], 
							searchArea, 
							null);

						subseq.RemoveAt(index);
					}
				}

				++counter;
				if (counter % 600 == 0)
					Console.WriteLine($"{counter} out of {files.Count}...");
			}

			/////////////////////////////////////////////// STAGE 2

			Console.WriteLine("Stage 2 started...");

			counter = 0;
			files = new HashSet<string>(files.Select(f => Path.Combine(RelinkFolder, Path.GetFileName(f))));

			searchArea = GetSearchArea(landParser, files.ToList(), landErrors);

			Console.WriteLine("Remapping...");

			var report = new StreamWriter("report.txt");

			foreach (var key in entities.Keys)
			{
				List<Tuple<string, string>> sameAutoResult = new List<Tuple<string, string>>(),
					differentAutoResult = new List<Tuple<string, string>>(),
					modifiedOnlyAutoResult = new List<Tuple<string, string>>(),
					basicOnlyAutoResult = new List<Tuple<string, string>>(),
					sameFirstPos = new List<Tuple<string, string>>(),
					differentFirstPos = new List<Tuple<string, string>>();

				var similarities = new List<string>();
				var start = DateTime.Now;

				var modifiedRemapResult = entities[key].Remap(searchArea, false, ContextFinder.SearchType.Local);

				Console.WriteLine($"Modified remapping done in {DateTime.Now - start}");

				start = DateTime.Now;

				var helper = new Helper();
				var basicRemapResult = helper.Remap(entities[key].GetConcernPoints(), searchArea);

				Console.WriteLine($"Basic remapping done in {DateTime.Now - start}");

				foreach (var cp in modifiedRemapResult.Keys)
				{
					var isModifiedAuto = modifiedRemapResult[cp].FirstOrDefault()?.IsAuto ?? false;
					var isBasicAuto = basicRemapResult[cp].FirstOrDefault()?.IsAuto ?? false;

					var sameFirst = basicRemapResult[cp].Count == 0 && modifiedRemapResult[cp].Count == 0 ||
						basicRemapResult[cp].Count > 0 && modifiedRemapResult[cp].Count > 0 &&
						modifiedRemapResult[cp][0].Context.HeaderContext.SelectMany(h => h.Value)
							.SequenceEqual(basicRemapResult[cp][0].Context.HeaderContext.SelectMany(h => h.Value));

					var hasChanged = modifiedRemapResult[cp].Count == 0 ||
						modifiedRemapResult[cp][0].HeaderSimilarity != 1 ||
						modifiedRemapResult[cp][0].InnerSimilarity != 1 ||
						modifiedRemapResult[cp][0].AncestorSimilarity != 1;

					if (hasChanged || !isModifiedAuto || !isBasicAuto)
					{
						if (basicRemapResult[cp].Count == 1 && modifiedRemapResult[cp].Count == 1)
							similarities.Add($"{ basicRemapResult[cp][0].Similarity };{ modifiedRemapResult[cp][0].Similarity }");
						else if (basicRemapResult[cp].Count > 1 && modifiedRemapResult[cp].Count > 1)
							similarities.Add($"{ basicRemapResult[cp][0].Similarity };{ modifiedRemapResult[cp][0].Similarity };{ basicRemapResult[cp][1].Similarity };{ modifiedRemapResult[cp][1].Similarity }");

						report.WriteLine(Path.GetFileName(cp.Context.FileContext.Name));
						report.WriteLine("*");

						report.WriteLine(String.Join(" ", cp.Context.HeaderContext.SelectMany(c => c.Value)));
						report.WriteLine("*");

						foreach (var landCandidate in basicRemapResult[cp].Take(5))
						{
							report.WriteLine(String.Join(" ", landCandidate.Context.HeaderContext.SelectMany(c => c.Value)));
							report.WriteLine($"{landCandidate.Similarity}  [{landCandidate.NameSimilarity}; {landCandidate.HeaderSimilarity}; {landCandidate.InnerSimilarity}; {landCandidate.AncestorSimilarity}] {(landCandidate.IsAuto ? "*" : "")}");
						}

						report.WriteLine("*");

						foreach (var landCandidate in modifiedRemapResult[cp].Take(5))
						{
							report.WriteLine(String.Join(" ", landCandidate.Context.HeaderContext.SelectMany(c => c.Value)));
							report.WriteLine($"{landCandidate.Similarity}  [{landCandidate.HeaderSimilarity}; {landCandidate.InnerSimilarity}; {landCandidate.AncestorSimilarity}]  " +
								$"[{landCandidate.Weights[ContextType.Header]}; {landCandidate.Weights[ContextType.Inner]}; {landCandidate.Weights[ContextType.Ancestors]}] {(landCandidate.IsAuto ? "*" : "")}");
						}
						report.WriteLine();
						report.WriteLine("**************************************************************");
						report.WriteLine();

						var tuple = new Tuple<string, string>(
								cp.Context.FileContext.Name,
								String.Join(" ", cp.Context.HeaderContext.SelectMany(h => h.Value))
							);

						if (isModifiedAuto)
						{
							if (isBasicAuto)
							{
								if (sameFirst)
									sameAutoResult.Add(tuple);
								else
									differentAutoResult.Add(tuple);
							}
							else
							{
								modifiedOnlyAutoResult.Add(tuple);
							}
						}
						else if (isBasicAuto)
						{
							basicOnlyAutoResult.Add(tuple);
						}
						else
						{
							if (sameFirst)
								sameFirstPos.Add(tuple);
							else
								differentFirstPos.Add(tuple);
						}
					}
				}
				File.WriteAllLines($"{key}_similarities.txt", similarities);
				File.WriteAllLines($"{key}_basicOnlyAutoResult.txt",
					basicOnlyAutoResult.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
				File.WriteAllLines($"{key}_modifiedOnlyAutoResult.txt",
					modifiedOnlyAutoResult.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
				File.WriteAllLines($"{key}_sameAutoResult.txt",
					sameAutoResult.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
				File.WriteAllLines($"{key}_differentAutoResult.txt",
					differentAutoResult.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
				File.WriteAllLines($"{key}_sameFirstPos.txt",
					sameFirstPos.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
				File.WriteAllLines($"{key}_differentFirstPos.txt",
					differentFirstPos.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));

				Console.WriteLine($"Total: {modifiedRemapResult.Count}");
				Console.WriteLine($"Modified only auto: {modifiedOnlyAutoResult.Count}");
				Console.WriteLine($"Basic only auto: {basicOnlyAutoResult.Count}");
				Console.WriteLine($"Same auto: {sameAutoResult.Count}");
				Console.WriteLine($"Different auto: {differentAutoResult.Count}");
				Console.WriteLine($"Same first: {sameFirstPos.Count}");
				Console.WriteLine($"Different first: {differentFirstPos.Count}");
				Console.WriteLine($"'{key}' done!");
			}

			report.Close();

			Console.WriteLine("Job's done!");
			Console.ReadLine();
		}
	}

	public class Helper
	{
		public const double CANDIDATE_SIMILARITY_THRESHOLD = 0.6;
		public const double SECOND_DISTANCE_GAP_COEFFICIENT = 1.5;
		public const int INNER_CONTEXT_LENGTH = 10;

		public Dictionary<ConcernPoint, List<RemapCandidateInfo>> Remap(
			List<ConcernPoint> points,
			List<ParsedFile> searchArea)
		{
			var groupedPoints = points
				.GroupBy(p => new { p.Context.Type, FileName = p.Context.FileContext.Name })
				.ToDictionary(e => e.Key, e => e.ToList());

			var overallResult = new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();

			foreach (var groupKey in groupedPoints.Keys)
			{
				var groupResult = DoSearch(groupedPoints[groupKey], searchArea);

				foreach (var elem in groupResult)
				{
					overallResult[elem.Key] = elem.Value;
				}
			}

			return overallResult;
		}

		public Dictionary<ConcernPoint, List<RemapCandidateInfo>> DoSearch(
			List<ConcernPoint> points,
			List<ParsedFile> searchArea)
		{
			var type = points[0].Context.Type;
			var file = points[0].Context.FileContext;

			var files = searchArea.Where(f => f.Name == file.Name).ToList();

			var candidates = new List<RemapCandidateInfo>();

			/// Находим все сущности того же типа
			foreach (var currentFile in files)
			{
				var visitor = new GroupNodesByTypeVisitor(new List<string> { type });
				currentFile.Root.Accept(visitor);

				candidates.AddRange(visitor.Grouped[type]
					.Select(n => new RemapCandidateInfo
					{
						Node = n,
						File = currentFile,
						Context = PointContext.GetCoreContext(n, currentFile)
					})
					.ToList()
				);
			}

			/// Запоминаем соответствие контекстов точкам привязки
			var contextsToPoints = points.GroupBy(p => p.Context)
				.ToDictionary(g => g.Key, g => g.ToList());

			var contextsSet = new HashSet<PointContext>();
			contextsSet.UnionWith(points.Select(p => p.Context));

			var evaluationResults = contextsSet.ToDictionary(p => p,
				p => candidates.Select(c => new RemapCandidateInfo { Node = c.Node, File = c.File, Context = c.Context }).ToList());

			foreach (var key in evaluationResults.Keys.ToList())
			{
				evaluationResults[key] = EvalCandidates(key, evaluationResults[key], files.First().MarkupSettings,
					CANDIDATE_SIMILARITY_THRESHOLD);
			}

			var result = contextsToPoints.SelectMany(e => e.Value).ToDictionary(e => e, e => evaluationResults[e.Context]);

			return files.Count > 0 ? result : null;
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

			return candidates;
		}

		private void ComputeCoreContextSimilarities(PointContext point, RemapCandidateInfo candidate)
		{
			candidate.NameSimilarity =
				Levenshtein(point.Name, candidate.Context.Name);
			candidate.HeaderSimilarity =
				Levenshtein(point.HeaderContext, candidate.Context.HeaderContext);
			candidate.AncestorSimilarity =
				Levenshtein(point.AncestorsContext, candidate.Context.AncestorsContext);
			candidate.InnerSimilarity =
				EvalSimilarity(point.InnerContext_old, candidate.Context.InnerContext_old);
		}

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
			else if (a is AncestorsContextElement)
				return EvalSimilarity(a as AncestorsContextElement, b as AncestorsContextElement);
			else
				return a.Equals(b) ? 1 : 0;
		}

		#region EvalSimilarity

		private double EvalSimilarity(List<ContextElement> a, List<ContextElement> b)
		{
			var source = a.Take(INNER_CONTEXT_LENGTH).ToList();
			var @new = b.Take(INNER_CONTEXT_LENGTH).ToList();

			return source.Count > 0
				? source.Intersect(@new).Count() / (double)source.Count 
				: @new.Count == 0 ? 1 : 0;
		}
		
		public double EvalSimilarity(List<HeaderContextElement> a, List<HeaderContextElement> b) =>
			Levenshtein(a, b);

		public double EvalSimilarity(HeaderContextElement a, HeaderContextElement b)
		{
			return Levenshtein(String.Join("", a.Value), String.Join("", b.Value));
		}

		public double EvalSimilarity(AncestorsContextElement a, AncestorsContextElement b)
		{
			return a.Type == b.Type ? Levenshtein(a.HeaderContext, b.HeaderContext) : 0;
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

			var denominator  = Math.Max(a.Count(), b.Count());

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
				distances[i, 0] = distances[i - 1, 0] + 1;
			for (int j = 1; j <= b.Count(); ++j)
				distances[0, j] = distances[0, j - 1] + 1;

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

		private bool IsSimilarEnough(RemapCandidateInfo candidate, double threshold) =>
			candidate.Similarity >= threshold;

		private bool AreDistantEnough(RemapCandidateInfo first, RemapCandidateInfo second) =>
			second == null || second.Similarity != 1
				&& 1 - second.Similarity >= (1 - first.Similarity) * SECOND_DISTANCE_GAP_COEFFICIENT;

		private void ComputeTotalSimilarity(PointContext sourceContext,
			List<RemapCandidateInfo> candidates)
		{
			candidates.ForEach(c => c.Similarity = c.Similarity ??
				(2 * c.AncestorSimilarity + 1 * c.InnerSimilarity + 1 * c.HeaderSimilarity + 3 * c.NameSimilarity) / 7);
		}

		#endregion
	}
}
