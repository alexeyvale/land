using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpDependencyAnalyzer
{
	public class ReferencePoint
	{
		public string FilePath { get; set; }

		public int PointOffset { get; set; }
		public int EnclosingOperatorStartOffset { get; set; }
		public int EnclosingOperatorEndOffset { get; set; }
	}

	public class Response
	{
		public List<ReferencePoint> Points { get; set; } = new List<ReferencePoint>();
	}
}
