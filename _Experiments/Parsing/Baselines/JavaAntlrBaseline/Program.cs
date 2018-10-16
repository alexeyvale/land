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
		public int FieldCounter { get; private set; } = 0;

		private StreamWriter MethodOutput { get; set; }
		private StreamWriter ClassOutput { get; set; }
		private StreamWriter EnumOutput { get; set; }
		private StreamWriter FieldOutput { get; set; }

		private List<string> Methods { get; set; }
		private List<string> ClassesInterfaces { get; set; }
		private List<string> Enums { get; set; }
		private List<string> Fields { get; set; }

		private string FileName { get; set; }

		private bool InField { get; set; } = false;

		public JavaTreeVisitor(string outputDirectory)
		{
			MethodOutput = new StreamWriter(Path.Combine(outputDirectory, "method_baseline.txt"), false);
			ClassOutput = new StreamWriter(Path.Combine(outputDirectory, "class_interface_baseline.txt"), false);
			EnumOutput = new StreamWriter(Path.Combine(outputDirectory, "enum_baseline.txt"), false);
			FieldOutput = new StreamWriter(Path.Combine(outputDirectory, "field_baseline.txt"), false);
		}

		public void CloseOutputs()
		{
			MethodOutput.Close();
			ClassOutput.Close();
			EnumOutput.Close();
			FieldOutput.Close();
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
			Fields = new List<string>();
			InField = false;

			base.VisitCompilationUnit(context);

			if(Methods.Count > 0)
			{
				MethodOutput.WriteLine("*");
				MethodOutput.WriteLine(FileName);
				Methods.ForEach(line => MethodOutput.WriteLine(line));
			}

			if (ClassesInterfaces.Count > 0)
			{
				ClassOutput.WriteLine("*");
				ClassOutput.WriteLine(FileName);
				ClassesInterfaces.ForEach(line => ClassOutput.WriteLine(line));
			}

			if (Enums.Count > 0)
			{
				EnumOutput.WriteLine("*");
				EnumOutput.WriteLine(FileName);
				Enums.ForEach(line => EnumOutput.WriteLine(line));
			}

			if (Fields.Count > 0)
			{
				FieldOutput.WriteLine("*");
				FieldOutput.WriteLine(FileName);
				Fields.ForEach(line => FieldOutput.WriteLine(line));
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

		public override bool VisitFieldDeclaration([NotNull] JavaParser.FieldDeclarationContext context)
		{
			InField = true;
			base.VisitChildren(context);
			InField = false;

			return true;
		}

		public override bool VisitVariableDeclaratorId([NotNull] JavaParser.VariableDeclaratorIdContext context)
		{
			if(InField)
			{
				++FieldCounter;
				Fields.Add(context.IDENTIFIER().GetText());
			}

			return true;
		}

		public override bool VisitMethodDeclaration([NotNull] JavaParser.MethodDeclarationContext context)
		{
			++MethodCounter;
			Methods.Add(context.IDENTIFIER().GetText());

			return true;
		}

		public override bool VisitInterfaceMethodDeclaration([NotNull] JavaParser.InterfaceMethodDeclarationContext context)
		{
			++MethodCounter;
			Methods.Add(context.IDENTIFIER().GetText());

			return true;
		}

		public override bool VisitAnnotationTypeDeclaration([NotNull] JavaParser.AnnotationTypeDeclarationContext context)
		{
			return true;
		}

		public override bool VisitBlock([NotNull] JavaParser.BlockContext context)
		{
			return true;
		}

		public override bool VisitVariableInitializer([NotNull] JavaParser.VariableInitializerContext context)
		{
			return true;
		}
	}

	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length > 0)
			{			
				var visitor = new JavaTreeVisitor(args[0]);
				var package = new List<string>();

				if (args.Length > 1)
				{
					foreach (var path in args.Skip(1))
						package.AddRange(Directory.GetFiles(path, "*.java", SearchOption.AllDirectories));
				}
				else
				{
					var landResultsFile = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\LanD Workspace\last_batch_parsing_report.txt";

					if (File.Exists(landResultsFile))
					{
						package.AddRange(Directory.GetFiles(
							File.ReadAllLines(landResultsFile)[1],
							"*.java", SearchOption.AllDirectories));
					}
					else
					{
						Console.WriteLine("Не указаны каталоги для парсинга");
						return;
					}
				}

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
				Console.WriteLine($"fields: {visitor.FieldCounter}");
			}
		}
	}
}
