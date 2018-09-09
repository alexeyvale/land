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

				var enumOutput = new StreamWriter("_enumOutput.txt", false);
				var propertyOutput = new StreamWriter("_propertyOutput.txt", false);
				var fieldOutput = new StreamWriter("_fieldOutput.txt", false);
				var methodOutput = new StreamWriter("_methodOutput.txt", false);

				foreach (var filename in package)
				{
					var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(filename), new CSharpParseOptions());

					foreach (var node in tree.GetRoot().DescendantNodes().OfType<EnumDeclarationSyntax>())
						enumOutput.WriteLine(node.Identifier);

					foreach (var node in tree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>())
						foreach (var variable in node.Declaration.Variables)
							fieldOutput.WriteLine(variable.Identifier);

					foreach (var node in tree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>())
						propertyOutput.WriteLine(node.Identifier);

					foreach (var node in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
						methodOutput.WriteLine(node.Identifier);

					enumsCounter += tree.GetRoot().DescendantNodes().OfType<EnumDeclarationSyntax>().Count();
					classesCounter += tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Count()
						+ tree.GetRoot().DescendantNodes().OfType<StructDeclarationSyntax>().Count()
						+ tree.GetRoot().DescendantNodes().OfType<InterfaceDeclarationSyntax>().Count();
					fieldsCounter += tree.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>().Sum(node => node.Declaration.Variables.Count);
					propertiesCounter += tree.GetRoot().DescendantNodes().OfType<PropertyDeclarationSyntax>().Count();
					methodsCounter += tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Count();
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
