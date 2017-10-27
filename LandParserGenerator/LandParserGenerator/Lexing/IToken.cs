using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Lexing
{
	public interface IToken
	{
		int Column { get; }
		int Line { get; }
		int StartOffset { get; }
		int EndOffset { get; }
		string Text { get; }
		string Name { get; }
	}

}
