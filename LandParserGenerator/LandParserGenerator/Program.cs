using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
	class Program
	{
		static void Main(string[] args)
		{
			Grammar yaccGrammar = new Grammar();

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
				new string[]{ "DECLARATION_NAME", "declaration_body" },
				new string[]{ "declaration_code" }
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
				new string[]{ "alternative", "alternatives" },
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
				new string[]{ "TEXT" },
				new string[] {"LITERAL" }
			}));
		}
	}
}
