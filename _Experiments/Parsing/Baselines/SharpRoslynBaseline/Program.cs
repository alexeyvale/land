using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynParserTest
{
	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length > 0)
			{
				var package = new List<string>();

				if (args.Length > 1)
				{
					foreach (var path in args.Skip(1))
						package.AddRange(Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories));
				}
				else
				{
					var landResultsFile = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\LanD Workspace\last_batch_parsing_report.txt";

					if (File.Exists(landResultsFile))
					{
						package.AddRange(Directory.GetFiles(
							File.ReadAllLines(landResultsFile)[1],
							"*.cs", SearchOption.AllDirectories));
					}
					else
					{
						Console.WriteLine("Не указаны каталоги для парсинга");
						return;
					}
				}

				var classesCounter = 0;
				var enumsCounter = 0;
				var propertiesCounter = 0;
				var methodsCounter = 0;
				var fieldsCounter = 0;

				var enumOutput = new StreamWriter(Path.Combine(args[0], "enum_baseline.txt"), false);
				var propertyOutput = new StreamWriter(Path.Combine(args[0], "property_baseline.txt"), false);
				var fieldOutput = new StreamWriter(Path.Combine(args[0], "field_baseline.txt"), false);
				var methodOutput = new StreamWriter(Path.Combine(args[0], "method_baseline.txt"), false);

				foreach (var filename in package)
				{
					var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filename), new CSharpParseOptions());

					var enums = tree.GetRoot().DescendantNodes().OfType<EnumDeclarationSyntax>().ToList();
					if (enums.Count > 0)
					{
						enumOutput.WriteLine("*");
						enumOutput.WriteLine(filename);

						foreach (var node in enums)
							enumOutput.WriteLine(node.Identifier);
					}

					var fields = tree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
					if (fields.Count > 0)
					{
						fieldOutput.WriteLine("*");
						fieldOutput.WriteLine(filename);

						foreach (var node in fields)
							foreach (var variable in node.Declaration.Variables)
								fieldOutput.WriteLine(variable.Identifier);
					}

					var properties = tree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>().ToList();
					if (properties.Count > 0)
					{
						propertyOutput.WriteLine("*");
						propertyOutput.WriteLine(filename);

						foreach (var node in properties)
							propertyOutput.WriteLine(node.Identifier);
					}

					var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
					if (methods.Count > 0)
					{
						methodOutput.WriteLine("*");
						methodOutput.WriteLine(filename);

						foreach (var node in methods)
							methodOutput.WriteLine(node.Identifier);
					}

					enumsCounter += enums.Count();
					classesCounter += tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Count()
						+ tree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>().Count()
						+ tree.GetRoot().DescendantNodes().OfType<InterfaceDeclarationSyntax>().Count();
					fieldsCounter += fields.Sum(node => node.Declaration.Variables.Count);
					propertiesCounter += properties.Count();
					methodsCounter += methods.Count();
				}

				enumOutput.Close();
				propertyOutput.Close();
				methodOutput.Close();
				fieldOutput.Close();

				Console.WriteLine($"enums: {enumsCounter}");
				Console.WriteLine($"classes: {classesCounter}");
				Console.WriteLine($"fields: {fieldsCounter}");
				Console.WriteLine($"properties: {propertiesCounter}");
				Console.WriteLine($"methods: {methodsCounter}");
			}
		}
	}
}
