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

					foreach (var sorceFile in d.Records.Keys)
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
				.ToDictionary(g => g.Key, g => g.SelectMany(r => r).ToLookup(r => r.SourceFilePath, r => r));

			foreach (var ext in groupedRecords.Keys)
			{
				Console.WriteLine($"Current extension: {ext}");

				var sourceFiles = groupedRecords[ext]
					.Select(r => r.Key).Distinct().ToList();

				var targetFiles = groupedRecords[ext].SelectMany(r => r.Select(e => e.TargetFilePath)).Distinct()
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
					.ToDictionary(e => e.BindingContext.Name, e => e);

				Console.WriteLine($"Target files parsed...");

				foreach (var sourceFilePath in sourceFiles)
				{
					Console.WriteLine($"Current source file: {sourceFilePath}");

					var extension = Path.GetExtension(sourceFilePath);
					var text = File.ReadAllText(sourceFilePath);

					var sourceParsed =  new ParsedFile
					{
						Root = parsers[extension].Parse(text),
						Text = text,
						BindingContext = PointContext.GetFileContext(sourceFilePath, text)
					};

					Console.WriteLine($"Source file parsed...");

					/// Привязываемся ко всему в исходном файле
					var markupManager = new MarkupManager(
						name => sourceParsed.Name == name? sourceParsed : targetFiles[name],
						new ProgrammingLanguageHeuristic()
					);

					var customContextFinder = new CopyPaste.ContextFinder();
					customContextFinder.GetParsed = markupManager.ContextFinder.GetParsed;

					markupManager.AddLand(
						sourceParsed,
						/// Если в train будем учитывать контекст ближайших, придётся расширить searchArea
						new List<ParsedFile> { sourceParsed }
					);

					var allPoints = markupManager.GetConcernPoints();
					var allFiles = groupedRecords[ext][sourceFilePath]
						.Select(e => e.TargetFilePath)
						.Distinct()
						.Select(e => targetFiles[e])
						.ToList();

					var similarFiles = allFiles
						.Where(f => markupManager.ContextFinder.AreFilesSimilarEnough(f.BindingContext, sourceParsed.BindingContext))
						.ToList();
					var similarFilesNames = new HashSet<string>(similarFiles.Select(f=>f.Name));

					var pointsInSimilarFiles = allPoints.Where(p => similarFilesNames.Contains(
						groupedRecords[ext][sourceFilePath].SingleOrDefault(e => e.SourceOffset == p.AstNode.Location.Start.Offset)?.TargetFilePath
					)).ToList();

					var localSearchResult = customContextFinder.Find(pointsInSimilarFiles, similarFiles, 
						CopyPaste.ContextFinder.SearchType.Local);
					var globalSearchResult = customContextFinder.Find(allPoints.Except(pointsInSimilarFiles).ToList(), allFiles, 
						CopyPaste.ContextFinder.SearchType.Global);

					foreach(var kvp in localSearchResult)
					{
						globalSearchResult[kvp.Key] = kvp.Value;
					}

					var result = globalSearchResult.ToLookup(e => e.Key.Context.Type, e => e);

					foreach(var typePointsPair in result)
					{
						Console.WriteLine($"Current type: {typePointsPair.Key}");

						/// Будем дописывать строчки в train-файл 
						var trainFileName = Path.Combine(args[1], $"{ext.Replace(",", ".").Trim('.')}.{typePointsPair.Key}.csv");
						var trainFile = new StreamWriter(trainFileName, true);

						/// Если файл только что создан, записываем туда заголовок
						if (trainFile.BaseStream.Position == 0)
						{
							trainFile.WriteLine(CandidateFeatures.ToHeaderString(";"));
						}

						foreach(var pointCandidatesPair in typePointsPair)
						{
							/// находим запись в датасете о корректном сопоставлении
							var record = groupedRecords[ext][sourceFilePath]
									.SingleOrDefault(e => e.SourceOffset == pointCandidatesPair.Key.AstNode.Location.Start.Offset);

							/// если запись нашли и корректное сопоставление есть в одном из анализируемых файлов
							if (record != null)
							{
								var correctCandidate = pointCandidatesPair.Value.SingleOrDefault(e =>
									e.Node.Location.Start.Offset == record.TargetOffset);

								if(correctCandidate.HeaderCoreSimilarity == 1 
									&& correctCandidate.AncestorSimilarity == 1 
									&& correctCandidate.InnerSimilarity == 1)
								{
									continue;
								}

								if (correctCandidate != null)
								{
									correctCandidate.IsAuto = true;
								}
							}

							/// записываем информацию о кандидатах в выходной файл
							foreach (var line in ContextFinder.GetFeatures(pointCandidatesPair.Key.Context, pointCandidatesPair.Value)
								.Select(f => f.ToString(";")))
							{
								trainFile.WriteLine(line);
							}
						}

						trainFile.Close();
					}
				}
			}

			Console.WriteLine("Process finished!");

			Console.ReadLine();
		}

		private static string GetAbsolutePath(string directryPath, string filePath) =>
			Path.Combine(directryPath, filePath);

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
