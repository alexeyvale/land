using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.Core.Lexing
{
	public interface IToken
	{
		SegmentLocation Location { get; }
		string Text { get; }
		string Name { get; }
	}

}
