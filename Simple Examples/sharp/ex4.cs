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
	
	public struct StructWithFixedSizeMembers
	{
		public unsafe fixed int Integers[100];
		public int NormalMember;
		public unsafe fixed double Doubles[200];

		[Obsolete("another attribute")]
		public unsafe fixed byte Old[1];
	}
}
