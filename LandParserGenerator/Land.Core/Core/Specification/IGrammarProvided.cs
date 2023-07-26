using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Land.Core.Specification
{
	public interface IGrammarProvided
	{
		Grammar GrammarObject { get; }
	}
}
