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
		private IDictionary<string, int> NameTypeMap { get; set; }

		public int Column { get { return Token.Column; } }
		public int Line { get { return Token.Line; } }

		public string Text { get { return Token.Text; } }

		public string Name
		{
			get
			{
				return NameTypeMap
					.Where(m => m.Value == Token.Type && m.Key.All(c=>Char.IsLetterOrDigit(c) || c == '_'))
					.First().Key;
			}
		}

		public AntlrTokenAdapter(IToken token, IDictionary<string, int> mapping)
		{
			Token = token;
			NameTypeMap = mapping;
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
			var stream = new UnbufferedCharStream(new StreamReader(filename));
			Lexer = LexerConstructor(stream);
		}

		public void SetSourceText(string text)
		{
			byte[] textBuffer = Encoding.Default.GetBytes(text);
			MemoryStream memStream = new MemoryStream(textBuffer);

			var stream = new UnbufferedCharStream(memStream);

			Lexer = LexerConstructor(stream);
		}

		public LandParserGenerator.Lexing.IToken NextToken()
		{
			 return new AntlrTokenAdapter(Lexer.NextToken(), Lexer.TokenTypeMap);
		}
	}
}
