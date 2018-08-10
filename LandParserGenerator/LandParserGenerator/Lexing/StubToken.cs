using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.Core.Lexing
{
	public class StubToken: IToken
	{
        public int Column { get; set; } = 0;
        public int Line { get; set; } = 0;
        public int StartOffset { get; set; } = 0;
        public int EndOffset { get; set; } = 0;
        public string Text { get; set; } = String.Empty;
        public string Name { get; private set; }

        public StubToken(string name)
        {
            Name = name;
        }
    }

}
