using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using AspectCore;
using Land.Core.Markup;
using Land.Core.Parsing.Tree;

namespace Comparison
{
	class Program
	{
		const string MarkupFolder = @"D:\Repositories\_mapping\Roslyn\Common\base";
		const string RelinkFolder = @"D:\Repositories\_mapping\Roslyn\Common\modified";
		const int EntitiesPerFile = 3;
		const int FilesTake = 4417;
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

		static void Main(string[] args)
		{
			var entities = new Dictionary<string, MarkupManager>
			{
				{ "class_struct_interface", new MarkupManager(null) },
				{ "method", new MarkupManager(null) },
				{ "field", new MarkupManager(null) },
				{ "property", new MarkupManager(null) }
			};

			/// Создаём парсер C# и менеджер разметки из библиотеки LanD
			var landParser = sharp.ParserProvider.GetParser();
			landParser.SetPreprocessor(new SharpPreprocessing.ConditionalCompilation.SharpPreprocessor());

			var errors = new List<string>();

			/////////////////////////////////////////////// STAGE 1

			var counter = 0;
			var filesList = Directory.GetFiles(MarkupFolder, "*.cs").ToList();

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

			foreach (var file in files)
			{
				/// Читаем текст из файла
				var text = File.ReadAllText(file);

				/// Парсим при помощи LanD
				var landRoot = landParser.Parse(text);
				if (landParser.Log.Any(l => l.Type == Land.Core.MessageType.Error))
					errors.Add(file);

				/// Привязываемся к сущностям
				var visitor = new GetNodeSequenceVisitor();
				landRoot.Accept(visitor);

				foreach(var key in entities.Keys)
				{
					var subseq = visitor.Sequence.Where(n => n.Type == key).ToList();

					for (var i = 0; i < EntitiesPerFile; ++i)
					{
						if (subseq.Count == 0)
							break;

						var index = RandomGen.Next(0, subseq.Count);

						entities[key].AddConcernPoint(new TargetFileInfo
						{
							FileName = Path.GetFileName(file),
							FileText = text,
							TargetNode = subseq[index]
						});

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

			var searchArea = new Dictionary<string, Tuple<string, Node>>();
			foreach(var file in files)
			{
				var text = File.ReadAllText(file);
				var landRoot = landParser.Parse(text);
				searchArea[file] = new Tuple<string, Node>(text, landRoot);

				++counter;
				if (counter % 600 == 0)
					Console.WriteLine($"{counter} out of {files.Count}...");
			}

			Console.WriteLine("Remapping...");

			var report = new StreamWriter("report.txt");

			foreach (var key in entities.Keys)
			{
				List<Tuple<string, string>> sameAutoResult = new List<Tuple<string, string>>(),
					differentAutoResult = new List<Tuple<string, string>>(),
					basicOnlyAutoResult = new List<Tuple<string, string>>(),
					modifiedOnlyAutoResult = new List<Tuple<string, string>>(),
					sameFirstPos = new List<Tuple<string, string>>(),
					differentFirstPos = new List<Tuple<string, string>>();

				var similarities = new List<string>();
				var start = DateTime.Now;

				entities[key].ContextFinder = new BasicContextFinder();
				var basicRemapResult = entities[key].Remap(searchArea.Select(e => new TargetFileInfo
				{
					FileName = Path.GetFileName(e.Key),
					FileText = e.Value.Item1,
					TargetNode = e.Value.Item2
				}).ToList(), true, false);

				Console.WriteLine($"Basic remapping done in {DateTime.Now - start}");

				start = DateTime.Now;

				entities[key].ContextFinder = new ModifiedContextFinder();
				var modifiedRemapResult = entities[key].Remap(searchArea.Select(e => new TargetFileInfo
				{
					FileName = Path.GetFileName(e.Key),
					FileText = e.Value.Item1,
					TargetNode = e.Value.Item2
				}).ToList(), true, false);

				Console.WriteLine($"Modified remapping done in {DateTime.Now - start}");

				foreach (var cp in basicRemapResult.Keys)
				{
					var isBasicAuto = basicRemapResult[cp].FirstOrDefault()?.IsAuto ?? false;
					var isModifiedAuto = modifiedRemapResult.ContainsKey(cp) && (modifiedRemapResult[cp].FirstOrDefault()?.IsAuto ?? false);

					var sameFirst = basicRemapResult[cp].Count == 0 && modifiedRemapResult[cp].Count == 0
						|| basicRemapResult[cp].Count > 0 && modifiedRemapResult[cp].Count > 0
						&& String.Join("", modifiedRemapResult[cp][0].Context.HeaderContext.SelectMany(h => h.Value))
							.StartsWith(String.Join("", basicRemapResult[cp][0].Context.HeaderContext.SelectMany(h => h.Value)));

					if (basicRemapResult[cp].Count == 1)
						similarities.Add($"{ basicRemapResult[cp][0].Similarity };{ modifiedRemapResult[cp][0].Similarity }");
					else if (basicRemapResult[cp].Count > 1)
						similarities.Add($"{ basicRemapResult[cp][0].Similarity };{ modifiedRemapResult[cp][0].Similarity };{ basicRemapResult[cp][1].Similarity };{ modifiedRemapResult[cp][1].Similarity }");

					report.WriteLine(Path.GetFileName(cp.Context.FileName));
					report.WriteLine("*");

					report.WriteLine(String.Join(" ", cp.Context.HeaderContext.SelectMany(c=>c.Value)));
					report.WriteLine("*");

					foreach (var landCandidate in basicRemapResult[cp].Take(5))
					{
						report.WriteLine(String.Join(" ", landCandidate.Context.HeaderContext.SelectMany(c => c.Value)));
						report.WriteLine($"{landCandidate.Similarity}  [{landCandidate.HeaderSimilarity}; {landCandidate.InnerSimilarity}; {landCandidate.AncestorSimilarity}] {(landCandidate.IsAuto ? "*" : "")}");
					}
					report.WriteLine("*");

					foreach (var landCandidate in modifiedRemapResult[cp].Take(5))
					{
						report.WriteLine(String.Join(" ", landCandidate.Context.HeaderContext.SelectMany(c => c.Value)));
						report.WriteLine($"{landCandidate.Similarity}  [{landCandidate.HeaderSimilarity}; {landCandidate.InnerSimilarity}; {landCandidate.AncestorSimilarity}] {(landCandidate.IsAuto ? "*" : "")}");
					}
					report.WriteLine();
					report.WriteLine("**************************************************************");
					report.WriteLine();

					var tuple = new Tuple<string, string>(
							cp.Context.FileName,
							String.Join(" ", cp.Context.HeaderContext.SelectMany(h => h.Value))
						);

					if (isBasicAuto)
					{
						if (isModifiedAuto)
						{
							if (sameFirst)
								sameAutoResult.Add(tuple);
							else
								differentAutoResult.Add(tuple);
						}
						else
						{
							basicOnlyAutoResult.Add(tuple);
						}
					}
					else if(isModifiedAuto)
					{
						modifiedOnlyAutoResult.Add(tuple);
					}
					else
					{
						if (sameFirst)
							sameFirstPos.Add(tuple);
						else
							differentFirstPos.Add(tuple);
					}
				}
				File.WriteAllLines($"{key}_similarities.txt", similarities);
				File.WriteAllLines($"{key}_modifiedOnlyAutoResult.txt",
					modifiedOnlyAutoResult.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
				File.WriteAllLines($"{key}_basicOnlyAutoResult.txt",
					basicOnlyAutoResult.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
				File.WriteAllLines($"{key}_sameAutoResult.txt", 
					sameAutoResult.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
				File.WriteAllLines($"{key}_differentAutoResult.txt", 
					differentAutoResult.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
				File.WriteAllLines($"{key}_sameFirstPos.txt", 
					sameFirstPos.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
				File.WriteAllLines($"{key}_differentFirstPos.txt", 
					differentFirstPos.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));

				Console.WriteLine($"Basic only auto: {basicOnlyAutoResult.Count}");
				Console.WriteLine($"Modified only auto: {modifiedOnlyAutoResult.Count}");
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

		//static void Main(string[] args)
		//{
		//	var entities = new Dictionary<string, Tuple<MarkupManager, List<PointOfInterest>>>
		//	{
		//		{ "class_struct_interface", new Tuple<MarkupManager, List<PointOfInterest>>(new MarkupManager(), new List<PointOfInterest>()) },
		//		{ "method", new Tuple<MarkupManager, List<PointOfInterest>>(new MarkupManager(), new List<PointOfInterest>()) },
		//		{ "field", new Tuple<MarkupManager, List<PointOfInterest>>(new MarkupManager(), new List<PointOfInterest>()) },
		//		{ "property", new Tuple<MarkupManager, List<PointOfInterest>>(new MarkupManager(), new List<PointOfInterest>()) }
		//	};

		//	/// Создаём парсер C# и менеджер разметки из библиотеки LanD
		//	var landParser = sharp.ParserProvider.GetParser();
		//	landParser.SetPreprocessor(new SharpPreprocessing.ConditionalCompilation.SharpPreprocessor());
		//	var landMarkupManager = new MarkupManager();

		//	var coreParser = new ParserWrapper("../../components/AspectCore");

		//	var landErrors = new List<string>();
		//	var coreErrors = new List<string>();

		//	/////////////////////////////////////////////// STAGE 1

		//	var counter = 0;
		//	var filesList = Directory.GetFiles(MarkupFolder, "*.cs").ToList();

		//	var files = new HashSet<string>();
		//	if (filesList.Count > FilesTake)
		//	{
		//		while (files.Count < FilesTake)
		//			files.Add(filesList[RandomGen.Next(filesList.Count)]);
		//	}
		//	else
		//	{
		//		files.UnionWith(filesList);
		//	}

		//	if (Directory.Exists("./test"))
		//		Directory.Delete("./test", true);
		//	Directory.CreateDirectory("test");
		//	foreach (var file in files)
		//		File.Copy(file, $"./test/{Path.GetFileName(file)}");

		//	foreach (var file in files)
		//	{
		//		/// Читаем текст из файла
		//		var text = File.ReadAllText(file);

		//		/// Парсим при помощи LanD
		//		var landRoot = landParser.Parse(text);
		//		if (landParser.Log.Any(l => l.Type == Land.Core.MessageType.Error))
		//			landErrors.Add(file);

		//		/// Парсим при помощи AspectCore
		//		var coreRoot = coreParser.ParseText(text, file);

		//		/// Привязываемся к сущностям
		//		var visitor = new GetNodeSequenceVisitor();
		//		landRoot.Accept(visitor);

		//		foreach (var key in entities.Keys)
		//		{
		//			var subseq = visitor.Sequence.Where(n => n.Type == key).ToList();
		//			var usedIndices = new List<int>();

		//			for (var i = 0; i < Math.Min(EntitiesPerFile, subseq.Count); ++i)
		//			{
		//				var index = RandomGen.Next(0, subseq.Count);

		//				entities[key].Item1.AddConcernPoint(new TargetFileInfo
		//				{
		//					FileName = Path.GetFileName(file),
		//					FileText = text,
		//					TargetNode = subseq[index]
		//				});

		//				var corePoint = TreeSearchEngine.FindPointByLocation(
		//					coreRoot,
		//					subseq[index].Children.FirstOrDefault(c => c.Type == "name").Location.Start.Line,
		//					subseq[index].Children.FirstOrDefault(c => c.Type == "name").Location.Start.Column
		//				).FirstOrDefault();
		//				//corePoint.ApplyInnerContext();

		//				entities[key].Item2.Add(corePoint);

		//				subseq.RemoveAt(index);
		//			}
		//		}

		//		++counter;
		//		if (counter % 600 == 0)
		//			Console.WriteLine($"{counter} out of {files.Count}...");
		//	}

		//	foreach (var key in entities.Keys)
		//		Console.WriteLine($"{key}: land - {entities[key].Item1.GetConcernPoints().Count}; core - {entities[key].Item2.Count}");

		//	//Console.WriteLine("********** Ошибочные файлы **********");

		//	//foreach (var file in landErrors)
		//	//	Console.WriteLine(file);

		//	//Console.WriteLine("*********************");

		//	//foreach (var file in coreErrors)
		//	//	Console.WriteLine(file);

		//	/////////////////////////////////////////////// STAGE 2

		//	Console.WriteLine("Stage 2 started...");

		//	counter = 0;
		//	files = new HashSet<string>(files.Select(f => Path.Combine(RelinkFolder, Path.GetFileName(f))));

		//	var searchArea = new Dictionary<string, Tuple<string, Node, PointOfInterest>>();
		//	foreach (var file in files)
		//	{
		//		var text = File.ReadAllText(file);
		//		var landRoot = landParser.Parse(text);
		//		var coreRoot = coreParser.ParseText(text, file);
		//		searchArea[file] = new Tuple<string, Node, PointOfInterest>(text, landRoot, coreRoot);

		//		++counter;
		//		if (counter % 600 == 0)
		//			Console.WriteLine($"{counter} out of {files.Count}...");
		//	}

		//	Console.WriteLine("Remapping...");

		//	var report = new StreamWriter("report.txt");

		//	foreach (var key in entities.Keys)
		//	{
		//		List<Tuple<string, string>> sameAutoResult = new List<Tuple<string, string>>(),
		//			differentAutoResult = new List<Tuple<string, string>>(),
		//			landOnlyAutoResult = new List<Tuple<string, string>>(),
		//			coreOnlyAutoResult = new List<Tuple<string, string>>(),
		//			sameFirstPos = new List<Tuple<string, string>>(),
		//			differentFirstPos = new List<Tuple<string, string>>();

		//		var landRemapResult = entities[key].Item1.Remap(searchArea.Select(e => new TargetFileInfo
		//		{
		//			FileName = Path.GetFileName(e.Key),
		//			FileText = e.Value.Item1,
		//			TargetNode = e.Value.Item2
		//		}).ToList(), true);

		//		Console.WriteLine($"LanD remapping done!");

		//		var coreRemapResult = new Dictionary<ConcernPoint, TreeSearchResult>();
		//		var landPoints = entities[key].Item1.GetConcernPoints();

		//		for (var i = 0; i < entities[key].Item2.Count; ++i)
		//		{
		//			var poi = entities[key].Item2[i];
		//			var parsed = searchArea
		//				.Where(e => Path.GetFileNameWithoutExtension(e.Key) == Path.GetFileNameWithoutExtension(poi.FileName))
		//				.FirstOrDefault();

		//			coreRemapResult[landPoints[i]] =
		//				TreeSearchEngine.FindPointInTree2(parsed.Value.Item3, poi, parsed.Value.Item1);
		//		}

		//		Console.WriteLine($"Core remapping done!");

		//		foreach (var cp in landRemapResult.Keys)
		//		{
		//			report.WriteLine(Path.GetFileName(cp.Context.FileName));
		//			report.WriteLine("*");

		//			report.WriteLine(String.Join(" ", cp.Context.HeaderContext.SelectMany(c => c.Value)));
		//			report.WriteLine("*");

		//			foreach (var landCandidate in landRemapResult[cp].Take(5))
		//			{
		//				report.WriteLine(String.Join(" ", landCandidate.Context.HeaderContext.SelectMany(c => c.Value)));
		//				report.WriteLine(landCandidate.Similarity);
		//			}
		//			report.WriteLine("*");

		//			for (var j = 0; j < coreRemapResult[cp].Count && j < 5; ++j)
		//			{
		//				var coreCandidate = coreRemapResult[cp][j];
		//				report.WriteLine(String.Join(" ", coreCandidate.Context[0].Name));
		//				report.WriteLine(coreRemapResult[cp].GetNodeSimilarity(j));
		//			}
		//			report.WriteLine();
		//			report.WriteLine("**************************************************************");
		//			report.WriteLine();

		//			var isLandAuto = landRemapResult[cp].FirstOrDefault()?.Similarity >= 0.6
		//				&& (landRemapResult[cp].Count == 1
		//					|| (1 - landRemapResult[cp][1].Similarity) >= (1 - landRemapResult[cp][0].Similarity) * 1.5);

		//			var isCoreAuto = coreRemapResult[cp].GetNodeSimilarity(0) >= 0.6
		//				&& (coreRemapResult[cp].Count == 1
		//					|| (1 - coreRemapResult[cp].GetNodeSimilarity(1)) >= (1 - coreRemapResult[cp].GetNodeSimilarity(0)) * 2);

		//			var sameFirst = coreRemapResult[cp].Count > 0 && landRemapResult[cp].Count > 0
		//				&& String.Join("", coreRemapResult[cp][0].Context[0].Name)
		//					.StartsWith(String.Join("", landRemapResult[cp][0].Context.HeaderContext.SelectMany(h => h.Value)));

		//			var tuple = new Tuple<string, string>(
		//					cp.Context.FileName,
		//					String.Join(" ", cp.Context.HeaderContext.SelectMany(h => h.Value))
		//				);

		//			if (isLandAuto)
		//			{
		//				if (isCoreAuto)
		//				{
		//					if (sameFirst)
		//						sameAutoResult.Add(tuple);
		//					else
		//						differentAutoResult.Add(tuple);
		//				}
		//				else
		//				{
		//					landOnlyAutoResult.Add(tuple);
		//				}
		//			}
		//			else if (isCoreAuto)
		//			{
		//				coreOnlyAutoResult.Add(tuple);
		//			}
		//			else
		//			{
		//				if (sameFirst)
		//					sameFirstPos.Add(tuple);
		//				else
		//					differentFirstPos.Add(tuple);
		//			}
		//		}
		//		File.WriteAllLines($"{key}_coreOnlyAutoResult.txt",
		//			coreOnlyAutoResult.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
		//		File.WriteAllLines($"{key}_landOnlyAutoResult.txt",
		//			landOnlyAutoResult.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
		//		File.WriteAllLines($"{key}_sameAutoResult.txt",
		//			sameAutoResult.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
		//		File.WriteAllLines($"{key}_differentAutoResult.txt",
		//			differentAutoResult.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
		//		File.WriteAllLines($"{key}_sameFirstPos.txt",
		//			sameFirstPos.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));
		//		File.WriteAllLines($"{key}_differentFirstPos.txt",
		//			differentFirstPos.SelectMany(r => new string[] { r.Item1, r.Item2, "" }));

		//		Console.WriteLine($"Land only auto: {landOnlyAutoResult.Count}");
		//		Console.WriteLine($"Core only auto: {coreOnlyAutoResult.Count}");
		//		Console.WriteLine($"Same auto: {sameAutoResult.Count}");
		//		Console.WriteLine($"Different auto: {differentAutoResult.Count}");
		//		Console.WriteLine($"Same first: {sameFirstPos.Count}");
		//		Console.WriteLine($"Different first: {differentFirstPos.Count}");
		//		Console.WriteLine($"'{key}' done!");
		//	}

		//	report.Close();

		//	Console.WriteLine("Job's done!");
		//	Console.ReadLine();
		//}
	}
}
