using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using PascalABCCompiler.PascalABCNewParser;
using PascalABCCompiler.SyntaxTree;

namespace PascalABCProvidedBaseline
{
	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length > 0)
			{
				var parser = new PascalABCNewLanguageParser();
				var package = new List<string>();

				foreach (var path in args)
					package.AddRange(Directory.GetFiles(path, "*.pas", SearchOption.AllDirectories));

				var proceduresCounter = 0;
				var classesCounter = 0;

				var procedureOutput = new StreamWriter("_pascalabcProcedureOutput.txt", false);
				var classOutput = new StreamWriter("_pascalabcClassOutput.txt", false);

				foreach (var filename in package)
				{
					var tree = parser.BuildTree(filename, File.ReadAllText(filename), PascalABCCompiler.Parsers.ParseMode.Normal);

					var procedures = tree.DescendantNodes().OfType<procedure_definition>().ToList();
					if (procedures.Count > 0)
					{
						procedureOutput.WriteLine(filename);

						foreach (var node in procedures)
							procedureOutput.WriteLine(node.proc_header.name);
					}

					proceduresCounter += procedures.Count();
				}

				procedureOutput.Close();
				classOutput.Close();

				Console.WriteLine($"procedures: {proceduresCounter}");
				Console.WriteLine($"classes: {classesCounter}");

				Console.ReadLine();
			}
		}
	}
}
