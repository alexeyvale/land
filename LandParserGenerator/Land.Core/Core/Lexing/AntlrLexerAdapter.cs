using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Antlr4.Runtime;

namespace Land.Core.Lexing
{
	public class AntlrLexerAdapter: ILexer
	{
		private Lexer Lexer { get; set; }

		private Func<ICharStream, Lexer> LexerConstructor { get; set; }

		public AntlrLexerAdapter(Func<ICharStream, Lexer> constructor)
		{
			LexerConstructor = constructor;
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

		public IToken NextToken()
		{
			 return new AntlrTokenAdapter(Lexer.NextToken(), Lexer);
		}

        public IToken CreateToken(string name)
        {
            return new StubToken(name);
        }
    }
}
