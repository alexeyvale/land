using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QUT.Gppg;

namespace Land.Core
{
	[Serializable]
	public class PointLocation
	{
		public int? Line { get; private set; }

		public int? Column { get;  private set; }

		public int Offset { get; private set; }

		public PointLocation(int offset)
		{
			Offset = offset;
		}

		public PointLocation(int? ln, int? col, int offset)
		{
			Line = ln;
			Column = col;
			Offset = offset;
		}

		public void Shift(int lnDelta, int colDelta, int offsetDelta)
		{
			Offset += offsetDelta;

			if(Line.HasValue)
			{
				Line += lnDelta;
				Column += colDelta;

				if (Line <= 0 || Offset < 0)
				{
					Line = 1;
					Offset = 0;
				}

				if (Column <= 0)
					Column = 0;
			}

			if (Offset < 0)
				Offset = 0;
		}
	}

	[Serializable]
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
				Start = Start,
				End = last.End
			};
		}

		public SegmentLocation SmartMerge(SegmentLocation loc)
		{
			if (loc == null)
			{
				return this;
			}
			else
			{
				return new SegmentLocation()
				{
					Start = this.Start.Offset < loc.Start.Offset ? this.Start : loc.Start,
					End = this.End.Offset > loc.End.Offset ? this.End : loc.End,
				};
			}
		}

		public int? Length => Start != null && End != null ? End.Offset - Start.Offset + 1 : (int?)null;

		public void Shift(int lnDelta, int colDelta, int offsetDelta)
		{
			Start.Shift(lnDelta, colDelta, offsetDelta);
			End.Shift(lnDelta, colDelta, offsetDelta);
		}

		public override bool Equals(object obj)
		{
			return obj is SegmentLocation other 
				&& Start.Offset == other.Start.Offset
				&& End.Offset == other.End.Offset;
		}

		public bool Includes(SegmentLocation other)
		{
			return other != null 
				&& Start.Offset <= other.Start.Offset
				&& End.Offset >= other.End.Offset;
		}

		public bool Overlaps(SegmentLocation other)
		{
			return other != null
				&& (Start.Offset <= other.Start.Offset && other.Start.Offset <= End.Offset) 
				^ (Start.Offset <= other.End.Offset && other.End.Offset <= End.Offset);
		}
	}

    public class SegmentLocationComparer : IComparer<SegmentLocation>
    {
		public int Compare(SegmentLocation x, SegmentLocation y) =>
			x.Start.Offset - y.Start.Offset;
    }
}
