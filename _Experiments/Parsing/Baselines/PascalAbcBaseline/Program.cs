using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using PascalABCCompiler.PascalABCNewParser;
using PascalABCCompiler.SyntaxTree;

namespace PascalABCBaseline
{
	class Program
	{
		private static Encoding GetEncoding(string filename)
		{
			using (FileStream fs = File.OpenRead(filename))
			{
				Ude.CharsetDetector cdet = new Ude.CharsetDetector();
				cdet.Feed(fs);
				cdet.DataEnd();
				if (cdet.Charset != null)
				{
					return Encoding.GetEncoding(cdet.Charset);
				}
				else
				{
					return Encoding.Default;
				}
			}
		}

		static void Main(string[] args)
		{
			if (args.Length > 0)
			{
				var parser = new PascalABCNewLanguageParser();
				var package = new List<string>();

				if (args.Length > 1)
				{
					foreach (var path in args.Skip(1))
						package.AddRange(Directory.GetFiles(path, "*.pas", SearchOption.AllDirectories));
				}
				else
				{
					var landResultsFile = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\LanD Workspace\last_batch_parsing_report.txt";

					if (File.Exists(landResultsFile))
					{
						package.AddRange(Directory.GetFiles(
							File.ReadAllLines(landResultsFile)[1],
							"*.pas", SearchOption.AllDirectories));
					}
					else
					{
						Console.WriteLine("Не указаны каталоги для парсинга");
						return;
					}
				}

				var proceduresCounter = 0;
				var classesCounter = 0;

				var procedureOutput = new StreamWriter(Path.Combine(args[0], "routine_baseline.txt"), false);
				var classOutput = new StreamWriter(Path.Combine(args[0], "class_type_baseline.txt"), false);

				foreach (var filename in package)
				{
					var tree = parser.BuildTree(filename, File.ReadAllText(filename, GetEncoding(filename)), PascalABCCompiler.Parsers.ParseMode.Normal);

					if (tree != null)
					{
						var procedures = tree.DescendantNodes().OfType<procedure_definition>().ToList();
						if (procedures.Count > 0)
						{
							procedureOutput.WriteLine("*");
							procedureOutput.WriteLine(filename);

							foreach (var node in procedures)
								procedureOutput.WriteLine(node.proc_header.name);
						}

						proceduresCounter += procedures.Count();

						var classes = tree.DescendantNodes().OfType<class_definition>().ToList();
						if (classes.Count > 0)
						{
							classOutput.WriteLine("*");
							classOutput.WriteLine(filename);

							foreach (var node in classes)
							{
								if (node.Parent is type_declaration)
								{
									classOutput.WriteLine(((type_declaration)node.Parent).type_name);
								}
							}
						}

						classesCounter += classes.Count();
					}
					else
					{
						Console.WriteLine(filename);
						foreach (var error in parser.Errors)
						{
							Console.WriteLine(error.Message);
						}
					}
				}

				procedureOutput.Close();
				classOutput.Close();

				Console.WriteLine($"procedures: {proceduresCounter}");
				Console.WriteLine($"classes: {classesCounter}");
			}
		}
	}
}
