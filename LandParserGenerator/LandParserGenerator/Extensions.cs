using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;

namespace Land.Core
{
	public interface IGrammarProvided
	{
		Grammar GrammarObject { get; }
	}

	public static class Extensions
	{
		public static string GetTokenInfoForMessage(this IGrammarProvided target, IToken token)
		{
			var userified = target.GrammarObject.Userify(token.Name);
			if (userified == token.Name && token.Name != Grammar.ANY_TOKEN_NAME && token.Name != Grammar.EOF_TOKEN_NAME)
				return $"{token.Name}: '{token.Text}'";
			else
				return userified;
		}
	}
}
