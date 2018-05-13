using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;
using LandParserGenerator.Lexing;

namespace LandParserGenerator.Parsing
{
	public abstract class BaseStatistics
	{
		public TimeSpan TimeSpent { get; set; }
		public int BacktracingCalled { get; set; }
		public int MaxNumberOfDecisions { get; set; }
		public int LongestBacktracking { get; set; }
	}
}
