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
	public class AntlrTokenAdapter: Lexing.IToken
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
}
