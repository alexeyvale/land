using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing.LL
{
	public class ParsingStackAction
	{
		public enum ParsingStackActionType { Push, Pop }

		public ParsingStackActionType Type { get; set; }
		public Node Value { get; set; }
	}
}
