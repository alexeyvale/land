using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing.LL
{
	public class ParseAction
	{
		public enum ParseActionType { Push, Pop }

		public ParseActionType Type { get; set; }
		public Node Value { get; set; }
	}
}
