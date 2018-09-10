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

				foreach (var path in args)
					package.AddRange(Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories));

				var classesCounter = 0;
				var enumsCounter = 0;
				var propertiesCounter = 0;
				var methodsCounter = 0;
				var fieldsCounter = 0;

				var enumOutput = new StreamWriter("_roslynCSharpEnumOutput.txt", false);
				var propertyOutput = new StreamWriter("_roslynCSharpPropertyOutput.txt", false);
				var fieldOutput = new StreamWriter("_roslynCSharpFieldOutput.txt", false);
				var methodOutput = new StreamWriter("_roslynCSharpMethodOutput.txt", false);

				foreach (var filename in package)
				{
					var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filename), new CSharpParseOptions());

					var enums = tree.GetRoot().DescendantNodes().OfType<EnumDeclarationSyntax>().ToList();
					if (enums.Count > 0)
					{
						enumOutput.WriteLine(filename);

						foreach (var node in enums)
							enumOutput.WriteLine(node.Identifier);
					}

					var fields = tree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();
					if (fields.Count > 0)
					{
						fieldOutput.WriteLine(filename);

						foreach (var node in fields)
							foreach (var variable in node.Declaration.Variables)
								fieldOutput.WriteLine(variable.Identifier);
					}

					var properties = tree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>().ToList();
					if (properties.Count > 0)
					{
						propertyOutput.WriteLine(filename);

						foreach (var node in properties)
							propertyOutput.WriteLine(node.Identifier);
					}

					var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
					if (methods.Count > 0)
					{
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

				Console.ReadLine();
			}
		}
	}
}
