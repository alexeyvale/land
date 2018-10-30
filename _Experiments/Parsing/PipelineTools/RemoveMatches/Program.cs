using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace RemoveMatches
{
	class Program
	{
		static void Main(string[] args)
		{
			if(args.Length > 0)
			{
				var files = Directory.GetFiles(args[0]).GroupBy(f=>f.Substring(0, f.LastIndexOf('_'))).ToList();

				/// Проходим по парам файлов для всех возможных типов сущностей
				foreach (var pair in files)
				{
					if (pair.Count() == 2)
					{
						/// Отчёты для некоторого типа сущностей, разбитые на куски,
						/// соответствующие отдельным проанализированным файлам
						var landReport = File.ReadAllText(pair.ElementAt(0))
							.Split(new char[] { '*' }, StringSplitOptions.RemoveEmptyEntries)
							.Select(r => r.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
							.Select(splitted => new Tuple<string, List<string>>(splitted.First(), splitted.Skip(1).OrderBy(e => e).ToList()))
							.OrderBy(e => e.Item1)
							.ToList();
						var baselineReport = File.ReadAllText(pair.ElementAt(1))
							.Split(new char[] { '*' }, StringSplitOptions.RemoveEmptyEntries)
							.Select(r => r.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
							.Select(splitted => new Tuple<string, List<string>>(splitted.First(), splitted.Skip(1).OrderBy(e => e).ToList()))
							.OrderBy(e => e.Item1)
							.ToList();

						int landIndex = 0, baselineIndex = 0;

						while (landIndex < landReport.Count && baselineIndex < baselineReport.Count)
						{
							if (landReport[landIndex].Item1 == baselineReport[baselineIndex].Item1)
							{
								int landFileIndex = 0, baselineFileIndex = 0;

								while (landFileIndex < landReport[landIndex].Item2.Count
									&& baselineFileIndex < baselineReport[baselineIndex].Item2.Count)
								{
									switch (landReport[landIndex].Item2[landFileIndex]
										.CompareTo(baselineReport[baselineIndex].Item2[baselineFileIndex]))
									{
										case 0:
											landReport[landIndex].Item2.RemoveAt(landFileIndex);
											baselineReport[baselineIndex].Item2.RemoveAt(baselineFileIndex);
											break;
										case 1:
											baselineFileIndex++;
											break;
										case -1:
											landFileIndex++;
											break;
									}
								}

								if (landReport[landIndex].Item2.Count == 0 && baselineReport[baselineIndex].Item2.Count == 0)
								{
									landReport.RemoveAt(landIndex);
									baselineReport.RemoveAt(baselineIndex);
								}
								else
								{
									landIndex++;
									baselineIndex++;
								}
							}
							else if (String.Compare(landReport[landIndex].Item1, baselineReport[baselineIndex].Item1) == 1)
							{
								landReport.Insert(landIndex, new Tuple<string, List<string>>(baselineReport[baselineIndex].Item1, new List<string>()));
								baselineIndex++;
								landIndex++;
							}
							else
							{
								baselineReport.Insert(baselineIndex, new Tuple<string, List<string>>(landReport[landIndex].Item1, new List<string>()));
								baselineIndex++;
								landIndex++;
							}
						}

						using (var fs = new StreamWriter(pair.ElementAt(0), false))
						{
							foreach(var tuple in landReport)
							{
								fs.WriteLine(tuple.Item1);
								foreach (var elem in tuple.Item2)
									fs.WriteLine(elem);
							}
						}

						using (var fs = new StreamWriter(pair.ElementAt(1), false))
						{
							foreach (var tuple in baselineReport)
							{
								fs.WriteLine(tuple.Item1);
								foreach (var elem in tuple.Item2)
									fs.WriteLine(elem);
							}
						}
					}
				}
			}
		}
	}
}
