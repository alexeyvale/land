using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using ManualRemappingTool;
using Land.Control;
using System.Runtime.Serialization;
using Land.Core;
using Land.Markup;
using Land.Markup.Binding;
using Land.Markup.CoreExtension;

namespace DatasetToTrainConverter
{
	class Program
	{
		#region Consts

		private static readonly string APP_DATA_DIRECTORY =
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\LanD Control";
		private static readonly string CACHE_DIRECTORY =
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\LanD Control\Cache";
		public static readonly string SETTINGS_FILE_NAME = "LandExplorerSettings.xml";

		public static string SETTINGS_DEFAULT_PATH =>
			System.IO.Path.Combine(APP_DATA_DIRECTORY, SETTINGS_FILE_NAME);

		private const string DATASET_FILE_EXT = ".ds.txt";

		#endregion

		static void Main(string[] args)
		{
			if (args.Length != 2)
			{
				return;
			}

			var parsers = new ParserManager();
			parsers.Load(LoadSettings(SETTINGS_DEFAULT_PATH), CACHE_DIRECTORY, new List<Message>());

			/// Превращаем набор датасетов в группы записей, записи сгруппированы по расширению файлов
			var groupedRecords = Directory.GetFiles(args[0], $"*{DATASET_FILE_EXT}", SearchOption.TopDirectoryOnly)
				.Select(f => Dataset.Load(f))
				.GroupBy(d => d.ExtensionsString, d =>
				{
					var records = new List<ExtendedDatasetRecord>();
					var finalized = d.Records.Where(r => d.FinalizedFiles.Contains(r.Key));

					foreach(var unsupported in finalized.Where(r=>r.Value.Count > 1))
					{
						Console.WriteLine(
							$"Mapping to multiple files for {GetAbsolutePath(d.SourceDirectoryPath, unsupported.Key)} is unsupported"
						);
					}

					/// Рассматриваем только записи, касающиеся файлов, элементы которых были сопоставлены элементам в одном файле
					foreach (var sorceFile in d.Records.Keys.Where(sf => d[sf].Count == 1))
					{
						var absSourcePath =
							GetAbsolutePath(d.SourceDirectoryPath, sorceFile);

						foreach (var targetFile in d[sorceFile].Keys)
						{
							var absTargetPath =
								GetAbsolutePath(d.TargetDirectoryPath, targetFile);

							records.AddRange(d[sorceFile][targetFile]
								.Select(e => new ExtendedDatasetRecord(e)
								{
									SourceFilePath = absSourcePath,
									TargetFilePath = absTargetPath
								})
							);
						}
					}
					return records;
				})
				.ToDictionary(g => g.Key, g => g.SelectMany(r => r).ToLookup(r=>r.SourceFilePath, r => r));

			foreach (var ext in groupedRecords.Keys)
			{
				// TODO Если важна перепривязка не только в рамках исх. файла и модиф., надо парсить все файлы в исходном каталоге и целевом

				/// Парсим все исходные и целевые файлы
				var sourceFiles = groupedRecords[ext].Select(r => r.Key).Distinct()
					.ToDictionary(e => e, e =>
					 {
						 var extension = Path.GetExtension(e);
						 var text = File.ReadAllText(e);

						 return new ParsedFile
						 {
							 Root = parsers[extension].Parse(text),
							 Text = text,
							 BindingContext = PointContext.GetFileContext(e, text)
						 };
					 });

				var targetFiles = groupedRecords[ext].SelectMany(r => r.Select(e=>e.TargetFilePath)).Distinct()
					.Select(e =>
					{
						var extension = Path.GetExtension(e);
						var text = File.ReadAllText(e);

						return new ParsedFile
						{
							Root = parsers[extension].Parse(text),
							Text = text,
							BindingContext = PointContext.GetFileContext(e, text)
						};
					})
					.ToDictionary(e => e.BindingContext.Name, e =>
					{
						var visitor = new LandExplorerVisitor();
						e.Root.Accept(visitor);

						return new
						{
							Parsed = e,
							Nodes = visitor.Land.ToLookup(n => n.Type)
						};
					});

				foreach (var sourceFilePair in sourceFiles)
				{
					Console.WriteLine(sourceFilePair.Key);

					/// Привязываемся ко всему в исходном файле
					var markupManager = new MarkupManager(name => sourceFiles.ContainsKey(name)
						? sourceFiles[name] : targetFiles[name].Parsed);

					markupManager.AddLand(
						sourceFilePair.Value,
						sourceFiles.Values.ToList()
					);

					var points = markupManager.GetConcernPoints()
						.GroupBy(p => p.Context.Type)
						.ToDictionary(g=>g.Key, g=>g.ToList());

					var targetFilePath = groupedRecords[ext][sourceFilePair.Key].FirstOrDefault()?.TargetFilePath;

					/// Для точек каждого типа оцениваем кандидатов
					foreach (var type in points.Keys)
					{
						var trainFileName = Path.Combine(args[0], $"{ext.Replace(",",".").Trim('.')}.{type}.csv");
						var trainFile = new StreamWriter(trainFileName);

						trainFile.WriteLine(CandidateFeatures.ToHeaderString(";"));

						var candidates = targetFiles[targetFilePath].Nodes[type].Select(n => new RemapCandidateInfo
						{
							Node = n,
							File = targetFiles[targetFilePath].Parsed,
							Context = markupManager.ContextFinder.ContextManager
								.GetContext(n, targetFiles[targetFilePath].Parsed, new SiblingsConstructionArgs(), null)
						}).ToList();

						foreach(var point in points[type])
						{
							markupManager.ContextFinder.ComputeSimilarities(point.Context, candidates, true);

							var record = groupedRecords[ext][sourceFilePair.Key]
								.SingleOrDefault(e => e.SourceOffset == point.AstNode.Location.Start.Offset);

							if(record != null)
							{
								var correctCandidate = candidates.SingleOrDefault(e => e.Node.Location.Start.Offset == record.TargetOffset);

								if(correctCandidate != null)
								{
									correctCandidate.IsAuto = true;
								}
							}

							foreach(var line in ContextFinder.GetFeatures(point.Context, candidates)
								.Select(f => f.ToString(";")))
							{
								trainFile.WriteLine(line);
							}
						}

						trainFile.Close();
					}
				}
			}

			Console.ReadLine();
		}

		private static string GetAbsolutePath(string directryPath, string filePath) =>
			Path.Combine(directryPath, filePath);

		private static string GetRelativePath(string directoryPath, string filePath)
		{
			var directoryUri = new Uri(directoryPath + "/");

			return Uri.UnescapeDataString(
				directoryUri.MakeRelativeUri(new Uri(filePath)).ToString()
			);
		}

		private static LandExplorerSettings LoadSettings(string path)
		{
			if (File.Exists(path))
			{
				var serializer = new DataContractSerializer(
					typeof(LandExplorerSettings), new Type[] { typeof(ParserSettingsItem) }
				);

				using (FileStream fs = new FileStream(path, FileMode.Open))
				{
					return (LandExplorerSettings)serializer.ReadObject(fs);
				}
			}
			else
			{
				return null;
			}
		}
	}
}
