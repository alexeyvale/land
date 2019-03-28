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

				var classOutput = new StreamWriter(Path.Combine(args[0], "class_struct_interface_baseline.txt"), false);
				var enumOutput = new StreamWriter(Path.Combine(args[0], "enum_baseline.txt"), false);
				var propertyOutput = new StreamWriter(Path.Combine(args[0], "property_baseline.txt"), false);
				var fieldOutput = new StreamWriter(Path.Combine(args[0], "field_baseline.txt"), false);
				var methodOutput = new StreamWriter(Path.Combine(args[0], "method_baseline.txt"), false);

				var totalTime = new TimeSpan();

				foreach (var filename in package)
				{
					var startTime = DateTime.Now;
					var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filename), new CSharpParseOptions());
					totalTime += DateTime.Now - startTime;


					var enums = tree.GetRoot().DescendantNodes().OfType<EnumDeclarationSyntax>()
						.Where(e=>!e.Identifier.IsMissing).ToList();
					if (enums.Count > 0)
					{
						enumOutput.WriteLine("***");
						enumOutput.WriteLine(filename);

						foreach (var node in enums)
							enumOutput.WriteLine(node.Identifier);
					}

					var fields = tree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>()
						.SelectMany(f=>f.Declaration.Variables)
						.Where(e => !e.Identifier.IsMissing).ToList();
					if (fields.Count > 0)
					{
						fieldOutput.WriteLine("***");
						fieldOutput.WriteLine(filename);

						foreach (var node in fields)
							fieldOutput.WriteLine(node.Identifier);
					}

					var properties = tree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>()
						.Where(e => !e.Identifier.IsMissing).ToList();
					if (properties.Count > 0)
					{
						propertyOutput.WriteLine("***");
						propertyOutput.WriteLine(filename);

						foreach (var node in properties)
							propertyOutput.WriteLine(node.Identifier);
					}

					var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
						.Where(e => !e.Identifier.IsMissing).ToList();
					if (methods.Count > 0)
					{
						methodOutput.WriteLine("***");
						methodOutput.WriteLine(filename);

						foreach (var node in methods)
							methodOutput.WriteLine(node.Identifier);
					}

					var classes = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
						.Where(e => !e.Identifier.IsMissing).ToList();
					var structs = tree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>()
						.Where(e => !e.Identifier.IsMissing).ToList();
					var interfaces = tree.GetRoot().DescendantNodes().OfType<InterfaceDeclarationSyntax>()
						.Where(e => !e.Identifier.IsMissing).ToList();

					if (classes.Count > 0 || structs.Count > 0 || interfaces.Count > 0)
					{
						classOutput.WriteLine("***");
						classOutput.WriteLine(filename);

						foreach (var node in classes)
							classOutput.WriteLine(node.Identifier);
						foreach (var node in structs)
							classOutput.WriteLine(node.Identifier);
						foreach (var node in interfaces)
							classOutput.WriteLine(node.Identifier);
					}

					enumsCounter += enums.Count();
					classesCounter += classes.Count() + structs.Count() + interfaces.Count();
					fieldsCounter += fields.Count();
					propertiesCounter += properties.Count();
					methodsCounter += methods.Count();
				}

				enumOutput.Close();
				propertyOutput.Close();
				methodOutput.Close();
				fieldOutput.Close();
				classOutput.Close();

				Console.WriteLine($"enums: {enumsCounter}");
				Console.WriteLine($"classes: {classesCounter}");
				Console.WriteLine($"fields: {fieldsCounter}");
				Console.WriteLine($"properties: {propertiesCounter}");
				Console.WriteLine($"methods: {methodsCounter}");

				Console.WriteLine(totalTime.ToString(@"hh\:mm\:ss\:ff"));
			}
		}
	}
}
