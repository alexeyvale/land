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
					_location = new SegmentLocation()
					{
						Start = new PointLocation(Token.Line, Token.Column, Token.StartIndex),
						/// Лексический анализатор остановился за концом текущего токена
						End = new PointLocation(Lexer.Line, Lexer.Column - 1, Token.StopIndex)
					};
				}

				return _location;
			}
		}
		public string Text => Token.Text;
		public string Name { get; private set; }
		public int Index { get; set; }

		public AntlrTokenAdapter(IToken token, Grammar grammar, Lexer lexer)
		{
			Token = token;
			Name = lexer.Vocabulary.GetSymbolicName(Token.Type);
			Index = grammar.SymbolToIndex[Name];
			Lexer = lexer;
		}
	}
}
