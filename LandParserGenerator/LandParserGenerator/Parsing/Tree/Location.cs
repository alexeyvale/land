using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.Core.Parsing.Tree
{
	[Serializable]
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


		//public int StartLine { get; set; }
		//public int StartColumn { get; set; }
		//public int EndLine { get; set; }
		//public int EndColumn { get; set; }

		//public bool IsDefault { get { return StartLine * StartColumn * EndLine * EndColumn == 0; } }

		//public Location Merge(Location a)
		//{
		//	if (a.IsDefault)
		//		return this;

		//	return new Location()
		//	{
		//		StartLine = Math.Min(StartLine, a.StartLine),
		//		EndLine = Math.Max(EndLine, a.EndLine),
		//		StartColumn = StartLine < a.StartLine ? StartColumn : 
		//			a.StartLine < StartLine ? a.StartColumn : 
		//			Math.Min(StartColumn, a.StartColumn),
		//		EndColumn = EndColumn < a.EndColumn ? a.EndColumn : 
		//			a.EndColumn < EndColumn ? EndColumn : 
		//			Math.Max(EndColumn, a.EndColumn)
		//	};
		//}
	}
}
