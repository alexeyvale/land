using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.LL;

namespace LandParserGenerator
{
	class Program
	{
		static void BuildYacc()
		{
			Grammar yaccGrammar = new Grammar();

			yaccGrammar.DeclareTerminal(new TerminalSymbol("BORDER", "%%"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("DECLARATION_NAME", null));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("CORNER_LEFT", "<"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("ID", null));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("CORNER_RIGHT", ">"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("RULE_NAME", null));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("COLON", ":"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("SEMICOLON", ";"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("LITERAL", null));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("LBRACE", "{"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("RBRACE", "}"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("PIPE", "|"));


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
				new string[]{ "ID", "identifiers" },
				new string[]{ "ID" }
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("rules", new string[][]
			{
				new string[]{ "rule", "rules" },
				new string[]{ "rule" }
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("rule", new string[][]
			{
				new string[]{ "RULE_NAME", "COLON", "alternatives", "SEMICOLON" },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("alternatives", new string[][]
			{
				new string[]{ "alternative", "PIPE", "alternatives" },
				new string[]{ "alternative" }
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
		}

		static void BuildExpressionGrammar()
		{
			/// Формируем грамматику

			Grammar exprGrammar = new Grammar();

			exprGrammar.DeclareTerminal(new TerminalSymbol("PLUS", "'+'"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("MULT", "'*'"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("LPAR", "'('"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("RPAR", "')'"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("ID", "[_a-zA-Z][_0-9a-zA-Z]"));

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

			/// Генерируем по грамматике файл для ANTLR

			var lexerGrammarOutput = new StreamWriter("ExpressionGrammarLexer.g4");

			lexerGrammarOutput.WriteLine("lexer grammar ExpressionGrammarLexer;");

			foreach(var token in exprGrammar.Tokens.Values)
			{
				if(token.Name != Grammar.EofTokenName)
					lexerGrammarOutput.WriteLine($"{token.Name}: {token.Pattern} ;");
			}

			lexerGrammarOutput.Close();

			/// Запускаем ANTLR и получаем файл лексера

			Process process = new Process();
			ProcessStartInfo startInfo = new ProcessStartInfo()
			{
				FileName = "cmd.exe",
				Arguments = "/C java -jar \"../../Components/Antlr/antlr-4.7-complete.jar\" -Dlanguage=CSharp ExpressionGrammarLexer.g4",
				WindowStyle = ProcessWindowStyle.Hidden
			};
			process.StartInfo = startInfo;
			process.Start();

			while(!process.HasExited)
			{
				System.Threading.Thread.Sleep(0);
			}

			/// Перемещаем файл в каталог проекта

			File.Copy("ExpressionGrammarLexer.cs", "../../ExpressionGrammarLexer.cs", true);

			/// Создаём парсер

			var parser = new Parser(exprGrammar,
				new AntlrLexerAdapter<ExpressionGrammarLexer>(
					(Antlr4.Runtime.ICharStream stream) => new ExpressionGrammarLexer(stream)
				)
			);
		}

		static void Main(string[] args)
		{
			BuildExpressionGrammar();

			Console.ReadLine();
		}
	}
}
