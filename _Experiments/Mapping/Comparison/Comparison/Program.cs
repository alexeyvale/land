using Land.Core.Parsing.Tree;
using Land.Markup;
using Land.Markup.Binding;
using Land.Markup.CoreExtension;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Comparison
{
	class Program
	{
		const string MarkupFolder = @"D:\Repositories\_mapping\ASP.NET Core\3551\Common\base";
		const string RelinkFolder = @"D:\Repositories\_mapping\ASP.NET Core\3551\Common\modified";

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

				var landParsed = new ParsedFile
				{
					BindingContext = PointContext.GetFileContext(Path.GetFileName(file), text),
					Root = landRoot,
					Text = text
				};

				landSearchArea.Add(landParsed);

				++counter;
				if (counter % 100 == 0)
					Console.WriteLine($"{counter} out of {files.Count}...");
			}

			Console.WriteLine($"LanD parsing done in {DateTime.Now - start}");

			return landSearchArea;
		}

		static void Main(string[] args)
		{
			var heuristic = new ProgrammingLanguageHeuristic();
			var markupManager = new MarkupManager(null, heuristic);
			var entityTypes = new string[] { "class_struct_interface", "method", "field", "property" };

			/// Создаём парсер C# и менеджер разметки из библиотеки LanD	
			var landParser = sharp.ParserProvider.GetParser(false);
			landParser.SetVisitor(g => new MarkupOptionsProcessingVisitor(g));
			landParser.SetPreprocessor(new SharpPreprocessing.ConditionalCompilation.SharpPreprocessor());

			var landErrors = new List<string>();

			/////////////////////////////////////////////// STAGE 1

			var counter = 0;
			var files = new HashSet<string>(Directory.GetFiles(MarkupFolder, "*.cs"));

			/// Парсим отобранные файлы
			var searchArea = GetSearchArea(landParser, files.ToList(), landErrors);
			var start = DateTime.Now;

			foreach(var file in searchArea)
			{		
				markupManager.AddLand(file, searchArea);

				++counter;
				if (counter % 100 == 0)
				{
					Console.WriteLine($"{counter} out of {files.Count} in {DateTime.Now - start}...");
				}
			}

			Console.WriteLine($"Binding done in {DateTime.Now - start}");

			/////////////////////////////////////////////// STAGE 2

			Console.WriteLine("Stage 2 started...");

			counter = 0;
			files = new HashSet<string>(files.Select(f => Path.Combine(RelinkFolder, Path.GetFileName(f))));

			searchArea = GetSearchArea(landParser, files.ToList(), landErrors);

			Console.WriteLine("Remapping...");

			var report = new StreamWriter("report.txt");

			start = DateTime.Now;
			markupManager.ContextFinder.UseNaiveAlgorithm = false;
			markupManager.ContextFinder.Optimization = ContextFinder.OptimizationType.LocalBest;
			var modifiedRemapResult = markupManager.Remap(searchArea, false, ContextFinder.SearchType.Local);
			Console.WriteLine($"Modified remapping done in {DateTime.Now - start}");

			start = DateTime.Now;
			markupManager.ContextFinder.UseNaiveAlgorithm = true;
			markupManager.ContextFinder.Optimization = ContextFinder.OptimizationType.LocalBest;
			var basicRemapResult = markupManager.Remap(searchArea, false, ContextFinder.SearchType.Local);
			Console.WriteLine($"Base remapping done in {DateTime.Now - start}");

			foreach (var key in entityTypes)
			{
				var pointsOfType = modifiedRemapResult.Keys
					.Where(e => e.Context.Type == key).ToList();

				List<List<string>> sameAutoResult = new List<List<string>>(),
					differentAutoResult = new List<List<string>>(),
					modifiedOnlyAutoResult = new List<List<string>>(),
					basicOnlyAutoResult = new List<List<string>>(),
					sameFirstPos = new List<List<string>>(),
					differentFirstPos = new List<List<string>>();

				var similarities = new List<string>();

				foreach (var cp in pointsOfType)
				{
					var modifiedResult = modifiedRemapResult[cp].Where(e => !e.Deleted).ToList();
					var basicResult = basicRemapResult[cp].Where(e => !e.Deleted).ToList();

					var isModifiedAuto = modifiedResult.FirstOrDefault()?.IsAuto ?? false;
					var isBasicAuto = basicResult.FirstOrDefault()?.IsAuto ?? false;

					var sameFirst = basicResult.Count == 0 && modifiedResult.Count == 0 ||
						basicResult.Count > 0 && modifiedResult.Count > 0 &&
						modifiedResult[0].Context.HeaderContext.Sequence_old
							.SequenceEqual(basicResult[0].Context.HeaderContext.Sequence_old);

					/// Отсекаем элементы, привязку к которым можно обеспечить за счёт базовой эвристики
					var hasNotChanged = modifiedRemapResult[cp].Count == 1 
						&& modifiedRemapResult[cp][0].Weights == null;

					if (!hasNotChanged)
					{
						if (basicRemapResult[cp].Count == 1 && modifiedRemapResult[cp].Count == 1)
							similarities.Add($"{ basicRemapResult[cp][0].Similarity };{ modifiedRemapResult[cp][0].Similarity }");
						else if (basicRemapResult[cp].Count > 1 && modifiedRemapResult[cp].Count > 1)
							similarities.Add($"{ basicRemapResult[cp][0].Similarity };{ modifiedRemapResult[cp][0].Similarity };{ basicRemapResult[cp][1].Similarity };{ modifiedRemapResult[cp][1].Similarity }");

						var reportLines = new List<string>();

						reportLines.Add($"file:///{MarkupFolder}\\{cp.Context.FileContext.Name}");
						reportLines.Add($"file:///{RelinkFolder}\\{cp.Context.FileContext.Name}");
						reportLines.Add("*");

						reportLines.Add($"{String.Join(" ", cp.Context.HeaderContext.Sequence_old)}     {cp.Context.Line}");
						reportLines.Add("*");

						foreach (var landCandidate in basicRemapResult[cp].Take(7))
						{
							reportLines.Add($"{String.Join(" ", landCandidate.Context.HeaderContext.Sequence_old)}     {landCandidate.Context.Line}");
							reportLines.Add($"\t{landCandidate.Similarity:0.000}  [HC={landCandidate.HeaderCoreSimilarity:0.00};  H={landCandidate.HeaderNonCoreSimilarity:0.00};  I={landCandidate.InnerSimilarity:0.00};  A={landCandidate.AncestorSimilarity:0.00}] {(landCandidate.IsAuto ? "*" : (landCandidate.Deleted ? "#" : ""))}");
						}

						reportLines.Add("*");

						if(modifiedRemapResult[cp].Count > 0)
						{
							reportLines.Add($"HC={modifiedRemapResult[cp][0].Weights[ContextType.HeaderCore]};  " +
								$"HNC={modifiedRemapResult[cp][0].Weights[ContextType.HeaderNonCore]:0.00};  " +
								$"I={modifiedRemapResult[cp][0].Weights[ContextType.Inner]:0.00};  " +
								$"A={modifiedRemapResult[cp][0].Weights[ContextType.Ancestors]:0.00};  " +
								$"SG={modifiedRemapResult[cp][0].Weights[ContextType.SiblingsGlobal]:0.00}; " +
								$"SR={modifiedRemapResult[cp][0].Weights[ContextType.SiblingsRange]:0.00}");
						}

						foreach (var landCandidate in modifiedRemapResult[cp].Take(7))
						{
							reportLines.Add($"{String.Join(" ", landCandidate.Context.HeaderContext.Sequence_old)}     {landCandidate.Context.Line}");
							reportLines.Add($"\t{landCandidate.Similarity:0.000}  [HC={landCandidate.HeaderCoreSimilarity:0.00};  HNC={landCandidate.HeaderNonCoreSimilarity:0.00};  " +
								$"I={landCandidate.InnerSimilarity:0.00};  A={landCandidate.AncestorSimilarity:0.00};  " +
								$"SG={landCandidate.SiblingsGlobalSimilarity:0.00}; SR={landCandidate.SiblingsRangeSimilarity:0.00}] " +
								$"{(landCandidate.IsAuto ? "*" : (landCandidate.Deleted ? "#" : ""))}");
						}
						reportLines.Add("");
						reportLines.Add("**************************************************************");
						reportLines.Add("");

						foreach (var line in reportLines)
						{
							report.WriteLine(line);
						}

						List<List<string>> customReport = null;

						if (isModifiedAuto)
						{
							if (isBasicAuto)
							{
								customReport = sameFirst
									? sameAutoResult : differentAutoResult;
							}
							else
							{
								customReport = modifiedOnlyAutoResult;
							}
						}
						else if (isBasicAuto)
						{
							customReport = basicOnlyAutoResult;
						}
						else
						{
							customReport = sameFirst
								? sameFirstPos : differentFirstPos;
						}

						customReport.Add(reportLines);
					}
				}

				File.WriteAllLines($"{key}_similarities.txt", similarities);
				File.WriteAllLines($"{key}_basicOnlyAutoResult.txt",
					basicOnlyAutoResult.SelectMany(r => r));
				File.WriteAllLines($"{key}_modifiedOnlyAutoResult.txt",
					modifiedOnlyAutoResult.SelectMany(r => r));
				File.WriteAllLines($"{key}_sameAutoResult.txt",
					sameAutoResult.SelectMany(r => r));
				File.WriteAllLines($"{key}_differentAutoResult.txt",
					differentAutoResult.SelectMany(r => r));
				File.WriteAllLines($"{key}_sameFirstPos.txt",
					sameFirstPos.SelectMany(r => r));
				File.WriteAllLines($"{key}_differentFirstPos.txt",
					differentFirstPos.SelectMany(r => r));

				if (sameAutoResult.Count > 50) {

					var randomAutoIdx = new HashSet<int>();
					var random = new Random(7);

					while(randomAutoIdx.Count < 50)
					{
						randomAutoIdx.Add(random.Next(sameAutoResult.Count));
					}

					File.WriteAllLines($"{key}_sameAutoResult_toCheck.txt",
						randomAutoIdx.SelectMany(idx => sameAutoResult[idx]));
				}

				Console.WriteLine($"Total: {pointsOfType.Count}");
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
}
