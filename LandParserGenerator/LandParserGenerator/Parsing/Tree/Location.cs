using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.Core.Parsing.Tree
{
	public class Location
	{
		public int StartOffset { get; set; }
		public int EndOffset { get; set; }

		public Location Merge(Location loc)
		{
			if (loc == null)
			{
				return this;
			}
			else
			{
				return new Location()
				{
					StartOffset = Math.Min(this.StartOffset, loc.StartOffset),
					EndOffset = Math.Max(this.EndOffset, loc.EndOffset)
				};
			}
		}
	}
}
