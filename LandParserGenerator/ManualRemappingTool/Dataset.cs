﻿using System;
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

		#region Serializable

		public string SourceDirectoryPath { get; set; }
		public string TargetDirectoryPath { get; set; }
		public string EntityType { get; set; }
		public Dictionary<string, Dictionary<string, List<DatasetRecord>>> Records { get; set; }

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

		public void Add(
				string sourceFilePath,
				string targetFilePath,
				int sourceLine,
				int targetLine,
				string entityType,
				bool hasDoubts = false
			)
		{
			if (!Records.ContainsKey(sourceFilePath))
			{
				Records[sourceFilePath] = 
					new Dictionary<string, List<DatasetRecord>>();
			}

			if (!Records[sourceFilePath].ContainsKey(targetFilePath))
			{
				Records[sourceFilePath][targetFilePath] = 
					new List<DatasetRecord>();
			}

			var existing = Records[sourceFilePath][targetFilePath]
				.Where(r => r.EntityType == entityType && r.SourceLine == sourceLine)
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
				SourceLine = sourceLine,
				TargetLine = targetLine
			});
		}

		public void Remove(
				string sourceFilePath,
				string targetFilePath,
				int sourceLine,
				int targetLine,
				string entityType
			)
		{
			if(Records.ContainsKey(sourceFilePath) 
				&& Records[sourceFilePath].ContainsKey(targetFilePath))
			{
				var elem = Records[sourceFilePath][targetFilePath]
					.FirstOrDefault(r => r.EntityType == entityType && r.SourceLine == sourceLine
						&& r.TargetLine == targetLine);

				if(elem != null)
				{
					Records[sourceFilePath][targetFilePath].Remove(elem);
				}
			}	
		}

		public void New()
		{
			Records = new Dictionary<string, Dictionary<string, List<DatasetRecord>>>();
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

					foreach (var targetFile in sourceFile.Value)
					{
						fs.WriteLine("**");

						fs.WriteLine(targetFile.Key);

						foreach(var record in targetFile.Value)
						{
							fs.WriteLine(record.ToString());
						}
					}
				}
			}
		}

		public static Dataset Load(string path)
		{
			var ds = new Dataset
			{
				SavingPath = path,
				Records = new Dictionary<string, Dictionary<string, List<DatasetRecord>>>()
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
					continue;
				}

				if (lines[i] == "**")
				{
					currentTargetFile = lines[++i];
					continue;
				}

				var record = DatasetRecord.FromString(lines[i]);

				ds.Add(
					currentSourceFile,
					currentTargetFile,
					record.SourceLine,
					record.TargetLine,
					record.EntityType,
					record.HasDoubts
				);
			}

			return ds;
		}
	}

	public class DatasetRecord
	{
		public int SourceLine { get; set; }
		public int TargetLine { get; set; }
		public bool HasDoubts { get; set; }
		public string EntityType { get; set; }

		public override string ToString()
		{
			return $"{SourceLine};{TargetLine};{EntityType};{HasDoubts}";
		}

		public static DatasetRecord FromString(string str)
		{
			var splitted = str.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

			return new DatasetRecord
			{
				SourceLine = int.Parse(splitted[0]),
				TargetLine = int.Parse(splitted[1]),
				EntityType = splitted[2],
				HasDoubts = bool.Parse(splitted[3])
			};
		}
	}
}