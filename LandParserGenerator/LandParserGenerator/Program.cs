using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CSharp;

using LandParserGenerator.Parsing.LL;

namespace LandParserGenerator
{
	public static class Builder
	{
		private static Type BuildLexer(Grammar grammar, string lexerName)
		{
			/// Генерируем по грамматике файл для ANTLR

			var grammarOutput = new StreamWriter($"{lexerName}.g4");

			grammarOutput.WriteLine($"lexer grammar {lexerName};");
			grammarOutput.WriteLine();
			grammarOutput.WriteLine(@"WS: [ \n\r\t]+ -> skip ;");

			foreach (var token in grammar.Tokens.Values)
			{
				/// На уровне лексера распознаём только лексемы для обычных токенов
				if (!grammar.SpecialTokens.Contains(token.Name))
				{
					/// Если токен служит только для описания других токенов - это fragment
					var isFragment = grammar.SkipTokens.Contains(token.Name) || grammar.Rules.SelectMany(r => r.Value.Alternatives).Any(a=>a.Contains(token.Name)) ?
						"" : "fragment ";
					grammarOutput.WriteLine($"{isFragment}{token.Name}: {token.Pattern} ;");
                }
			}

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

		public static Parser BuildYacc()
		{
			Grammar yaccGrammar = new Grammar();

			yaccGrammar.DeclareSpecialTokens("ERROR", "TEXT");

			/// Пропускаемые сущности
			yaccGrammar.DeclareTerminal(new TerminalSymbol("COMMENT_L", @"'//' ~[\n\r]*"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("COMMENT_ML", "'/*' .*? '*/'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("COMMENT", "COMMENT_L|COMMENT_ML"));

			yaccGrammar.DeclareTerminal(new TerminalSymbol("STRING_SKIP", "'\\\"' | '\\\\'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("STRING_STD", "'/*' .*? '*/'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("STRING_ESC", "'@\"' [^\"]* '\"'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("STRING", "STRING_STD|STRING_ESC"));

			yaccGrammar.DeclareTerminal(new TerminalSymbol("DECLARATION_CODE", "'%{' (STRING|COMMENT|.)*? '%}'"));

			yaccGrammar.SetSkipTokens("COMMENT", "STRING", "DECLARATION_CODE");

			/// Нужные штуки
			yaccGrammar.DeclareTerminal(new TerminalSymbol("BORDER", "'%%'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("DECLARATION_NAME", "'%' ID"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("CORNER_LEFT", "'<'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("ID", "[_a-zA-Z][_0-9a-zA-Z]*"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("CORNER_RIGHT", "'>'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("COLON", "':'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("SEMICOLON", "';'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("LITERAL", @"'\''(.|'\\\'')*?'\''"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("LBRACE", "'{'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("RBRACE", "'}'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("PIPE", "'|'"));


			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("grammar", new string[][]
			{
				new string[]{ "declarations", "BORDER", "rules", "grammar_ending" }
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("grammar_ending", new string[][]
			{
				new string[]{ "BORDER", "TEXT" },
				new string[]{ "BORDER" },
				new string[]{ }
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("declarations", new string[][]
			{
				new string[]{ "declaration", "declarations" },
				new string[]{ },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("declaration", new string[][]
			{
				new string[]{ "DECLARATION_NAME", "optional_type", "declaration_body" }
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("optional_type", new string[][]
			{
				new string[]{ "CORNER_LEFT", "ID", "CORNER_RIGHT" },
				new string[]{ }
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("declaration_body", new string[][]
			{
				new string[]{ "identifiers" },
				new string[]{ "TEXT" }
			}));


			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("identifiers", new string[][]
			{
				new string[]{ "ID", "identifiers_list" },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("identifiers_list", new string[][]
			{
				new string[]{ "ID", "identifiers_list" },
				new string[]{ }
			}));


			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("rules", new string[][]
			{
				new string[]{ "rule", "rules_list" },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("rules_list", new string[][]
			{
				new string[]{ "rule", "rules_list" },
				new string[]{ },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("rule", new string[][]
			{
				new string[]{ "ID", "COLON", "alternatives", "SEMICOLON" },
			}));


			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("alternatives", new string[][]
			{
				new string[]{ "alternative", "alternatives_list" },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("alternatives_list", new string[][]
			{
				new string[]{ "PIPE", "alternative", "alternatives_list" },
				new string[]{ },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("alternative", new string[][]
			{
				new string[]{ "alternative_component", "alternative" },
				new string[]{ }
			}));


			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("alternative_component", new string[][]
			{
				new string[]{ "ID" },
				new string[]{ "LBRACE", "TEXT", "RBRACE" },
				new string[] {"LITERAL" }
			}));

			yaccGrammar.SetStartSymbol("grammar");

			TableLL1 table = new TableLL1(yaccGrammar);
			table.ExportToCsv("yacc_table.csv");

			var lexerType = BuildLexer(yaccGrammar, "YaccGrammarLexer");

			/// Создаём парсер

			var parser = new Parser(yaccGrammar,
				new AntlrLexerAdapter(
					(Antlr4.Runtime.ICharStream stream) => (Antlr4.Runtime.Lexer)Activator.CreateInstance(lexerType, stream)
				)
			);

			return parser;
		}

		public static Parser BuildSharp()
		{
			Grammar sharpGrammar = new Grammar();

			sharpGrammar.DeclareSpecialTokens("ERROR", "TEXT");

			/// Пропускаемые сущности
			sharpGrammar.DeclareTerminal(new TerminalSymbol("COMMENT_L", @"'//' ~[\n\r]*"));
			sharpGrammar.DeclareTerminal(new TerminalSymbol("COMMENT_ML", "'/*' .*? '*/'"));
			sharpGrammar.DeclareTerminal(new TerminalSymbol("COMMENT", "COMMENT_L|COMMENT_ML"));

			sharpGrammar.DeclareTerminal(new TerminalSymbol("STRING_SKIP", "'\\\"' | '\\\\'"));
			sharpGrammar.DeclareTerminal(new TerminalSymbol("STRING_STD", "'/*' .*? '*/'"));
			sharpGrammar.DeclareTerminal(new TerminalSymbol("STRING_ESC", "'@\"' [^\"]* '\"'"));
			sharpGrammar.DeclareTerminal(new TerminalSymbol("STRING", "STRING_STD|STRING_ESC"));

			sharpGrammar.SetSkipTokens("COMMENT", "STRING");

			/// Нужные штуки
			sharpGrammar.DeclareTerminal(new TerminalSymbol("NAMESPACE", "'namespace'"));
			sharpGrammar.DeclareTerminal(new TerminalSymbol("CLASS", "'class'"));
			sharpGrammar.DeclareTerminal(new TerminalSymbol("ID", "'@'?[_a-zA-Z][_0-9a-zA-Z]*"));

			sharpGrammar.DeclareTerminal(new TerminalSymbol("LCBRACE", "'{'"));
			sharpGrammar.DeclareTerminal(new TerminalSymbol("DOT", "'.'"));
			sharpGrammar.DeclareTerminal(new TerminalSymbol("RCBRACE", "'}'"));
			sharpGrammar.DeclareTerminal(new TerminalSymbol("LBRACE", "'('"));
			sharpGrammar.DeclareTerminal(new TerminalSymbol("RBRACE", "')'"));
			

			sharpGrammar.DeclareNonterminal(new NonterminalSymbol("program", new string[][]
			{
				new string[]{ "TEXT", "NAMESPACE", "full_name", "LCBRACE", "namespace_content", "RCBRACE" }
			}));

			sharpGrammar.DeclareNonterminal(new NonterminalSymbol("namespace_content", new string[][]
			{
				new string[]{ "class", "class_list" },
			}));

			sharpGrammar.DeclareNonterminal(new NonterminalSymbol("class_list", new string[][]
			{
				new string[]{ "class", "class_list" },
				new string[]{ }
			}));

			sharpGrammar.DeclareNonterminal(new NonterminalSymbol("class", new string[][]
			{
				new string[]{ "TEXT", "CLASS", "full_name", "TEXT", "LCBRACE", "TEXT", "RCBRACE" }
			}));

			sharpGrammar.DeclareNonterminal(new NonterminalSymbol("full_name", new string[][]
			{
				new string[]{ "ID", "full_name_list" },
			}));

			sharpGrammar.DeclareNonterminal(new NonterminalSymbol("full_name_list", new string[][]
			{
				new string[]{ "DOT", "ID", "identifiers_list" },
				new string[]{ }
			}));

			sharpGrammar.DeclareNonterminal(new NonterminalSymbol("identifiers", new string[][]
			{
				new string[]{ "ID", "identifiers_list" },
			}));

			sharpGrammar.DeclareNonterminal(new NonterminalSymbol("identifiers_list", new string[][]
			{
				new string[]{ "ID", "identifiers_list" },
				new string[]{ }
			}));


			sharpGrammar.SetStartSymbol("program");

			TableLL1 table = new TableLL1(sharpGrammar);
			table.ExportToCsv("sharp_table.csv");

			var lexerType = BuildLexer(sharpGrammar, "SharpGrammarLexer");

			/// Создаём парсер

			var parser = new Parser(sharpGrammar,
				new AntlrLexerAdapter(
					(Antlr4.Runtime.ICharStream stream) => (Antlr4.Runtime.Lexer)Activator.CreateInstance(lexerType, stream)
				)
			);

			return parser;
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
			TableLL1 table = new TableLL1(exprGrammar);
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
	}
}
