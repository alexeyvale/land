using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QUT.Gppg;

namespace LandParserGenerator
{
	public class Anchor
	{
		private int COLUMN_NUMBER_CORRECTION = 1;

		public int Line { get; set; }
		public int Column { get; set; }

		public Anchor(int ln, int col)
		{
			Line = ln;
			Column = col + COLUMN_NUMBER_CORRECTION;
		}

		public static implicit operator Anchor(LexLocation loc)
		{
			return new Anchor(loc.StartLine, loc.StartColumn);
		}
	}
}
