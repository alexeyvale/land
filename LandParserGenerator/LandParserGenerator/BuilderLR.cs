using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CSharp;

using Land.Core.Parsing.LR;

namespace Land.Core
{
	public static class BuilderLR
	{
		public static Parser BuildParser(string text, List<Message> errors)
		{
			return BuilderBase.BuildParser(GrammarType.LR, text, errors) as Parser;
		}

		public static Parser BuildExpressionGrammar()
		{
			/// Формируем грамматику

			Grammar exprGrammar = new Grammar(GrammarType.LR);

			exprGrammar.DeclareTerminal(new TerminalSymbol("PLUS", "'+'"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("MULT", "'*'"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("LPAR", "'('"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("RPAR", "')'"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("ID", "[_a-zA-Z][_0-9a-zA-Z]*"));

			exprGrammar.DeclareNonterminal(new NonterminalSymbol("E", new string[][]
			{
				new string[]{ "E", "PLUS", "T" },
				new string[]{ "T" }
			}));

			exprGrammar.DeclareNonterminal(new NonterminalSymbol("T", new string[][]
			{
				new string[]{ "T", "MULT", "F" },
				new string[]{ "F" }
			}));

			exprGrammar.DeclareNonterminal(new NonterminalSymbol("F", new string[][]
			{
				new string[]{ "LPAR", "E", "RPAR" },
				new string[]{ "ID" }
			}));

			exprGrammar.SetOption(ParsingOption.START, "E'");

			exprGrammar.PostProcessing();

			/// Строим таблицу парсинга
			TableLR1 table = new TableLR1(exprGrammar);
			table.ExportToCsv("expr_table.csv");

			/// Получаем тип лексера
			var lexerType = BuilderBase.BuildLexer(exprGrammar, "ExpressionGrammarLexer");

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

			Grammar exprGrammar = new Grammar(GrammarType.LR);

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

			exprGrammar.SetOption(ParsingOption.START, "s'");

			exprGrammar.PostProcessing();

			/// Строим таблицу парсинга
			TableLR1 table = new TableLR1(exprGrammar);
			table.ExportToCsv("test_table.csv");

			/// Получаем тип лексера
			var lexerType = BuilderBase.BuildLexer(exprGrammar, "TestGrammarLexer");

			/// Создаём парсер
			var parser = new Parser(exprGrammar,
				new AntlrLexerAdapter(
					(Antlr4.Runtime.ICharStream stream) => (Antlr4.Runtime.Lexer)Activator.CreateInstance(lexerType, stream)
				)
			);

			return parser;
		}

		public static Parser BuildYacc()
		{
			Grammar yaccGrammar = new Grammar(GrammarType.LR);

			/// Пропускаемые сущности
			yaccGrammar.DeclareTerminal(new TerminalSymbol("COMMENT_L", @"'//' ~[\n\r]*"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("COMMENT_ML", "'/*' .*? '*/'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("COMMENT", "COMMENT_L|COMMENT_ML"));

			yaccGrammar.DeclareTerminal(new TerminalSymbol("STRING_SKIP", "'\\\\\"' | '\\\\\\\\'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("STRING_STD", "'\"' (STRING_SKIP|.)*? '\"'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("STRING_ESC", "'@\"' ~[\"]* '\"'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("STRING", "STRING_STD|STRING_ESC"));

			yaccGrammar.DeclareTerminal(new TerminalSymbol("DECLARATION_CODE", "'%{' (STRING|COMMENT|.)*? '%}'"));

			yaccGrammar.SetOption(ParsingOption.SKIP, "COMMENT", "STRING", "DECLARATION_CODE");

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
				new string[]{ "BORDER", "Any" },
				new string[]{ "BORDER" },
				new string[]{ }
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("declarations", new string[][]
			{
				new string[]{ "declarations", "declaration" },
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
				new string[]{ "Any" }
			}));


			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("identifiers", new string[][]
			{
				new string[]{ "identifiers", "ID" },
				new string[]{ },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("rules", new string[][]
			{
				new string[]{ "rules", "rule" },
				new string[]{ "rule" },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("rule", new string[][]
			{
				new string[]{ "ID", "COLON", "alternatives", "SEMICOLON" },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("alternatives", new string[][]
			{
				new string[]{ "alternatives", "PIPE" , "alternative" },
				new string[]{ "alternative" }
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("alternative", new string[][]
			{
				new string[]{ "alternative_components" },
				new string[]{ }
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("alternative_components", new string[][]
			{
				new string[]{ "alternative_components", "alternative_component" },
				new string[]{ "alternative_component" }
			}));


			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("alternative_component", new string[][]
			{
				new string[]{ "ID" },
				new string[]{ "LBRACE", "Any", "RBRACE" },
				new string[] {"LITERAL" }
			}));

			yaccGrammar.SetOption(ParsingOption.START, "grammar'");

			yaccGrammar.PostProcessing();

			TableLR1 table = new TableLR1(yaccGrammar);
			table.ExportToCsv("yacc_table.csv");

			var lexerType = BuilderBase.BuildLexer(yaccGrammar, "YaccGrammarLexer");

			/// Создаём парсер

			var parser = new Parser(yaccGrammar,
				new AntlrLexerAdapter(
					(Antlr4.Runtime.ICharStream stream) => (Antlr4.Runtime.Lexer)Activator.CreateInstance(lexerType, stream)
				)
			);

			return parser;
		}
	}
}
