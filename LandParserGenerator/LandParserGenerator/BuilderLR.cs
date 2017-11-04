using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CSharp;

using LandParserGenerator.Parsing.LR;

namespace LandParserGenerator
{
	public static class BuilderLR
	{
		private static Type BuildLexer(Grammar grammar, string lexerName)
		{
			/// Генерируем по грамматике файл для ANTLR

			var grammarOutput = new StreamWriter($"{lexerName}.g4");

			grammarOutput.WriteLine($"lexer grammar {lexerName};");
			grammarOutput.WriteLine();
			grammarOutput.WriteLine(@"WS: [ \n\r\t]+ -> skip ;");

			foreach (var token in grammar.Tokens.Values.Where(v => !String.IsNullOrEmpty(v.Pattern)))
			{
				/// На уровне лексера распознаём только лексемы для обычных токенов
				if (!grammar.SpecialTokens.Contains(token.Name))
				{
					/// Если токен служит только для описания других токенов - это fragment
					var isFragment = grammar.SkipTokens.Contains(token.Name) || grammar.Rules.SelectMany(r => r.Value.Alternatives).Any(a => a.Contains(token.Name)) ?
						"" : "fragment ";
					grammarOutput.WriteLine($"{isFragment}{token.Name}: {token.Pattern} ;");
				}
			}

			grammarOutput.WriteLine(@"UNDEFINED: . -> skip ;");

			grammarOutput.Close();

			/// Запускаем ANTLR и получаем файл лексера

			Process process = new Process();
			ProcessStartInfo startInfo = new ProcessStartInfo()
			{
				FileName = "cmd.exe",
				Arguments = $"/C java -jar \"../../../components/Antlr/antlr-4.7-complete.jar\" -Dlanguage=CSharp {lexerName}.g4",
				WindowStyle = ProcessWindowStyle.Hidden
			};
			process.StartInfo = startInfo;
			process.Start();

			while (!process.HasExited)
			{
				System.Threading.Thread.Sleep(0);
			}

			/// Компилируем .cs-файл лексера

			var codeProvider = new CSharpCodeProvider();

			var compilerParams = new System.CodeDom.Compiler.CompilerParameters();
			compilerParams.GenerateInMemory = true;
			compilerParams.ReferencedAssemblies.Add("Antlr4.Runtime.Standard.dll");
			compilerParams.ReferencedAssemblies.Add("System.dll");

			var compilationResult = codeProvider.CompileAssemblyFromFile(compilerParams, $"{lexerName}.cs");
			return compilationResult.CompiledAssembly.GetType(lexerName);
		}

		public static Parser BuildExpressionGrammar()
		{
			/// Формируем грамматику

			Grammar exprGrammar = new Grammar();

			exprGrammar.DeclareSpecialTokens("ERROR", "TEXT");

			exprGrammar.DeclareTerminal(new TerminalSymbol("PLUS", "'+'"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("MULT", "'*'"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("LPAR", "'('"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("RPAR", "')'"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("ID", "[_a-zA-Z][_0-9a-zA-Z]*"));

			exprGrammar.DeclareNonterminal(new NonterminalSymbol("E", new string[][]
			{
				new string[]{ "T", "E'" }
			}));

			exprGrammar.DeclareNonterminal(new NonterminalSymbol("E'", new string[][]
			{
				new string[]{ "PLUS", "T","E'" },
				new string[]{ }
			}));

			exprGrammar.DeclareNonterminal(new NonterminalSymbol("T", new string[][]
			{
				new string[]{ "F", "T'" },
			}));

			exprGrammar.DeclareNonterminal(new NonterminalSymbol("T'", new string[][]
			{
				new string[]{ "MULT", "F","T'" },
				new string[]{ }
			}));

			exprGrammar.DeclareNonterminal(new NonterminalSymbol("F", new string[][]
			{
				new string[]{ "LPAR", "E","RPAR" },
				new string[]{ "ID" }
			}));

			exprGrammar.SetStartSymbol("E");

			/// Строим таблицу парсинга
			TableLR1 table = new TableLR1(exprGrammar);
			table.ExportToCsv("expr_table.csv");

			/// Получаем тип лексера
			var lexerType = BuildLexer(exprGrammar, "ExpressionGrammarLexer");

			/// Создаём парсер
			var parser = new Parser(exprGrammar,
				new AntlrLexerAdapter(
					(Antlr4.Runtime.ICharStream stream) => (Antlr4.Runtime.Lexer)Activator.CreateInstance(lexerType, stream)
				)
			);

			return parser;
		}

		public static Parser BuildTestCase()
		{
			/// Формируем грамматику

			Grammar exprGrammar = new Grammar();

			exprGrammar.DeclareTerminal(new TerminalSymbol("C", "'c'"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("D", "'d'"));

			exprGrammar.DeclareNonterminal(new NonterminalSymbol("s", new string[][]
			{
				new string[]{ "c", "c" }
			}));

			exprGrammar.DeclareNonterminal(new NonterminalSymbol("c", new string[][]
			{
				new string[]{ "C", "c" },
				new string[] { "D" }
			}));

			exprGrammar.SetStartSymbol("s");

			/// Строим таблицу парсинга
			TableLR1 table = new TableLR1(exprGrammar);
			table.ExportToCsv("test_table.csv");

			/// Получаем тип лексера
			var lexerType = BuildLexer(exprGrammar, "TestGrammarLexer");

			/// Создаём парсер
			var parser = new Parser(exprGrammar,
				new AntlrLexerAdapter(
					(Antlr4.Runtime.ICharStream stream) => (Antlr4.Runtime.Lexer)Activator.CreateInstance(lexerType, stream)
				)
			);

			return parser;
		}
	}
}
