using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManualRemappingTool
{
	public enum MessageType { Error, Info, Success }

	public class MessageSentEventArgs
	{
		public string Message { get; set; }
		public DateTime Stamp { get; set; } = DateTime.Now;
		public MessageType Type { get; set; }
	}
}
