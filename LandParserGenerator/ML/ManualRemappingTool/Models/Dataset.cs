using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace ManualRemappingTool
{
	public class Dataset
	{
		public string SavingPath { get; set; }

		public HashSet<string> Extensions { get; set; } = new HashSet<string>();

		public List<DatasetRecord> this[string src, string trg] =>
			Records.ContainsKey(src) && Records[src].ContainsKey(trg)
				? Records[src][trg] : new List<DatasetRecord>();

		public Dictionary<string, List<DatasetRecord>> this[string src] =>
			Records.ContainsKey(src) 
			? Records[src] : new Dictionary<string, List<DatasetRecord>>();

		#region Serializable

		public string SourceDirectoryPath { get; set; }
		public string TargetDirectoryPath { get; set; }

		public HashSet<string> FinalizedFiles { get; set; }

		public Dictionary<string, Dictionary<string, List<DatasetRecord>>> Records { get; private set; }

		public Dictionary<string, int> LandEntitiesCount { get; private set; }

		public string ExtensionsString
		{
			get { return String.Join("; ", Extensions); }

			set
			{
				/// Разбиваем строку на отдельные расширения, добавляем точку, если она отсутствует
				Extensions = new HashSet<string>(
					value.ToLower().Split(new char[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(ext => ext.StartsWith(".") ? ext : '.' + ext)
				);
			}
		}

		#endregion

		public void IntroduceSource(string sourceFinePath, int landCount)
		{
			LandEntitiesCount[sourceFinePath] = landCount;

			if(!Records.ContainsKey(sourceFinePath))
			{
				Records[sourceFinePath] = new Dictionary<string, List<DatasetRecord>>();
			}
		}

		public void Add(
			string sourceFilePath,
			string targetFilePath,
			int sourceOffset,
			int targetOffset,
			string entityType,
			bool hasDoubts = false,
			bool isAuto = false
		)
		{
			if (!Records[sourceFilePath].ContainsKey(targetFilePath))
			{
				Records[sourceFilePath][targetFilePath] = 
					new List<DatasetRecord>();
			}

			var existing = Records[sourceFilePath][targetFilePath]
				.Where(r => r.EntityType == entityType && r.SourceOffset == sourceOffset)
				.ToList();

			if(existing.Count > 0
				&& (existing.First().HasDoubts && !hasDoubts
				|| !existing.First().HasDoubts))
			{
				foreach(var elem in existing)
				{
					Records[sourceFilePath][targetFilePath].Remove(elem);
				}
			}

			Records[sourceFilePath][targetFilePath].Add(new DatasetRecord
			{
				HasDoubts = hasDoubts,
				EntityType = entityType,
				SourceOffset = sourceOffset,
				TargetOffset = targetOffset,
				IsAuto = isAuto
			});
		}

		public void Remove(
			string sourceFilePath,
			string targetFilePath,
			int sourceOffset,
			int targetOffset,
			string entityType
		)
		{
			if(Records.ContainsKey(sourceFilePath) 
				&& Records[sourceFilePath].ContainsKey(targetFilePath))
			{
				var elem = Records[sourceFilePath][targetFilePath]
					.FirstOrDefault(r => r.EntityType == entityType && r.SourceOffset == sourceOffset
						&& r.TargetOffset == targetOffset);

				if(elem != null)
				{
					Records[sourceFilePath][targetFilePath].Remove(elem);
				}
			}	
		}

		public void New()
		{
			Records = new Dictionary<string, Dictionary<string, List<DatasetRecord>>>();
			LandEntitiesCount = new Dictionary<string, int>();
			FinalizedFiles = new HashSet<string>();
			SavingPath = null;
		}

		public void Save(string path = null)
		{
			if (!String.IsNullOrEmpty(path))
			{
				SavingPath = path;
			}

			using (StreamWriter fs = new StreamWriter(SavingPath, false))
			{
				fs.WriteLine(SourceDirectoryPath);
				fs.WriteLine(TargetDirectoryPath);
				fs.WriteLine(ExtensionsString);

				foreach (var sourceFile in Records)
				{
					fs.WriteLine("*");
					fs.WriteLine(sourceFile.Key);
					fs.WriteLine(LandEntitiesCount[sourceFile.Key]);

					foreach (var targetFile in sourceFile.Value)
					{
						fs.WriteLine("**");

						fs.WriteLine(targetFile.Key);

						foreach (var record in targetFile.Value)
						{
							fs.WriteLine(record.ToString());
						}
					}
				}

				fs.WriteLine("***");

				foreach (var sourceFilePath in FinalizedFiles)
				{
					fs.WriteLine(sourceFilePath);
				}
			}
		}

		public static Dataset Load(string path)
		{
			var ds = new Dataset
			{
				SavingPath = path,
				Records = new Dictionary<string, Dictionary<string, List<DatasetRecord>>>(),
				LandEntitiesCount = new Dictionary<string, int>(),
				FinalizedFiles = new HashSet<string>()
			};

			var lines = File.ReadAllLines(path);

			ds.SourceDirectoryPath = lines[0];
			ds.TargetDirectoryPath = lines[1];
			ds.ExtensionsString = lines[2];

			string currentSourceFile = null, currentTargetFile = null;

			for (var i = 3; i < lines.Length; ++i)
			{
				if(lines[i] == "*")
				{
					currentSourceFile = lines[++i];
					ds.IntroduceSource(currentSourceFile, int.Parse(lines[++i]));

					continue;
				}

				if (lines[i] == "**")
				{
					currentTargetFile = lines[++i];
					continue;
				}

				if (lines[i] == "***")
				{
					for (var j = i + 1; j < lines.Length; ++j)
					{
						ds.FinalizedFiles.Add(lines[j]);
					}

					break;
				}

				var record = DatasetRecord.FromString(lines[i]);

				ds.Add(
					currentSourceFile,
					currentTargetFile,
					record.SourceOffset,
					record.TargetOffset,
					record.EntityType,
					record.HasDoubts,
					record.IsAuto
				);
			}

			return ds;
		}
	}

	public class DatasetRecord
	{
		public int SourceOffset { get; set; }
		public int TargetOffset { get; set; }
		public bool HasDoubts { get; set; }
		public string EntityType { get; set; }
		public bool IsAuto { get; set; }

		public override string ToString()
		{
			return $"{SourceOffset};{TargetOffset};{EntityType};{HasDoubts};{IsAuto}";
		}

		public static DatasetRecord FromString(string str)
		{
			var splitted = str.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

			return new DatasetRecord
			{
				SourceOffset = int.Parse(splitted[0]),
				TargetOffset = int.Parse(splitted[1]),
				EntityType = splitted[2],
				HasDoubts = bool.Parse(splitted[3]),
				IsAuto = splitted.Length > 4 ? bool.Parse(splitted[4]) : false
			};
		}
	}

	public class ExtendedDatasetRecord: DatasetRecord
	{
		public string SourceFilePath { get; set; }
		public string TargetFilePath { get; set; }

		public ExtendedDatasetRecord(DatasetRecord record):base()
		{
			SourceOffset = record.SourceOffset;
			TargetOffset = record.TargetOffset;
			HasDoubts = record.HasDoubts;
			EntityType = record.EntityType;
			IsAuto = record.IsAuto;
		}
	}
}
