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
	public class AntlrTokenAdapter : Lexing.IToken
	{
		private IToken Token { get; set; }

		public SegmentLocation Location { get; private set; }
		public string Text => Token.Text;
		public string Name { get; private set; }

		public AntlrTokenAdapter(IToken token, Lexer lexer)
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
