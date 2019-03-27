using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.Core
{
	public interface ISymbol
	{
		string Name { get; }

		int Index { get; set;  }
	}
}
