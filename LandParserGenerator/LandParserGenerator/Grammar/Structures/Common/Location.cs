using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QUT.Gppg;

namespace Land.Core
{
	public class PointLocation
	{
		private int COLUMN_NUMBER_CORRECTION = 1;

		public int? Line { get; set; }
		public int? Column { get; set; }
		public int? Offset { get; set; }

		public PointLocation(int offset)
		{
			Line = null;
			Column = null;
			Offset = offset;
		}

		public PointLocation(int ln, int col, int offset)
		{
			Line = ln;
			Column = col + COLUMN_NUMBER_CORRECTION;
			Offset = offset;
		}
	}

	public class SegmentLocation: IMerge<SegmentLocation>
	{
		public PointLocation Start { get; set; }

		public PointLocation End { get; set; }

		public SegmentLocation Merge(SegmentLocation last)
		{
			if (last == null)
				return this;

			return new SegmentLocation()
			{
				Start = this.Start,
				End = last.End
			};
		}
	}
}
