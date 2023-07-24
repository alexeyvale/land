using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.GUI
{
	public class DocumentSegment
	{
		public string FileName { get; set; }
		public int StartOffset { get; set; }
		public int EndOffset { get; set; }
		public bool CaptureWholeLine { get; set; }
	}
}
