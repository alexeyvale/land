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

		private SegmentLocation _location = null;
		public SegmentLocation Location
		{
			get
			{
				if (_location == null)
				{
					var lines = Token.Text.Split('\n');
					var emptyAfterLastEnd = lines[lines.Length - 1].Length == 0;

					_location = new SegmentLocation()
					{
						Start = new PointLocation(Token.Line, Token.Column, Token.StartIndex),
						End = new PointLocation(
							Token.Line + (emptyAfterLastEnd ? lines.Length : lines.Length - 1),
							emptyAfterLastEnd ? lines[lines.Length - 2].Length - 1 : lines[lines.Length - 1].Length - 1,
							Token.StopIndex
						)
					};
				}

				return _location;
			}
		}

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
