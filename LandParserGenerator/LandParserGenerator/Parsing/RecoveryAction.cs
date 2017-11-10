using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LandParserGenerator.Lexing;
using LandParserGenerator.Parsing.Tree;

namespace LandParserGenerator.Parsing
{
	public enum RecoveryActionType { UseSecondaryTextAlt, SkipMoreText }

	public class RecoveryAction
	{
		public RecoveryActionType ActionType { get; set; }
		public int RecoveryStartTokenIndex { get; set; }
		public int RecoveryEndTokenIndex { get; set; }
		public int ErrorTokenIndex { get; set; }
		public int AttemptsCount { get; set; }
	}
}
