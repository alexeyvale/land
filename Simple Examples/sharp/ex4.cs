using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICSharpCode.Decompiler.Tests.TestCases.Correctness
{
	class RefLocalsAndReturns
	{
		public delegate ref TReturn RefFunc<T1, TReturn>(T1 param1);
	}
}
