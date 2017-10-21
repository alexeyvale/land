using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
	class Program
	{
		static void BuildYacc()
		{
			Grammar yaccGrammar = new Grammar();

			yaccGrammar.DeclareTerminal(new Token("BORDER", "%%"));
			yaccGrammar.DeclareTerminal(new Token("DECLARATION_NAME", null));
			yaccGrammar.DeclareTerminal(new Token("CORNER_LEFT", "<"));
			yaccGrammar.DeclareTerminal(new Token("ID", null));
			yaccGrammar.DeclareTerminal(new Token("CORNER_RIGHT", ">"));
			yaccGrammar.DeclareTerminal(new Token("RULE_NAME", null));
			yaccGrammar.DeclareTerminal(new Token("COLON", ":"));
			yaccGrammar.DeclareTerminal(new Token("SEMICOLON", ";"));
			yaccGrammar.DeclareTerminal(new Token("LITERAL", null));
			yaccGrammar.DeclareTerminal(new Token("LBRACE", "{"));
			yaccGrammar.DeclareTerminal(new Token("RBRACE", "}"));
			yaccGrammar.DeclareTerminal(new Token("PIPE", "|"));


			yaccGrammar.DeclareNonterminal(new Rule("grammar", new string[][]
			{
				new string[]{ "declarations", "BORDER", "rules", "grammar_ending" }
			}));

			yaccGrammar.DeclareNonterminal(new Rule("grammar_ending", new string[][]
			{
				new string[]{ "BORDER", "TEXT" },
				new string[]{ "BORDER" },
				new string[]{ }
			}));

			yaccGrammar.DeclareNonterminal(new Rule("declarations", new string[][]
			{
				new string[]{ "declaration", "declarations" },
				new string[]{ },
			}));

			yaccGrammar.DeclareNonterminal(new Rule("declaration", new string[][]
			{
				new string[]{ "DECLARATION_NAME", "optional_type", "declaration_body" }
			}));

			yaccGrammar.DeclareNonterminal(new Rule("optional_type", new string[][]
			{
				new string[]{ "CORNER_LEFT", "ID", "CORNER_RIGHT" },
				new string[]{ }
			}));

			yaccGrammar.DeclareNonterminal(new Rule("declaration_body", new string[][]
			{
				new string[]{ "identifiers" },
				new string[]{ "TEXT" }
			}));

			yaccGrammar.DeclareNonterminal(new Rule("identifiers", new string[][]
			{
				new string[]{ "ID", "identifiers" },
				new string[]{ "ID" }
			}));

			yaccGrammar.DeclareNonterminal(new Rule("rules", new string[][]
			{
				new string[]{ "rule", "rules" },
				new string[]{ "rule" }
			}));

			yaccGrammar.DeclareNonterminal(new Rule("rule", new string[][]
			{
				new string[]{ "RULE_NAME", "COLON", "alternatives", "SEMICOLON" },
			}));

			yaccGrammar.DeclareNonterminal(new Rule("alternatives", new string[][]
			{
				new string[]{ "alternative", "PIPE", "alternatives" },
				new string[]{ "alternative" }
			}));

			yaccGrammar.DeclareNonterminal(new Rule("alternative", new string[][]
			{
				new string[]{ "alternative_component", "alternative" },
				new string[]{ }
			}));

			yaccGrammar.DeclareNonterminal(new Rule("alternative_component", new string[][]
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
			Grammar exprGrammar = new Grammar();

			exprGrammar.DeclareTerminal(new Token("PLUS", "+"));
			exprGrammar.DeclareTerminal(new Token("MULT", "*"));
			exprGrammar.DeclareTerminal(new Token("LPAR", "("));
			exprGrammar.DeclareTerminal(new Token("RPAR", ")"));
			exprGrammar.DeclareTerminal(new Token("ID", null));

			exprGrammar.DeclareNonterminal(new Rule("E", new string[][]
			{
				new string[]{ "T", "E'" }
			}));

			exprGrammar.DeclareNonterminal(new Rule("E'", new string[][]
			{
				new string[]{ "PLUS", "T","E'" },
				new string[]{ }
			}));

			exprGrammar.DeclareNonterminal(new Rule("T", new string[][]
			{
				new string[]{ "F", "T'" },
			}));

			exprGrammar.DeclareNonterminal(new Rule("T'", new string[][]
			{
				new string[]{ "MULT", "F","T'" },
				new string[]{ }
			}));

			exprGrammar.DeclareNonterminal(new Rule("F", new string[][]
			{
				new string[]{ "LPAR", "E","RPAR" },
				new string[]{ "ID" }
			}));

			exprGrammar.SetStartSymbol("E");

			TableLL1 table = new TableLL1(exprGrammar);
			table.ExportToCsv("expr_table.csv");
		}

		static void Main(string[] args)
		{
			BuildExpressionGrammar();

			Console.ReadLine();
		}
	}
}
