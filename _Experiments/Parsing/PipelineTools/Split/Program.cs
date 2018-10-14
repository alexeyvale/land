using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Split
{
	class Program
	{
		private static string SplittedFilesTargetDirectory { get; set; }
		private static Dictionary<string, StreamWriter> SplittedFiles { get; set; }

		/// target_directory (--change to_type from_type+)* (--break type_name block_length)
		/// target_directory	-	каталог, в который надо поместить результаты разбиения отчёта
		/// --change	-	опция для переименовывания land-типов и группировки нескольких типов в один
		/// --break		-	опция для разделения значения типа type_name на несколько значений длиной block_length, блоки разделяются пробелами
		static void Main(string[] args)
		{
			if(args.Length >= 1)
			{
				var landReportContent = File.ReadAllLines(
					Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) 
					+ @"\LanD Workspace\last_batch_parsing_report.txt"
				);
				var changeOption = new Dictionary<string, string>();
				var breakOption = new Dictionary<string, int>();

				try
				{
					var changeToType = String.Empty;

					for (var i = 1; i < args.Length; ++i)
					{
						switch (args[i].ToLower())
						{
							case "--change":
								changeToType = args[i + 1];
								++i;
								break;
							case "--break":
								breakOption[args[i + 1]] = int.Parse(args[i + 2]);
								i += 2;
								break;
							default:
								changeOption[args[i]] = changeToType;
								break;
						}
					}
				}
				catch
				{
					Console.WriteLine("Ошибка при обработке опций");
					return;
				}

				SplittedFiles = new Dictionary<string, StreamWriter>();
				SplittedFilesTargetDirectory = args[0];

				if (Directory.Exists(SplittedFilesTargetDirectory))
					Directory.Delete(SplittedFilesTargetDirectory, true);
				Directory.CreateDirectory(SplittedFilesTargetDirectory);

				var sourceFileName = String.Empty;
				var sourceFileIslands = new Dictionary<string, List<string>>();

				for (var i = 2; i < landReportContent.Length; ++i)
				{
					var line = landReportContent[i].Trim();

					/// Если начинается информация про следующий файл
					if (line == "*")
					{
						if (!String.IsNullOrEmpty(sourceFileName))
							FlushBuffer(sourceFileName, sourceFileIslands);

						sourceFileName = landReportContent[i + 1].Trim();
						sourceFileIslands = new Dictionary<string, List<string>>();

						++i;
					}
					else
					{
						/// в очередной паре строк первая - имя типа сущности, вторая - значение,
						/// по которому её идентифицируем
						var islandType = line;
						var islandName = landReportContent[i + 1].Trim();

						if (changeOption.ContainsKey(islandType))
							islandType = changeOption[islandType];
						var breakValueSize = breakOption.ContainsKey(islandType) ? breakOption[islandType] : (int?)null;

						if (!sourceFileIslands.ContainsKey(islandType))
							sourceFileIslands[islandType] = new List<string>();

						if (breakValueSize.HasValue)
						{
							var splitted = islandName.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
							var curIndex = 0;
							while (curIndex < splitted.Length)
							{
								sourceFileIslands[islandType].Add(String.Join(" ", splitted.Skip(curIndex).Take(breakValueSize.Value)));
								curIndex += breakValueSize.Value;
							}
						}
						else
						{
							sourceFileIslands[islandType].Add(islandName);
						}

						++i;
					}
				}

				if (!String.IsNullOrEmpty(sourceFileName))
					FlushBuffer(sourceFileName, sourceFileIslands);

				foreach (var file in SplittedFiles.Values)
					file.Close();
			}
		}

		private static void FlushBuffer(string fileName, Dictionary<string, List<string>> islands)
		{
			foreach(var kvp in islands.Where(kvp=>kvp.Value.Count > 0))
			{
				if(!SplittedFiles.ContainsKey(kvp.Key))
					SplittedFiles[kvp.Key] = new StreamWriter(Path.Combine(SplittedFilesTargetDirectory, $"{kvp.Key}_land.txt"), false);

				SplittedFiles[kvp.Key].WriteLine('*');
				SplittedFiles[kvp.Key].WriteLine(fileName);

				foreach (var val in kvp.Value)
					SplittedFiles[kvp.Key].WriteLine(val);
			}
		}
	}
}
