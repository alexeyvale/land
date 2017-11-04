using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator.Parsing.LR
{
	public abstract class Action
	{ }

	public class ShiftAction: Action
	{
		public int TargetItemIndex { get; set; } 
	}

	public class ReduceAction: Action
	{
		public Alternative ReductionAlternative { get; set; }
	}

	public class AcceptAction: Action
	{ }
}
