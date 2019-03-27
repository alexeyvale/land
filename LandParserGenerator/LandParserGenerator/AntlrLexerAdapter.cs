using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using Antlr4.Runtime;

namespace Land.Core
{
	public class AntlrLexerAdapter: Lexing.ILexer
	{
		private Lexer Lexer { get; set; }

		private Func<ICharStream, Lexer> LexerConstructor { get; set; }

		private Grammar GrammarObject { get; set; }

		public AntlrLexerAdapter(Func<ICharStream, Lexer> constructor, Grammar grammar)
		{
			LexerConstructor = constructor;
			GrammarObject = grammar;
		}

		public void SetSourceFile(string filename)
		{
			var stream = new UnbufferedCharStream(new StreamReader(filename, Encoding.Default, true));
			Lexer = LexerConstructor(stream);
		}

		public void SetSourceText(string text)
		{
			byte[] textBuffer = Encoding.UTF8.GetBytes(text);
			MemoryStream memStream = new MemoryStream(textBuffer);

			var stream = CharStreams.fromStream(memStream);

			Lexer = LexerConstructor(stream);
		}

		public Lexing.IToken NextToken()
		{
			 return new AntlrTokenAdapter(Lexer.NextToken(), GrammarObject, Lexer);
		}

        public Land.Core.Lexing.IToken CreateToken(string name)
        {
            return new Lexing.StubToken(name);
        }
    }
}
