using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Text;

namespace Land.Core.Lexing
{
	public class AntlrTokenAdapter : IToken
	{
		private Antlr4.Runtime.IToken Token { get; set; }

		public SegmentLocation Location { get; private set; }
		public string Text => Token.Text;
		public string Name { get; private set; }

		public AntlrTokenAdapter(Antlr4.Runtime.IToken token, Antlr4.Runtime.Lexer lexer)
		{
			Token = token;
			Name = lexer.Vocabulary.GetSymbolicName(Token.Type);

			Location = new SegmentLocation()
			{
				Start = new PointLocation(Token.Line, Token.Column, Token.StartIndex),
				End = new PointLocation(Token.StopIndex)
			};
		}
	}
}
