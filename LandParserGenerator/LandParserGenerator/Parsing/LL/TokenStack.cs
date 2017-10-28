using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;

namespace LandParserGenerator.Parsing.LL
{
	public class TokenStream
	{
		private ILexer Lexer { get; set; }

		public TokenStream(ILexer lexer)
		{
			Lexer = lexer;
		}

		public void PushToken(IToken token)
		{

		}

		public IToken NextToken()
		{
			return null;
		}
	}
}
