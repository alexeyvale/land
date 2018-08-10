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
	public class Statistics
	{
		public TimeSpan TimeSpent { get; set; }

		public override string ToString()
		{
			return $"Время парсинга: {TimeSpent.ToString(@"hh\:mm\:ss\:ff")}";
		}
	}
}
