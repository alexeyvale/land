using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using Antlr4.Runtime;

namespace LandParserGenerator
{
	public class AntlrTokenAdapter: LandParserGenerator.Lexing.IToken
	{
		private IToken Token { get; set; }
		private Lexer Lexer { get; set; }

		public int Column { get { return Token.Column; } }
		public int Line { get { return Token.Line; } }

		public int StartOffset { get { return Token.StartIndex; } }
		public int EndOffset { get { return Token.StopIndex; } }

		public string Text { get { return Token.Text; } }

		public string Name
		{
			get
			{
				return Lexer.Vocabulary.GetSymbolicName(Token.Type);
			}
		}

		public AntlrTokenAdapter(IToken token, Lexer lexer)
		{
			Token = token;
			Lexer = lexer;
		}
	}

	public class AntlrLexerAdapter: LandParserGenerator.Lexing.ILexer
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

		public LandParserGenerator.Lexing.IToken NextToken()
		{
			 return new AntlrTokenAdapter(Lexer.NextToken(), Lexer);
		}

        public LandParserGenerator.Lexing.IToken CreateToken(string name)
        {
            return new LandParserGenerator.Lexing.StubToken(name);
        }
    }
}
