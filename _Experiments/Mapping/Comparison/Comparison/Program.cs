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
			var modifiedRemapResult = markupManager.Remap(searchArea, false, ContextFinder.SearchType.Local);
			Console.WriteLine($"Modified remapping done in {DateTime.Now - start}");

			start = DateTime.Now;
			markupManager.ContextFinder.UseNaiveAlgorithm = true;
			var basicRemapResult = markupManager.Remap(searchArea, false, ContextFinder.SearchType.Local);
			Console.WriteLine($"Base remapping done in {DateTime.Now - start}");

			foreach (var key in entityTypes)
			{
				var pointsOfType = modifiedRemapResult.Keys
					.Where(e => e.Context.Type == key).ToList();

				List<Tuple<string, string>> sameAutoResult = new List<Tuple<string, string>>(),
					differentAutoResult = new List<Tuple<string, string>>(),
					modifiedOnlyAutoResult = new List<Tuple<string, string>>(),
					basicOnlyAutoResult = new List<Tuple<string, string>>(),
					sameFirstPos = new List<Tuple<string, string>>(),
					differentFirstPos = new List<Tuple<string, string>>();

				var similarities = new List<string>();

				foreach (var cp in pointsOfType)
				{
					var isModifiedAuto = modifiedRemapResult[cp].FirstOrDefault()?.IsAuto ?? false;
					var isBasicAuto = basicRemapResult[cp].FirstOrDefault()?.IsAuto ?? false;

					var sameFirst = basicRemapResult[cp].Count == 0 && modifiedRemapResult[cp].Count == 0 ||
						basicRemapResult[cp].Count > 0 && modifiedRemapResult[cp].Count > 0 &&
						modifiedRemapResult[cp][0].Context.HeaderContext.Sequence.SelectMany(h => h.Value.Select(valElem => valElem.Text))
							.SequenceEqual(basicRemapResult[cp][0].Context.HeaderContext.Sequence.SelectMany(h => h.Value.Select(valElem => valElem.Text)));

					/// Отсекаем элементы, привязку к которым можно обеспечить за счёт базовой эвристики
					var hasNotChanged = modifiedRemapResult[cp].Count == 1 
						&& modifiedRemapResult[cp][0].Weights == null;

					if (!hasNotChanged)
					{
						if (basicRemapResult[cp].Count == 1 && modifiedRemapResult[cp].Count == 1)
							similarities.Add($"{ basicRemapResult[cp][0].Similarity };{ modifiedRemapResult[cp][0].Similarity }");
						else if (basicRemapResult[cp].Count > 1 && modifiedRemapResult[cp].Count > 1)
							similarities.Add($"{ basicRemapResult[cp][0].Similarity };{ modifiedRemapResult[cp][0].Similarity };{ basicRemapResult[cp][1].Similarity };{ modifiedRemapResult[cp][1].Similarity }");

						report.WriteLine(Path.GetFileName(cp.Context.FileContext.Name));
						report.WriteLine("*");

						report.WriteLine(String.Join(" ", cp.Context.HeaderContext.Sequence.SelectMany(c => c.Value.Select(valElem => valElem.Text))));
						report.WriteLine("*");

						foreach (var landCandidate in basicRemapResult[cp].Take(5))
						{
							report.WriteLine(String.Join(" ", landCandidate.Context.HeaderContext.Sequence.SelectMany(c => c.Value.Select(valElem => valElem.Text))));
							report.WriteLine($"{landCandidate.Similarity}  [SimHCore={landCandidate.HeaderCoreSimilarity}; SimH={landCandidate.HeaderNonCoreSimilarity}; SimI={landCandidate.InnerSimilarity}; SimA={landCandidate.AncestorSimilarity}] {(landCandidate.IsAuto ? "*" : "")}");
						}

						report.WriteLine("*");

						if(modifiedRemapResult[cp].Count > 0)
						{
							report.WriteLine($"WHCore={modifiedRemapResult[cp][0].Weights[ContextType.HeaderCore]}; " +
								$"WHNCore={modifiedRemapResult[cp][0].Weights[ContextType.HeaderNonCore]}; " +
								$"WI={modifiedRemapResult[cp][0].Weights[ContextType.Inner]}; " +
								$"WA={modifiedRemapResult[cp][0].Weights[ContextType.Ancestors]}; " +
								$"WS={modifiedRemapResult[cp][0].Weights[ContextType.Siblings]}");
						}

						foreach (var landCandidate in modifiedRemapResult[cp].Take(5))
						{
							report.WriteLine(String.Join(" ", landCandidate.Context.HeaderContext.Sequence.SelectMany(c => c.Value.Select(valElem => valElem.Text))));
							report.WriteLine($"{landCandidate.Similarity}  [SimHCore={landCandidate.HeaderCoreSimilarity}; SimHNCore={landCandidate.HeaderNonCoreSimilarity}; " +
								$"SimI={landCandidate.InnerSimilarity}; SimA={landCandidate.AncestorSimilarity}; " +
								$"SimSB={landCandidate.SiblingsBeforeSimilarity}; SimSA={landCandidate.SiblingsAfterSimilarity}; SimS={landCandidate.SiblingsSimilarity}] " +
								$"{(landCandidate.IsAuto ? "*" : "")}");
						}
						report.WriteLine();
						report.WriteLine("**************************************************************");
						report.WriteLine();

						var tuple = new Tuple<string, string>(
								cp.Context.FileContext.Name,
								String.Join(" ", cp.Context.HeaderContext.Sequence.SelectMany(h => h.Value.Select(valElem => valElem.Text)))
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
