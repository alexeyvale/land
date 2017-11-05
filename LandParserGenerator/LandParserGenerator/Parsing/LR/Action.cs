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

		public override string ToString()
		{
			return $"s {TargetItemIndex}";
		}
		public override bool Equals(object obj)
		{
			if (obj is ShiftAction)
			{
				var b = (ShiftAction)obj;
				return TargetItemIndex == b.TargetItemIndex;
			}
			else
				return false;
		}

		public override int GetHashCode()
		{
			return TargetItemIndex;
		}
	}

	public class ReduceAction: Action
	{
		public Alternative ReductionAlternative { get; set; }

		public override string ToString()
		{
			return $"r {ReductionAlternative}";
		}

		public override bool Equals(object obj)
		{
			if (obj is ReduceAction)
			{
				var b = (ReduceAction)obj;
				return ReductionAlternative.Equals(b.ReductionAlternative);
			}
			else
				return false;
		}

		public override int GetHashCode()
		{
			return ReductionAlternative.GetHashCode();
		}
	}

	public class AcceptAction: Action
	{
		public override string ToString()
		{
			return "accept";
		}

		public override bool Equals(object obj)
		{
			return obj is AcceptAction;
		}

		public override int GetHashCode()
		{
			return 0;
		}
	}
}
