using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing.LR
{
	public class Parser
	{
		private Grammar grammar { get; set; }
		private TableLR1 Table { get; set; }

		private Stack<int> StatesStack { get; set; }
		private Stack<string> SymbolsStack { get; set; }
		private TokenStream LexingStream { get; set; }

		private ILexer Lexer { get; set; }

		public List<string> Log { get; private set; }

		public Parser(Grammar g, ILexer lexer)
		{
			grammar = g;
			Table = new TableLR1(g);
			Lexer = lexer;
		}

		public Node Parse(string text, out string errorMessage)
		{
			Log = new List<string>();
			errorMessage = String.Empty;

			StatesStack = new Stack<int>();
			SymbolsStack = new Stack<string>();

			while(true)
			{

			}

			return null;
		}


		private IToken SkipText()
		{	
			return null;
		}
	}
}
