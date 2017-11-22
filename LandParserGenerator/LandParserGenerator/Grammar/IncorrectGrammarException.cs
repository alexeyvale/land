using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
    public class IncorrectGrammarException : Exception
    {
        public IncorrectGrammarException(string message): base(message)
        { }
    }
}
