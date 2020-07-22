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

		private static ILookup<string, ExtendedDatasetRecord> GetRecords(Dataset d)
		{
			var records = new List<ExtendedDatasetRecord>();
			var finalized = d.Records.Where(r => d.FinalizedFiles.Contains(r.Key)).ToList();

			foreach (var sourcePathRecordsPair in finalized)
			{
				var absSourcePath =
					GetAbsolutePath(d.SourceDirectoryPath, sourcePathRecordsPair.Key);

				/// Побочный эффект
				d.LandEntitiesCount[absSourcePath] = d.LandEntitiesCount[sourcePathRecordsPair.Key];

				foreach (var targetRelativePath in sourcePathRecordsPair.Value.Keys)
				{
					var absTargetPath =
						GetAbsolutePath(d.TargetDirectoryPath, targetRelativePath);

					records.AddRange(sourcePathRecordsPair.Value[targetRelativePath]
						.Select(e => new ExtendedDatasetRecord(e)
						{
							SourceFilePath = absSourcePath,
							TargetFilePath = absTargetPath
						})
					);
				}
			}

			return records.ToLookup(r => r.SourceFilePath, r => r);
		}

		static void Main(string[] args)
		{
			if (args.Length != 2)
			{
				return;
			}

			var totalPositive = 0;
			var totalNegative = 0;

			var contextFinder = new ContextFinder();

			var parsers = new ParserManager();
			parsers.Load(LoadSettings(SETTINGS_DEFAULT_PATH), CACHE_DIRECTORY, new List<Message>());

			var processedSources = new HashSet<string>();

			var groupedDatasets = Directory.GetFiles(args[0], $"*{DATASET_FILE_EXT}", SearchOption.TopDirectoryOnly)
				.Select(f => Dataset.Load(f))
				.ToLookup(d => d.ExtensionsString, d => d);

			/// Контрольная эвристика, всё, что находит она, не помещаем в датасет
			var heuristic = new ProgrammingLanguageHeuristic();

			foreach (var extGroup in groupedDatasets)
			{
				Console.WriteLine($"Current extension: {extGroup.Key}");

				/// Каждый датасет рассматриваем в отдельности, чтобы не держать в памяти
				/// слишком большое количество деревьев
				foreach (var dataset in extGroup)
				{
					Console.WriteLine($"Processing new dataset...");

					var records = GetRecords(dataset);
					var sourceFiles = records.Select(e => e.Key).ToList();
					var targetFiles = records.SelectMany(e => e.Select(el => el.TargetFilePath)).Distinct()
						.Select(e =>
						{
							var extension = Path.GetExtension(e);
							var text = File.ReadAllText(e);

							return new ParsedFile
							{
								Text = text,
								BindingContext = PointContext.GetFileContext(e, text)
							};
						})
						.ToDictionary(e => e.BindingContext.Name, e => e);

					Console.WriteLine($"Target files parsed...");

					foreach (var sourceFilePath in sourceFiles)
					{
						if(processedSources.Contains(sourceFilePath))
						{
							continue;
						}

						Console.WriteLine($"Current source file: {sourceFilePath}");
						processedSources.Add(sourceFilePath);

						/// Если в рассматриваемом исходном файле столько же островных сущностей,
						/// сколько было произведено автоперепривязок, он нам не нужен
						if(dataset.LandEntitiesCount[sourceFilePath] 
							== records[sourceFilePath].Where(r=>r.IsAuto).Count())
						{
							continue;
						}

						var extension = Path.GetExtension(sourceFilePath);
						var text = File.ReadAllText(sourceFilePath);

						var sourceParsed = new ParsedFile
						{
							Root = parsers[extension].Parse(text),
							Text = text,
							BindingContext = PointContext.GetFileContext(sourceFilePath, text)
						};

						Console.WriteLine($"Source file parsed...");

						/// Привязываемся ко всему в исходном файле
						var markupManager = new MarkupManager(
							name => sourceParsed.Name == name ? sourceParsed : targetFiles[name],
							new ProgrammingLanguageHeuristic()
						);

						var customContextFinder = new CopyPaste.ContextFinder();
						customContextFinder.GetParsed = markupManager.ContextFinder.GetParsed;

						markupManager.AddLand(
							sourceParsed,
							/// TODO Если в train будем учитывать контекст ближайших, придётся расширить searchArea
							new List<ParsedFile> { sourceParsed }
						);

						/// Сразу отсеиваем точки, которые находятся эвристикой
						var allPoints = markupManager.GetConcernPoints()
							.Where(p =>
							{
								/// Находим запись о корректном сопоставлении
								var record = records[sourceFilePath]
									.SingleOrDefault(e => e.SourceOffset == p.AstNode.Location.Start.Offset);

								return !(record?.IsAuto ?? false);
							})
							.ToList();

						//var similarFiles = targetFiles.Values
						//	.Where(f => markupManager.ContextFinder.AreFilesSimilarEnough(f.BindingContext, sourceParsed.BindingContext))
						//	.ToList();
						var similarFiles = targetFiles.Values
							.Where(f => Path.GetFileName(f.BindingContext.Name) == Path.GetFileName(sourceParsed.BindingContext.Name))
							.ToList();
						var similarFilesNames = new HashSet<string>(similarFiles.Select(f => f.Name));

						foreach(var file in similarFiles)
						{
							if(file.Root == null)
							{
								file.Root = parsers[extension].Parse(file.Text);
							}
						}

						//var pointsInSimilarFiles = allPoints.Where(p => similarFilesNames.Contains(
						//	records[sourceFilePath].SingleOrDefault(e => e.SourceOffset == p.AstNode.Location.Start.Offset)?.TargetFilePath
						//)).ToList();

						var localSearchResult = customContextFinder.Find(allPoints, similarFiles,
							CopyPaste.ContextFinder.SearchType.Local);

						//var globalSearchResult = customContextFinder.Find(allPoints.Except(pointsInSimilarFiles).ToList(), targetFiles.Values,
						//	CopyPaste.ContextFinder.SearchType.Global);

						//foreach (var kvp in localSearchResult)
						//{
						//	globalSearchResult[kvp.Key] = kvp.Value;
						//}

						var result = localSearchResult.ToLookup(e => e.Key.Context.Type, e => e);

						foreach (var typePointsPair in result)
						{
							Console.WriteLine($"Current type: {typePointsPair.Key}");

							/// Будем дописывать строчки в train-файл 
							var trainFileName = Path.Combine(args[1], $"{extGroup.Key.Replace(",", ".").Trim('.')}.{typePointsPair.Key}.csv");
							var trainFile = new StreamWriter(trainFileName, true);

							/// Если файл только что создан, записываем туда заголовок
							if (trainFile.BaseStream.Position == 0)
							{
								trainFile.WriteLine(CandidateFeatures.ToHeaderString(";"));
							}

							foreach (var pointCandidatesPair in typePointsPair)
							{
								totalNegative += pointCandidatesPair.Value.Count;

								/// находим запись в датасете о корректном сопоставлении
								var record = records[sourceFilePath]
									.SingleOrDefault(e => e.SourceOffset == pointCandidatesPair.Key.AstNode.Location.Start.Offset);

								/// если запись нашли и корректное сопоставление есть в одном из анализируемых файлов
								if (record != null)
								{
									var correctCandidate = pointCandidatesPair.Value.SingleOrDefault(e =>
										e.Node.Location.Start.Offset == record.TargetOffset);

									if (correctCandidate != null)
									{
										correctCandidate.IsAuto = true;

										++totalPositive;
										--totalNegative;
									}
								}

								/// записываем информацию о кандидатах в выходной файл
								foreach (var line in contextFinder.GetFeatures(pointCandidatesPair.Key.Context, pointCandidatesPair.Value)
									.Select(f => f.ToString(";")))
								{
									trainFile.WriteLine(line);
								}
							}

							trainFile.Close();
						}
					}
				}
			}

			Console.WriteLine($"Total results: {totalNegative} wrong candidates; {totalPositive} good candidates");

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
