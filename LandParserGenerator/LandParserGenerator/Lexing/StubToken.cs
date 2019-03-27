using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.Core.Lexing
{
	public class StubToken: IToken
	{
        public string Text { get; set; } = String.Empty;
        public string Name { get; private set; }
		public int Index { get; set; }
		public SegmentLocation Location { get; set; } = new SegmentLocation()
		{
			Start = new PointLocation(0, 0, 0),
			End = new PointLocation(0, 0, 0)
		};

		public StubToken(string name)
        {
            Name = name;
        }
    }

}
