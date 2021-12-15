using Land.Core;
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
		//const string BaseFolder = @"D:\Repositories\_mapping\Grammars\PascalABC.NET\44\base";
		//const string ModifiedFolder = @"D:\Repositories\_mapping\Grammars\PascalABC.NET\44\modified";
		//const string BaseFolder = @"D:\Repositories\_mapping\ASP.NET Core\3551\Common\base";
		//const string ModifiedFolder = @"D:\Repositories\_mapping\ASP.NET Core\3551\Common\modified";
		//const string BaseFolder = @"D:\Repositories\_mapping\PascalABC\1462\Common\base";
		//const string ModifiedFolder = @"D:\Repositories\_mapping\PascalABC\1462\Common\modified";
		const string BaseFolder = @"D:\Repositories\_mapping\Roslyn\3459\Common\base";
		const string ModifiedFolder = @"D:\Repositories\_mapping\Roslyn\3459\Common\modified";

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
				var landParsed = ParseFile(landParser, file, landErrors);

				landSearchArea.Add(landParsed);

				++counter;
				if (counter % 100 == 0)
					Console.WriteLine($"{counter} out of {files.Count}...");
			}

			Console.WriteLine($"LanD parsing done in {DateTime.Now - start}");

			return landSearchArea;
		}

		static ParsedFile ParseFile(
			Land.Core.Parsing.BaseParser landParser,
			string file,
			List<string> landErrors)
		{
			/// Читаем текст из файла
			var text = File.ReadAllText(file);

			/// Парсим при помощи LanD
			var landRoot = landParser.Parse(text);
			if (landParser.Log.Any(l => l.Type == Land.Core.MessageType.Error))
				landErrors.Add(file);

			return new ParsedFile
			{
				Name = Path.GetFileName(file),
				Root = landRoot,
				Text = text
			};
		}

		static void Main(string[] args)
		{
			FullBinding();
		}

		static void FullBinding()
		{
			var heuristic = new ContextsEqualityHeuristic();
			var markupManager = new MarkupManager(null, heuristic);
			var entityTypes = new string[] { "class_struct_interface", "method", "field", "property" };
			//var entityTypes = new string[] { "symbol_declaration", "symbol_declaration_element", "rule", "alternative", "alternative_element" };

			/// Создаём парсер и менеджер разметки из библиотеки LanD	
			var landParser = sharp.ParserProvider.GetParser(false);
			landParser.SetVisitor(g => new MarkupOptionsProcessingVisitor(g));
			landParser.SetPreprocessor(new SharpPreprocessing.ConditionalCompilation.SharpPreprocessor());

			var landErrors = new List<string>();

			/////////////////////////////////////////////// STAGE 1

			var initialFiles = new HashSet<string>(Directory.GetFiles(BaseFolder, "*.cs"));

			var report = new StreamWriter("report.txt");
			var casesForRandomBinding = new List<string>();

			Dictionary<string, List<List<string>>> sameAutoResult = entityTypes.ToDictionary(e => e, e => new List<List<string>>()),
				differentAutoResult = entityTypes.ToDictionary(e => e, e => new List<List<string>>()),
				modifiedOnlyAutoResult = entityTypes.ToDictionary(e => e, e => new List<List<string>>()),
				basicOnlyAutoResult = entityTypes.ToDictionary(e => e, e => new List<List<string>>()),
				sameFirstPos = entityTypes.ToDictionary(e => e, e => new List<List<string>>()),
				differentFirstPos = entityTypes.ToDictionary(e => e, e => new List<List<string>>());

			Dictionary<string, List<List<string>>> modifiedOnlyDifferentFirst = entityTypes.ToDictionary(e => e, e => new List<List<string>>()),
				modifiedOnlyOverloaded = entityTypes.ToDictionary(e => e, e => new List<List<string>>()),
				identicalCandidates = entityTypes.ToDictionary(e => e, e => new List<List<string>>());

			Dictionary<string, int> simpleRebindSameAuto = entityTypes.ToDictionary(e => e, e => 0),
				simpleRebindDifferentAuto = entityTypes.ToDictionary(e => e, e => 0),
				simpleRebindModifiedAuto = entityTypes.ToDictionary(e => e, e => 0);

			Dictionary<string, int> pointsOfType = entityTypes.ToDictionary(e => e, e => 0);

			var counter = 0;
			var modifiedTime = new TimeSpan();
			var baseTime = new TimeSpan();

			foreach (var file in initialFiles.ToList())
			{
				++counter;
				if(counter % 100 == 0)
				{
					Console.WriteLine($"{counter} out of {initialFiles.Count}...");
				}

				var initialFile = ParseFile(landParser, file, landErrors);
				var currentFile = ParseFile(landParser, Path.Combine(ModifiedFolder, Path.GetFileName(file)), landErrors);

				if(initialFile.Root == null || currentFile.Root == null)
				{
					Console.WriteLine($"{Path.GetFileName(file)} skipped...");
					continue;
				}

				markupManager.AddLand(initialFile);

				markupManager.ContextFinder.UseOldApproach = false;
				var startTime = DateTime.Now;
				var modifiedRemapResult = markupManager.Remap(new List<ParsedFile> { currentFile }, false, ContextFinder.SearchType.Local);
				modifiedTime += DateTime.Now - startTime;

				markupManager.ContextFinder.UseOldApproach = true;
				startTime = DateTime.Now;
				var basicRemapResult = markupManager.Remap(new List<ParsedFile> { currentFile }, false, ContextFinder.SearchType.Local);
				baseTime += DateTime.Now - startTime;

				foreach (var key in entityTypes)
				{
					var currentPointsOfType = modifiedRemapResult.Keys.Where(e => e.Context.Type == key).ToList();

					pointsOfType[key] += currentPointsOfType.Count;

					foreach (var cp in currentPointsOfType)
					{
						var modifiedResult = modifiedRemapResult[cp].Where(e => !e.Deleted).ToList();
						var basicResult = basicRemapResult[cp].Where(e => !e.Deleted).ToList();

						var isModifiedAuto = modifiedResult.FirstOrDefault()?.IsAuto ?? false;
						var isBasicAuto = basicResult.FirstOrDefault()?.IsAuto ?? false;

						var sameFirst = basicResult.Count == 0 && modifiedResult.Count == 0
							|| basicResult.Count > 0 && modifiedResult.Count > 0
								&& modifiedResult.First().Context.HeaderContext.Sequence_old
									.SequenceEqual(basicResult.First().Context.HeaderContext.Sequence_old)
								&& modifiedResult.First().Context.StartOffset == basicResult.First().Context.StartOffset;

						var hasNotChanged = isModifiedAuto && initialFile.Text.Substring(
							cp.NodeLocation.Start.Offset,
							cp.NodeLocation.Length.Value
						) == currentFile.Text.Substring(
							modifiedRemapResult[cp][0].Node.Location.Start.Offset,
							modifiedRemapResult[cp][0].Node.Location.Length.Value
						) && cp.Context.AncestorsContext.SequenceEqual(modifiedRemapResult[cp][0].Context.AncestorsContext);

						if (!hasNotChanged)
						{
							//casesForRandomBinding.Add($"{cp.Context.FileName};{cp.Context.Type};{cp.Context.StartOffset}");

							var reportLines = new List<string>();

							reportLines.Add($"file:///{BaseFolder}\\{cp.Context.FileName}");
							reportLines.Add($"file:///{ModifiedFolder}\\{cp.Context.FileName}");
							reportLines.Add("*");

							reportLines.Add($"{String.Join(" ", cp.Context.HeaderContext.Sequence_old)}     {cp.Context.Line}");
							reportLines.Add("*");

							foreach (var landCandidate in basicRemapResult[cp].Take(7).OrderBy(r => r.Deleted))
							{
								reportLines.Add($"{String.Join(" ", landCandidate.Context.HeaderContext.Sequence_old)}     {landCandidate.Context.Line}");
								reportLines.Add($"\t{landCandidate.Similarity:0.000}  [HC={landCandidate.HeaderCoreSimilarity:0.00};  H={landCandidate.HeaderNonCoreSimilarity:0.00};  I={landCandidate.InnerSimilarity:0.00};  S={landCandidate.AncestorSimilarity:0.00}] {(landCandidate.IsAuto ? "*" : (landCandidate.Deleted ? "#" : ""))}");
							}

							reportLines.Add("*");

							if (modifiedRemapResult[cp].Count > 0)
							{
								if (modifiedRemapResult[cp][0].Weights != null)
								{
									reportLines.Add($"HC={modifiedRemapResult[cp][0].Weights[ContextType.HeaderCore]};  " +
										$"HNC={modifiedRemapResult[cp][0].Weights[ContextType.HeaderNonCore]:0.00};  " +
										$"I={modifiedRemapResult[cp][0].Weights[ContextType.Inner]:0.00};  " +
										$"S={modifiedRemapResult[cp][0].Weights[ContextType.Ancestors]:0.00};  " +
										$"NN={modifiedRemapResult[cp][0].Weights[ContextType.SiblingsNearest]:0.00};  " +
										$"NA={modifiedRemapResult[cp][0].Weights[ContextType.SiblingsAll]:0.00}]");
								}
							}

							foreach (var landCandidate in modifiedRemapResult[cp].Take(7).OrderBy(r => r.Deleted))
							{
								reportLines.Add($"{String.Join(" ", landCandidate.Context.HeaderContext.Sequence_old)}     {landCandidate.Context.Line}");
								reportLines.Add($"\t{landCandidate.Similarity:0.000}  [HC={landCandidate.HeaderCoreSimilarity:0.00};  HNC={landCandidate.HeaderNonCoreSimilarity:0.00};  " +
									$"I={landCandidate.InnerSimilarity:0.00};  S={landCandidate.AncestorSimilarity:0.00};  " +
									$"NN={landCandidate.SiblingsNearestSimilarity:0.00};  NA={landCandidate.SiblingsAllSimilarity:0.00}] " +
									$"{(landCandidate.IsAuto ? "*" : (landCandidate.Deleted ? "#" : ""))}");
							}
							reportLines.Add("");
							reportLines.Add("**************************************************************");
							reportLines.Add("");

							foreach (var line in reportLines)
							{
								report.WriteLine(line);
							}

							if (modifiedRemapResult[cp].Count > 0 && modifiedRemapResult[cp].Skip(1).Any(e => e.Context.AncestorsContext.SequenceEqual(modifiedRemapResult[cp][0].Context.AncestorsContext)
									&& e.Context.HeaderContext.Core.SequenceEqual(modifiedRemapResult[cp][0].Context.HeaderContext.Core)
									&& e.Context.HeaderContext.NonCore.SequenceEqual(modifiedRemapResult[cp][0].Context.HeaderContext.NonCore)))
							{
								identicalCandidates[key].Add(reportLines);
							}

							if (isModifiedAuto)
							{
								if (isBasicAuto)
								{
									if (sameFirst)
									{
										if (modifiedRemapResult[cp][0].Weights == null)
										{
											simpleRebindSameAuto[key] += 1;
										}
										else
										{
											sameAutoResult[key].Add(reportLines);
											casesForRandomBinding.Add($"{cp.Context.FileName};{cp.Context.Type};{cp.Context.StartOffset}");
										}
									}
									else
									{
										if (modifiedRemapResult[cp][0].Weights == null)
										{
											simpleRebindDifferentAuto[key] += 1;
										}
										else
										{
											differentAutoResult[key].Add(reportLines);
											casesForRandomBinding.Add($"{cp.Context.FileName};{cp.Context.Type};{cp.Context.StartOffset}");
										}
									}
								}
								else
								{
									if (!sameFirst)
									{
										modifiedOnlyDifferentFirst[key].Add(reportLines);
									}

									if (modifiedRemapResult[cp].Count > 1
										&& modifiedRemapResult[cp].Skip(1).Any(e => e.Context.AncestorsContext.SequenceEqual(modifiedRemapResult[cp][0].Context.AncestorsContext)
											&& e.Context.HeaderContext.Core.SequenceEqual(modifiedRemapResult[cp][0].Context.HeaderContext.Core)))
									{
										modifiedOnlyOverloaded[key].Add(reportLines);
									}

									if (modifiedRemapResult[cp][0].Weights == null)
									{
										simpleRebindModifiedAuto[key] += 1;
									}
									else
									{
										modifiedOnlyAutoResult[key].Add(reportLines);
										casesForRandomBinding.Add($"{cp.Context.FileName};{cp.Context.Type};{cp.Context.StartOffset}");
									}
								}
							}
							else if (isBasicAuto)
							{
								basicOnlyAutoResult[key].Add(reportLines);
								//casesForRandomBinding.Add($"{cp.Context.FileName};{cp.Context.Type};{cp.Context.StartOffset}");
							}
							else
							{
								//casesForRandomBinding.Add($"{cp.Context.FileName};{cp.Context.Type};{cp.Context.StartOffset}");

								if (sameFirst)
								{
									sameFirstPos[key].Add(reportLines);
								}
								else
								{
									differentFirstPos[key].Add(reportLines);
								}
							}
						}
					}
				}

				markupManager.Clear();
				markupManager.ContextFinder.ContextManager.ClearCache(initialFile.Name);
				markupManager.ContextFinder.ContextManager.ClearCache(currentFile.Name);
			}

			Console.WriteLine();

			foreach (var key in entityTypes)
			{
				File.WriteAllLines($"{key}_basicOnlyAutoResult.txt",
					basicOnlyAutoResult[key].SelectMany(r => r));
				File.WriteAllLines($"{key}_modifiedOnlyAutoResult.txt",
					modifiedOnlyAutoResult[key].SelectMany(r => r));
				File.WriteAllLines($"{key}_sameAutoResult.txt",
					sameAutoResult[key].SelectMany(r => r));
				File.WriteAllLines($"{key}_differentAutoResult.txt",
					differentAutoResult[key].SelectMany(r => r));
				File.WriteAllLines($"{key}_sameFirstPos.txt",
					sameFirstPos[key].SelectMany(r => r));
				File.WriteAllLines($"{key}_differentFirstPos.txt",
					differentFirstPos[key].SelectMany(r => r));

				File.WriteAllLines($"{key}_modifiedOnlyOverloaded.txt",
					modifiedOnlyOverloaded[key].SelectMany(r => r));
				File.WriteAllLines($"{key}_modifiedOnlyDifferentFirst.txt",
					modifiedOnlyDifferentFirst[key].SelectMany(r => r));
				File.WriteAllLines($"{key}_identicalCandidates.txt",
					identicalCandidates[key].SelectMany(r => r));

				if (sameAutoResult[key].Count > 100)
				{

					var randomAutoIdx = new HashSet<int>();
					var random = new Random(7);

					while (randomAutoIdx.Count < 100)
					{
						randomAutoIdx.Add(random.Next(sameAutoResult[key].Count));
					}

					File.WriteAllLines($"{key}_sameAutoResult_toCheck.txt",
						randomAutoIdx.SelectMany(idx => sameAutoResult[key][idx]));
				}

				if (sameFirstPos[key].Count > 100)
				{

					var randomAutoIdx = new HashSet<int>();
					var random = new Random(7);

					while (randomAutoIdx.Count < 100)
					{
						randomAutoIdx.Add(random.Next(sameFirstPos[key].Count));
					}

					File.WriteAllLines($"{key}_sameFirstPos_toCheck.txt",
						randomAutoIdx.SelectMany(idx => sameFirstPos[key][idx]));
				}

				Console.WriteLine($"Total count: {pointsOfType[key]}");
				Console.WriteLine($"Total marked: {simpleRebindModifiedAuto[key] + simpleRebindSameAuto[key] + simpleRebindDifferentAuto[key] + modifiedOnlyAutoResult[key].Count + basicOnlyAutoResult[key].Count + sameAutoResult[key].Count + differentAutoResult[key].Count + sameFirstPos[key].Count + differentFirstPos[key].Count}");
				Console.WriteLine($"Simple rebinding: {simpleRebindModifiedAuto[key] + simpleRebindSameAuto[key] + simpleRebindDifferentAuto[key]}");
				Console.WriteLine($"Modified only auto: {modifiedOnlyAutoResult[key].Count}");
				Console.WriteLine($"Basic only auto: {basicOnlyAutoResult[key].Count}");
				Console.WriteLine($"Same auto: {sameAutoResult[key].Count}");
				Console.WriteLine($"Different auto: {differentAutoResult[key].Count}");
				Console.WriteLine($"Same first: {sameFirstPos[key].Count}");
				Console.WriteLine($"Different first: {differentFirstPos[key].Count}");

				Console.WriteLine($"Overloaded methods when modified auto: {modifiedOnlyOverloaded[key].Count}");
				Console.WriteLine($"Different first when modified auto: {modifiedOnlyDifferentFirst[key].Count}");
				Console.WriteLine($"Identical candidates: {identicalCandidates[key].Count}");

				Console.WriteLine($"'{key}' done!");
				Console.WriteLine();
			}

			Console.WriteLine($"Base time: {baseTime}");
			Console.WriteLine($"Modified time: {modifiedTime}");

			report.Close();

			File.WriteAllLines($"randomBinding.txt", casesForRandomBinding);

			Console.WriteLine("Job's done!");
			Console.ReadLine();
		}

		static void RandomBinding()
		{
			var heuristic = new ContextsEqualityHeuristic();
			var markupManager = new MarkupManager(null, heuristic);
			var entityTypes = new string[] { "class_struct_interface", "method", "field", "property" };
			//var entityTypes = new string[] { "symbol_declaration", "symbol_declaration_element", "rule", "alternative", "alternative_element" };

			/// Создаём парсер C# и менеджер разметки из библиотеки LanD	
			var landParser = sharp.ParserProvider.GetParser(false);
			landParser.SetVisitor(g => new MarkupOptionsProcessingVisitor(g));
			landParser.SetPreprocessor(new SharpPreprocessing.ConditionalCompilation.SharpPreprocessor());

			var landErrors = new List<string>();

			/////////////////////////////////////////////// STAGE 1

			var files = new HashSet<string>(Directory.GetFiles(BaseFolder, "*.cs"));

			var start = DateTime.Now;

			/// Парсим отобранные файлы
			var parsedFiles = GetSearchArea(landParser, files.ToList(), landErrors)
				.ToDictionary(e => e.Name, e => e);
			var expectedPoints = File.ReadAllLines("randomBinding.txt")
				.Select(l => l.Split(';'))
				.Select(e => new
				{
					FileName = e[0],
					Type = e[1],
					Offset = int.Parse(e[2])
				})
				.ToList();

			var generator = new Random(7);

			foreach(var expectedPoint in expectedPoints)
			{
				if (generator.Next(10) == 1)
				{
					var node = markupManager.GetConcernPointCandidates(
						parsedFiles[expectedPoint.FileName].Root,
						new Land.Core.SegmentLocation
						{
							Start = new Land.Core.PointLocation(expectedPoint.Offset),
							End = new Land.Core.PointLocation(expectedPoint.Offset)
						}
					).FirstOrDefault(c => c.Type == expectedPoint.Type);

					markupManager.AddConcernPoint(
						node,
						null,
						parsedFiles[expectedPoint.FileName]
					);
				}
			}

			Console.WriteLine($"Binding done in {DateTime.Now - start}");

			/////////////////////////////////////////////// STAGE 2

			Console.WriteLine("Stage 2 started...");

			files = new HashSet<string>(files.Select(f => Path.Combine(ModifiedFolder, Path.GetFileName(f))));

			var searchArea = GetSearchArea(landParser, files.ToList(), landErrors);

			Console.WriteLine("Rebinding...");

			var report = new StreamWriter("report.txt");

			start = DateTime.Now;
			markupManager.ContextFinder.UseOldApproach = false;
			var modifiedRemapResult = markupManager.Remap(searchArea, false, ContextFinder.SearchType.Local);
			Console.WriteLine($"Modified rebinding done in {DateTime.Now - start}");

			start = DateTime.Now;
			markupManager.ContextFinder.UseOldApproach = true;
			var basicRemapResult = markupManager.Remap(searchArea, false, ContextFinder.SearchType.Local);
			Console.WriteLine($"Base rebinding done in {DateTime.Now - start}");

			Console.WriteLine();

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

				int simpleRebindSameAuto = 0,
					simpleRebindDifferentAuto = 0,
					simpleRebindModifiedAuto = 0;

				List<List<string>> modifiedOnlyDifferentFirst = new List<List<string>>(),
					modifiedOnlyOverloaded = new List<List<string>>(),
					identicalCandidates = new List<List<string>>();

				var similarities = new List<string>();

				foreach (var cp in pointsOfType)
				{
					var modifiedResult = modifiedRemapResult[cp].Where(e => !e.Deleted).ToList();
					var basicResult = basicRemapResult[cp].Where(e => !e.Deleted).ToList();

					var isModifiedAuto = modifiedResult.FirstOrDefault()?.IsAuto ?? false;
					var isBasicAuto = basicResult.FirstOrDefault()?.IsAuto ?? false;

					var sameFirst = basicResult.Count == 0 && modifiedResult.Count == 0 
						|| basicResult.Count > 0 && modifiedResult.Count > 0
							&& modifiedResult.First().Context.HeaderContext.Sequence_old
								.SequenceEqual(basicResult.First().Context.HeaderContext.Sequence_old);

					var hasNotChanged = isModifiedAuto && File.ReadAllText(Path.Combine(BaseFolder, cp.Context.FileName)).Substring(
						cp.NodeLocation.Start.Offset,
						cp.NodeLocation.Length.Value
					) == searchArea.First(f=>f.Name == modifiedRemapResult[cp][0].File.Name).Text.Substring(
						modifiedRemapResult[cp][0].Node.Location.Start.Offset,
						modifiedRemapResult[cp][0].Node.Location.Length.Value
					) && cp.Context.AncestorsContext.SequenceEqual(modifiedRemapResult[cp][0].Context.AncestorsContext);

					if (!hasNotChanged)
					{
						if (basicRemapResult[cp].Count == 1 && modifiedRemapResult[cp].Count == 1)
							similarities.Add($"{ basicRemapResult[cp][0].Similarity };{ modifiedRemapResult[cp][0].Similarity }");
						else if (basicRemapResult[cp].Count > 1 && modifiedRemapResult[cp].Count > 1)
							similarities.Add($"{ basicRemapResult[cp][0].Similarity };{ modifiedRemapResult[cp][0].Similarity };{ basicRemapResult[cp][1].Similarity };{ modifiedRemapResult[cp][1].Similarity }");

						var reportLines = new List<string>();

						reportLines.Add($"file:///{BaseFolder}\\{cp.Context.FileName}");
						reportLines.Add($"file:///{ModifiedFolder}\\{cp.Context.FileName}");
						reportLines.Add("*");

						reportLines.Add($"{String.Join(" ", cp.Context.HeaderContext.Sequence_old)}     {cp.Context.Line}");
						reportLines.Add("*");

						foreach (var landCandidate in basicRemapResult[cp].Take(7).OrderBy(r=>r.Deleted))
						{
							reportLines.Add($"{String.Join(" ", landCandidate.Context.HeaderContext.Sequence_old)}     {landCandidate.Context.Line}");
							reportLines.Add($"\t{landCandidate.Similarity:0.000}  [HC={landCandidate.HeaderCoreSimilarity:0.00};  H={landCandidate.HeaderNonCoreSimilarity:0.00};  I={landCandidate.InnerSimilarity:0.00};  S={landCandidate.AncestorSimilarity:0.00}] {(landCandidate.IsAuto ? "*" : (landCandidate.Deleted ? "#" : ""))}");
						}

						reportLines.Add("*");

						if(modifiedRemapResult[cp].Count > 0)
						{
							if (modifiedRemapResult[cp][0].Weights != null)
							{
								reportLines.Add($"HC={modifiedRemapResult[cp][0].Weights[ContextType.HeaderCore]};  " +
									$"HNC={modifiedRemapResult[cp][0].Weights[ContextType.HeaderNonCore]:0.00};  " +
									$"I={modifiedRemapResult[cp][0].Weights[ContextType.Inner]:0.00};  " +
									$"S={modifiedRemapResult[cp][0].Weights[ContextType.Ancestors]:0.00};  " +
									$"NN={modifiedRemapResult[cp][0].Weights[ContextType.SiblingsNearest]:0.00};  " +
									$"NA={modifiedRemapResult[cp][0].Weights[ContextType.SiblingsAll]:0.00}]");
							}
						}

						foreach (var landCandidate in modifiedRemapResult[cp].Take(7).OrderBy(r => r.Deleted))
						{
							reportLines.Add($"{String.Join(" ", landCandidate.Context.HeaderContext.Sequence_old)}     {landCandidate.Context.Line}");
							reportLines.Add($"\t{landCandidate.Similarity:0.000}  [HC={landCandidate.HeaderCoreSimilarity:0.00};  HNC={landCandidate.HeaderNonCoreSimilarity:0.00};  " +
								$"I={landCandidate.InnerSimilarity:0.00};  S={landCandidate.AncestorSimilarity:0.00};  " +
								$"NN={landCandidate.SiblingsNearestSimilarity:0.00};  NA={landCandidate.SiblingsAllSimilarity:0.00}] " +
								$"{(landCandidate.IsAuto ? "*" : (landCandidate.Deleted ? "#" : ""))}");
						}
						reportLines.Add("");
						reportLines.Add("**************************************************************");
						reportLines.Add("");

						foreach (var line in reportLines)
						{
							report.WriteLine(line);
						}

						if (modifiedRemapResult[cp].Count > 0 && modifiedRemapResult[cp].Skip(1).Any(e => e.Context.AncestorsContext.SequenceEqual(modifiedRemapResult[cp][0].Context.AncestorsContext)
								&& e.Context.HeaderContext.Core.SequenceEqual(modifiedRemapResult[cp][0].Context.HeaderContext.Core)
								&& e.Context.HeaderContext.NonCore.SequenceEqual(modifiedRemapResult[cp][0].Context.HeaderContext.NonCore)))
						{
							identicalCandidates.Add(reportLines);
						}

						if (isModifiedAuto)
						{
							if (isBasicAuto)
							{
								if(sameFirst)
								{
									if (modifiedRemapResult[cp][0].Weights == null)
									{
										simpleRebindSameAuto += 1;
									}
									else
									{
										sameAutoResult.Add(reportLines);
									}
								}
								else
								{
									if (modifiedRemapResult[cp][0].Weights == null)
									{
										simpleRebindDifferentAuto += 1;
									}
									else
									{
										differentAutoResult.Add(reportLines);
									}
								}
							}
							else
							{
								if (!sameFirst || modifiedRemapResult[cp][0].Context.Line != basicRemapResult[cp][0].Context.Line)
								{
									modifiedOnlyDifferentFirst.Add(reportLines);
								}

								if (modifiedRemapResult[cp].Count > 1
									&& modifiedRemapResult[cp].Skip(1).Any(e => e.Context.AncestorsContext.SequenceEqual(modifiedRemapResult[cp][0].Context.AncestorsContext)
										&& e.Context.HeaderContext.Core.SequenceEqual(modifiedRemapResult[cp][0].Context.HeaderContext.Core)))
								{
									modifiedOnlyOverloaded.Add(reportLines);
								}

								if (modifiedRemapResult[cp][0].Weights == null)
								{
									simpleRebindModifiedAuto += 1;
								}
								else
								{
									modifiedOnlyAutoResult.Add(reportLines);
								}
							}
						}
						else if (isBasicAuto)
						{
							basicOnlyAutoResult.Add(reportLines);
						}
						else
						{
							if(sameFirst)
							{
								sameFirstPos.Add(reportLines);
							}
							else
							{
								differentFirstPos.Add(reportLines);
							}
						}
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

				File.WriteAllLines($"{key}_modifiedOnlyOverloaded.txt",
					modifiedOnlyOverloaded.SelectMany(r => r));
				File.WriteAllLines($"{key}_modifiedOnlyDifferentFirst.txt",
					modifiedOnlyDifferentFirst.SelectMany(r => r));
				File.WriteAllLines($"{key}_identicalCandidates.txt",
					identicalCandidates.SelectMany(r => r));

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

				if (sameFirstPos.Count > 50)
				{

					var randomAutoIdx = new HashSet<int>();
					var random = new Random(7);

					while (randomAutoIdx.Count < 50)
					{
						randomAutoIdx.Add(random.Next(sameFirstPos.Count));
					}

					File.WriteAllLines($"{key}_sameFirstPos_toCheck.txt",
						randomAutoIdx.SelectMany(idx => sameFirstPos[idx]));
				}

				Console.WriteLine($"Total count: {pointsOfType.Count}");
				Console.WriteLine($"Total marked: {simpleRebindModifiedAuto + simpleRebindSameAuto + simpleRebindDifferentAuto + modifiedOnlyAutoResult.Count + basicOnlyAutoResult.Count + sameAutoResult.Count + differentAutoResult.Count + sameFirstPos.Count + differentFirstPos.Count}");
				Console.WriteLine($"Simple rebinding: {simpleRebindModifiedAuto + simpleRebindSameAuto + simpleRebindDifferentAuto}");
				Console.WriteLine($"Modified only auto: {modifiedOnlyAutoResult.Count}");
				Console.WriteLine($"Basic only auto: {basicOnlyAutoResult.Count}");
				Console.WriteLine($"Same auto: {sameAutoResult.Count}");
				Console.WriteLine($"Different auto: {differentAutoResult.Count}");
				Console.WriteLine($"Same first: {sameFirstPos.Count}");
				Console.WriteLine($"Different first: {differentFirstPos.Count}");

				Console.WriteLine($"Overloaded methods when modified auto: {modifiedOnlyOverloaded.Count}");
				Console.WriteLine($"Different first when modified auto: {modifiedOnlyDifferentFirst.Count}");
				Console.WriteLine($"Identical candidates: {identicalCandidates.Count}");

				Console.WriteLine($"'{key}' done!");
				Console.WriteLine();
			}

			report.Close();

			Console.WriteLine("Job's done!");
			Console.ReadLine();
		}

		static void LineBinding()
		{
			const int TOTAL_NUMBER_OF_METHODS = 120;
			const int LINES_IN_METHOD = 2;

			var heuristic = new ContextsEqualityHeuristic();
			var markupManager = new MarkupManager(null, heuristic);

			/// Создаём парсер и менеджер разметки из библиотеки LanD	
			var landParser = sharp.ParserProvider.GetParser(false);
			landParser.SetVisitor(g => new MarkupOptionsProcessingVisitor(g));
			landParser.SetPreprocessor(new SharpPreprocessing.ConditionalCompilation.SharpPreprocessor());

			var landErrors = new List<string>();
			var initialFiles = new HashSet<string>(Directory.GetFiles(BaseFolder, "*.cs"));
			var report = new StreamWriter("report.txt");
			var reportToCheck = new StreamWriter("reportToCheck.txt");
			var totalCount = 0;
			var toCheckCount = 0;
			var shortLineCount = 0;

			var counter = 0;
			var allMethods = new List<Tuple<ConcernPoint, List<SegmentLocation>, RemapCandidateInfo>>();
			var gen = new Random(7);

			foreach (var file in initialFiles.ToList())
			{
				++counter;
				if (counter % 100 == 0)
				{
					Console.WriteLine($"{counter} out of {initialFiles.Count}...");
				}

				var initialFile = ParseFile(landParser, file, landErrors);
				var currentFile = ParseFile(landParser, Path.Combine(ModifiedFolder, Path.GetFileName(file)), landErrors);

				if (initialFile.Root == null || currentFile.Root == null)
				{
					Console.WriteLine($"{Path.GetFileName(file)} skipped...");
					continue;
				}

				markupManager.AddLand(initialFile);

				markupManager.ContextFinder.UseOldApproach = false;
				var modifiedRemapResult = markupManager.Remap("method", currentFile, false);

				foreach (var cp in modifiedRemapResult.Keys)
				{
					var modifiedResult = modifiedRemapResult[cp].Where(e => !e.Deleted).ToList();
					var isModifiedAuto = modifiedResult.FirstOrDefault()?.IsAuto ?? false;

					var hasNotChanged = isModifiedAuto && initialFile.Text.Substring(
						cp.NodeLocation.Start.Offset,
						cp.NodeLocation.Length.Value
					) == currentFile.Text.Substring(
						modifiedRemapResult[cp][0].Node.Location.Start.Offset,
						modifiedRemapResult[cp][0].Node.Location.Length.Value
					) && cp.Context.AncestorsContext.SequenceEqual(modifiedRemapResult[cp][0].Context.AncestorsContext);

					if (!hasNotChanged && isModifiedAuto)
					{
						/// Ищем узел, соответствующий телу метода
						var bodyNode = markupManager.GetConcernPointCandidates(initialFile.Root, cp.NodeLocation)
							.First(c => c.Type == "method").Children
							.FirstOrDefault(c => c.Type == "method_body");

						/// Если тело есть
						if (bodyNode != null)
						{
							/// Получаем его текст
							var bodyText = initialFile.Text.Substring(
								bodyNode.Location.Start.Offset,
								bodyNode.Location.Length.Value
							);
							/// Разбиваем на строки
							var lines = bodyText.Split('\n');
							/// Если строк хватает для интересной привязки
							if (lines.Length >= 4)
							{
								var selectedLines = new HashSet<int>();

								while (selectedLines.Count < LINES_IN_METHOD && selectedLines.Count != lines.Length - 2)
								{
									while (!selectedLines.Add(gen.Next(1, lines.Length - 1))) ;
								}

								allMethods.Add(new Tuple<ConcernPoint, List<SegmentLocation>, RemapCandidateInfo>(
									cp,
									selectedLines.Select(idx =>
									{
										var startOffset = bodyNode.Location.Start.Offset + idx
											+ lines.Take(idx).Sum(l => l.Length);

										return new SegmentLocation
										{
											Start = new PointLocation(bodyNode.Location.Start.Line + idx, null, startOffset),
											End = new PointLocation(startOffset + lines[idx].Length)
										};
									}).ToList(),
									modifiedResult.First()
								));
							}
						}
					}
				}

				markupManager.Clear();
				markupManager.ContextFinder.ContextManager.ClearCache(initialFile.Name);
				markupManager.ContextFinder.ContextManager.ClearCache(currentFile.Name);
			}

			var selectedMethods = new List<Tuple<ConcernPoint, List<SegmentLocation>, RemapCandidateInfo>>();

			while(allMethods.Count > 0 && selectedMethods.Count < TOTAL_NUMBER_OF_METHODS)
			{
				var idx = gen.Next(allMethods.Count);

				selectedMethods.Add(allMethods[idx]);
				allMethods.RemoveAt(idx);
			}

			foreach(var file in selectedMethods.ToLookup(m=>m.Item1.Context.FileName))
			{
				var initialFile = ParseFile(landParser, Path.Combine(BaseFolder, Path.GetFileName(file.Key)), landErrors);
				var currentFile = ParseFile(landParser, Path.Combine(ModifiedFolder, Path.GetFileName(file.Key)), landErrors);

				report.WriteLine("");
				report.WriteLine($"file:///{Path.Combine(BaseFolder, Path.GetFileName(file.Key))}");
				report.WriteLine($"file:///{Path.Combine(ModifiedFolder, Path.GetFileName(file.Key))}");
				report.WriteLine("*");

				var hasToCheck = false;

				foreach (var method in file)
				{
					foreach(var line in method.Item2)
					{
						totalCount += 1;

						var lineContext = new LineContext(method.Item1.NodeLocation, line, initialFile.Text);
						var (newLineContext, newLineLocation) = markupManager.ContextFinder
							.FindLine(lineContext, method.Item3.Node, currentFile, out double score, out double innerSim, out double outerSim, out bool confusionFlag);

						if (newLineLocation == null || score < 0.97)
						{
							toCheckCount += 1;

							if (!hasToCheck)
							{
								hasToCheck = true;
								reportToCheck.WriteLine("");
								reportToCheck.WriteLine($"file:///{Path.Combine(BaseFolder, Path.GetFileName(file.Key))}");
								reportToCheck.WriteLine($"file:///{Path.Combine(ModifiedFolder, Path.GetFileName(file.Key))}");
								reportToCheck.WriteLine("*");
							}

							reportToCheck.WriteLine($"[{line.Start.Line}]\t{initialFile.Text.Substring(line.Start.Offset, line.Length.Value).Trim()}");

							if (newLineLocation != null)
							{
								reportToCheck.WriteLine($"[{newLineLocation.Start.Line}]\t{currentFile.Text.Substring(newLineLocation.Start.Offset, newLineLocation.Length.Value).Trim()}\t{score:0.00}\t[{innerSim:0.00}, {outerSim:0.00}, {confusionFlag}]");
							}
							else
							{
								reportToCheck.WriteLine("*** не найдено ***");
							}
						}

						if(lineContext.InnerContext.TextLength <= 1) { shortLineCount++; }

						report.WriteLine($"[{line.Start.Line}]\t{initialFile.Text.Substring(line.Start.Offset, line.Length.Value).Trim()}");

						if (newLineLocation != null)
						{
							report.WriteLine($"[{newLineLocation.Start.Line}]\t{currentFile.Text.Substring(newLineLocation.Start.Offset, newLineLocation.Length.Value).Trim()}\t{score:0.00}\t[{innerSim:0.00}, {outerSim:0.00}, {confusionFlag}]");
						}
						else
						{
							report.WriteLine("*** не найдено ***");
						}
					}
				}
			}

			report.Close();
			reportToCheck.Close();

			Console.WriteLine("Job's done!");
			Console.WriteLine($"Total marked: {totalCount} lines");
			Console.WriteLine($"To check: {toCheckCount} lines");
			Console.WriteLine($"Short: {shortLineCount} lines");
			Console.ReadLine();
		}
	}
}
