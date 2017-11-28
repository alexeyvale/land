using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Lexing
{
	public interface ILexer
	{
		IToken NextToken();

        IToken CreateToken(string name);

		void SetSourceFile(string filename);

		void SetSourceText(string text);
	}

}
