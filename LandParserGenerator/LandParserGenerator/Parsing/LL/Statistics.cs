using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;
using LandParserGenerator.Lexing;

namespace LandParserGenerator.Parsing.LL
{
	public class Statistics: BaseStatistics
	{
		public Dictionary<string, int> ChangeAlternativeBacktrackings { get; set; } = new Dictionary<string, int>();

		public override string ToString()
		{
			return $"Всего вызовов бэктрекинга:\t{BacktracingCalled}{System.Environment.NewLine}";
		}
	}
}
