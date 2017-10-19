using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
	public class Token: IGrammarElement, IFirstSupporting
	{
		public string Name { get; private set; }
		public string Pattern { get; private set; }

		public Token(string name, string pattern)
		{
			Name = name;
			Pattern = pattern;
		}

		public HashSet<Token> First(Grammar g)
		{
			return new HashSet<Token>() { g.Tokens[this.Name] };
		}

		public override bool Equals(object obj)
		{
			return obj is Token && ((Token)obj).Name == Name;
		}

		public override int GetHashCode()
		{
			return Name.GetHashCode();
		}

		public const string EmptyTokenName = "EMPTY";
		public static Token Empty { get { return new Token(EmptyTokenName, String.Empty); } }
	}
}
