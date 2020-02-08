using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using AspectCore;
using Land.Markup;
using Land.Markup.CoreExtension;
using Land.Markup.Binding;
using Land.Core.Parsing.Tree;

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
		static Tuple<List<ParsedFile>, List<PointOfInterest>> GetSearchArea(
			Land.Core.Parsing.BaseParser landParser,
			ParserWrapper coreParser,
			List<string> files,
			List<string> landErrors,
			List<string> coreErrors)
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

			var coreSearchArea = new List<PointOfInterest>();
			start = DateTime.Now;

			/// Преобразуем построенные LanD деревья в формат AspectCore
			foreach (var file in landSearchArea)
			{
				var visitor = new LandToCoreConverter(landParser.GrammarObject, file.Name);
				file.Root.Accept(visitor);

				coreSearchArea.Add(visitor.Root);
			}

			//foreach(var file in files)
			//{
			//	var coreRoot = coreParser.ParseText(File.ReadAllText(file), file);
			//	coreSearchArea.Add(coreRoot);
			//}

			Console.WriteLine($"Got Core forest in {DateTime.Now - start}");

			return new Tuple<List<ParsedFile>, List<PointOfInterest>>(landSearchArea, coreSearchArea);
		}

		static void Main(string[] args)
		{
			var entities = new Dictionary<string, Tuple<MarkupManager, List<PointOfInterest>>>
			{
				{ "class_struct_interface", new Tuple<MarkupManager, List<PointOfInterest>>(new MarkupManager(null), new List<PointOfInterest>()) },
				{ "method", new Tuple<MarkupManager, List<PointOfInterest>>(new MarkupManager(null), new List<PointOfInterest>()) },
				{ "field", new Tuple<MarkupManager, List<PointOfInterest>>(new MarkupManager(null), new List<PointOfInterest>()) },
				{ "property", new Tuple<MarkupManager, List<PointOfInterest>>(new MarkupManager(null), new List<PointOfInterest>()) }
			};

			/// Создаём парсер C# и менеджер разметки из библиотеки LanD	
			var landParser = sharp.ParserProvider.GetParser(false);
			var markupSettings = new LanguageMarkupSettings(landParser.GrammarObject.Options.GetOptions());
			landParser.SetVisitor(g => new MarkupOptionsProcessingVisitor(g));
			landParser.SetPreprocessor(new SharpPreprocessing.ConditionalCompilation.SharpPreprocessor());

			var coreParser = new ParserWrapper("../../components/AspectCore");

			var landErrors = new List<string>();
			var coreErrors = new List<string>();

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
			var searchArea = GetSearchArea(landParser, coreParser, files.ToList(), landErrors, coreErrors);

			/// Привязываемся к сущностям, случайным образом выбирая нужное их количество в каждом файле
			for(var j=0; j< searchArea.Item1.Count; ++j)
			{		
				var visitor = new GetNodeSequenceVisitor();
				searchArea.Item1[j].Root.Accept(visitor);

				foreach(var key in entities.Keys)
				{
					var subseq = visitor.Sequence.Where(n => n.Type == key).ToList();

					for (var i = 0; i < EntitiesPerFile; ++i)
					{
						if (subseq.Count == 0) break;
						var index = RandomGen.Next(0, subseq.Count);

						entities[key].Item1.AddConcernPoint(
							subseq[index], 
							searchArea.Item1[j], 
							searchArea.Item1, 
							null);

						entities[key].Item2.Add(TreeSearchEngine.FindPointByLocation(
							searchArea.Item2[j],
							subseq[index].Children.FirstOrDefault(c => c.Type == "name").Location.Start.Line.Value,
							subseq[index].Children.FirstOrDefault(c => c.Type == "name").Location.Start.Column.Value
						).FirstOrDefault());

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

			searchArea = GetSearchArea(landParser, coreParser, files.ToList(), landErrors, coreErrors);

			Console.WriteLine("Remapping...");

			var report = new StreamWriter("report.txt");

			foreach (var key in entities.Keys)
			{
				List<Tuple<string, string>> sameAutoResult = new List<Tuple<string, string>>(),
					differentAutoResult = new List<Tuple<string, string>>(),
					landOnlyAutoResult = new List<Tuple<string, string>>(),
					coreOnlyAutoResult = new List<Tuple<string, string>>(),
					sameFirstPos = new List<Tuple<string, string>>(),
					differentFirstPos = new List<Tuple<string, string>>();

				var similarities = new List<string>();
				var start = DateTime.Now;

				var landRemapResult = entities[key].Item1.Remap(searchArea.Item1, false, ContextFinder.SearchType.Local);

				Console.WriteLine($"LanD remapping done in {DateTime.Now - start}");

				var coreRemapResult = new Dictionary<ConcernPoint, TreeSearchResult>();
				var landPoints = entities[key].Item1.GetConcernPoints();

				start = DateTime.Now;

				for (var i = 0; i < entities[key].Item2.Count; ++i)
				{
					var poi = entities[key].Item2[i];
					var parsed = searchArea.Item2
						.Select((elem, idx) => new { elem, idx } )
						.Where(e => Path.GetFileNameWithoutExtension(e.elem.FileName) == Path.GetFileNameWithoutExtension(poi.FileName))
						.FirstOrDefault();

					coreRemapResult[landPoints[i]] =
						TreeSearchEngine.FindPointInTree2(parsed.elem, poi, searchArea.Item1[parsed.idx].Text);
				}

				Console.WriteLine($"Core remapping done in {DateTime.Now - start}");

				foreach (var cp in landRemapResult.Keys)
				{
					var isLandAuto = landRemapResult[cp].FirstOrDefault()?.IsAuto ?? false;

					var isCoreAuto = coreRemapResult[cp].Singular ||
						(coreRemapResult[cp].Count >= 2 && 
						coreRemapResult[cp].GetNodeSimilarity(1) != 1 && 
						(1 - coreRemapResult[cp].GetNodeSimilarity(1)) >= (1 - coreRemapResult[cp].GetNodeSimilarity(0)) * 2);

					var sameFirst = coreRemapResult[cp].Count == 0 && landRemapResult[cp].Count == 0 ||
						coreRemapResult[cp].Count > 0 && landRemapResult[cp].Count > 0 &&
						String.Join("", coreRemapResult[cp][0].Context[0].Name)
							.StartsWith(String.Join("", landRemapResult[cp][0].Context.HeaderContext.SelectMany(h => h.Value)));

					var hasChanged = landRemapResult[cp].Count == 0 ||
						landRemapResult[cp][0].HeaderSimilarity != 1 ||
						landRemapResult[cp][0].InnerSimilarity != 1 ||
						landRemapResult[cp][0].AncestorSimilarity != 1;

					if (hasChanged || !isLandAuto || !isCoreAuto)
					{
						if (coreRemapResult[cp].Count == 1 && landRemapResult[cp].Count == 1)
							similarities.Add($"{ coreRemapResult[cp].GetNodeSimilarity(0) };{ landRemapResult[cp][0].Similarity }");
						else if (coreRemapResult[cp].Count > 1 && landRemapResult[cp].Count > 1)
							similarities.Add($"{ coreRemapResult[cp].GetNodeSimilarity(0) };{ landRemapResult[cp][0].Similarity };{ coreRemapResult[cp].GetNodeSimilarity(1) };{ landRemapResult[cp][1].Similarity }");

						report.WriteLine(Path.GetFileName(cp.Context.FileContext.Name));
						report.WriteLine("*");

						report.WriteLine(String.Join(" ", cp.Context.HeaderContext.SelectMany(c => c.Value)));
						report.WriteLine("*");

						for (var j = 0; j < coreRemapResult[cp].Count && j < 5; ++j)
						{
							var coreCandidate = coreRemapResult[cp][j];
							report.WriteLine(coreCandidate.Context[0].Name != null ? String.Join(" ", coreCandidate.Context[0].Name) : "");
							report.WriteLine($"{coreRemapResult[cp].GetNodeSimilarity(j)} {(j == 0 && isCoreAuto ? "*" : "")}");
						}

						report.WriteLine("*");

						foreach (var landCandidate in landRemapResult[cp].Take(5))
						{
							report.WriteLine(String.Join(" ", landCandidate.Context.HeaderContext.SelectMany(c => c.Value)));
							report.WriteLine($"{landCandidate.Similarity}  [{landCandidate.HeaderSimilarity}; {landCandidate.InnerSimilarity}; {landCandidate.AncestorSimilarity}] {(landCandidate.IsAuto ? "*" : "")}");
						}
						report.WriteLine();
						report.WriteLine("**************************************************************");
						report.WriteLine();

						var tuple = new Tuple<string, string>(
								cp.Context.FileContext.Name,
								String.Join(" ", cp.Context.HeaderContext.SelectMany(h => h.Value))
							);

						if (isLandAuto)
						{
							if (isCoreAuto)
							{
								if (sameFirst)
									sameAutoResult.Add(tuple);
								else
									differentAutoResult.Add(tuple);
							}
							else
							{
								landOnlyAutoResult.Add(tuple);
							}
						}
						else if (isCoreAuto)
						{
							coreOnlyAutoResult.Add(tuple);
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
				File.WriteAllLines($"{key}_coreOnlyAutoResult.txt",
					coreOnlyAutoResult.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
				File.WriteAllLines($"{key}_landOnlyAutoResult.txt",
					landOnlyAutoResult.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
				File.WriteAllLines($"{key}_sameAutoResult.txt",
					sameAutoResult.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
				File.WriteAllLines($"{key}_differentAutoResult.txt",
					differentAutoResult.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
				File.WriteAllLines($"{key}_sameFirstPos.txt",
					sameFirstPos.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
				File.WriteAllLines($"{key}_differentFirstPos.txt",
					differentFirstPos.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));

				Console.WriteLine($"Total: {landRemapResult.Count}");
				Console.WriteLine($"Land only auto: {landOnlyAutoResult.Count}");
				Console.WriteLine($"Core only auto: {coreOnlyAutoResult.Count}");
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
