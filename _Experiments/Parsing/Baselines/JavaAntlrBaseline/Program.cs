using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;

namespace JavaAntlrBaseline
{
	public class JavaTreeVisitor: JavaParserBaseVisitor<bool>
	{
		public int MethodCounter { get; private set; } = 0;
		public int ClassInterfaceCounter { get; private set; } = 0;
		public int EnumCounter { get; private set; } = 0;

		private StreamWriter MethodOutput { get; set; } = new StreamWriter("_javaMethodOutput.txt", false);
		private StreamWriter ClassOutput { get; set; } = new StreamWriter("_javaClassOutput.txt", false);
		private StreamWriter EnumOutput { get; set; } = new StreamWriter("_javaEnumOutput.txt", false);

		private List<string> Methods { get; set; }
		private List<string> ClassesInterfaces { get; set; }
		private List<string> Enums { get; set; }

		private string FileName { get; set; }

		public void CloseOutputs()
		{
			MethodOutput.Close();
			ClassOutput.Close();
			EnumOutput.Close();
		}

		public void SetFile(string filename)
		{
			FileName = filename;
		}

		public override bool VisitCompilationUnit([NotNull] JavaParser.CompilationUnitContext context)
		{
			Methods = new List<string>();
			ClassesInterfaces = new List<string>();
			Enums = new List<string>();

			base.VisitCompilationUnit(context);

			if(Methods.Count > 0)
			{
				MethodOutput.WriteLine(FileName);
				Methods.ForEach(line => MethodOutput.WriteLine(line));
			}

			if (ClassesInterfaces.Count > 0)
			{
				ClassOutput.WriteLine(FileName);
				ClassesInterfaces.ForEach(line => ClassOutput.WriteLine(line));
			}

			if (Enums.Count > 0)
			{
				EnumOutput.WriteLine(FileName);
				Enums.ForEach(line => EnumOutput.WriteLine(line));
			}

			return true;
		}

		public override bool VisitClassDeclaration([NotNull] JavaParser.ClassDeclarationContext context)
		{
			++ClassInterfaceCounter;
			ClassesInterfaces.Add(context.IDENTIFIER().GetText());

			return base.VisitChildren(context);
		}

		public override bool VisitInterfaceDeclaration([NotNull] JavaParser.InterfaceDeclarationContext context)
		{
			++ClassInterfaceCounter;
			ClassesInterfaces.Add(context.IDENTIFIER().GetText());

			return base.VisitChildren(context);
		}

		public override bool VisitEnumDeclaration([NotNull] JavaParser.EnumDeclarationContext context)
		{
			++EnumCounter;
			Enums.Add(context.IDENTIFIER().GetText());

			return true;
		}

		public override bool VisitMethodDeclaration([NotNull] JavaParser.MethodDeclarationContext context)
		{
			++MethodCounter;
			Methods.Add(context.IDENTIFIER().GetText());

			return true;
		}
	}

	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length > 0)
			{
				var visitor = new JavaTreeVisitor();
				var package = new List<string>();

				foreach (var path in args)
					package.AddRange(Directory.GetFiles(path, "*.java", SearchOption.AllDirectories));

				foreach (var filename in package)
				{
					var inputStream = new AntlrInputStream(File.ReadAllText(filename));
					var lexer = new JavaLexer(inputStream);
					var parser = new JavaParser(new CommonTokenStream(lexer));

					/// Запускаем парсинг
					var context = parser.compilationUnit();
					visitor.SetFile(filename);
					context.Accept(visitor);
				}

				visitor.CloseOutputs();

				Console.WriteLine($"methods: {visitor.MethodCounter}");
				Console.WriteLine($"classes: {visitor.ClassInterfaceCounter}");
				Console.WriteLine($"enums: {visitor.EnumCounter}");

				Console.ReadLine();
			}
		}
	}
}
