using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace Land.GUI
{
	/// <summary>
	/// https://stackoverflow.com/questions/11911660/redirect-console-writeline-from-windows-application-to-a-string
	/// </summary>
	public class ConsoleWriterEventArgs : EventArgs
	{
		public string Value { get; private set; }
		public ConsoleWriterEventArgs(string value)
		{
			Value = value;
		}
	}

	public class ConsoleWriter : TextWriter
	{
		public override Encoding Encoding { get { return Encoding.UTF8; } }

		public override void Write(string value)
		{
			WriteEvent?.Invoke(this, new ConsoleWriterEventArgs(value));
			base.Write(value);
		}

		public override void WriteLine(string value)
		{
			WriteLineEvent?.Invoke(this, new ConsoleWriterEventArgs(value));
			base.WriteLine(value);
		}

		public event EventHandler<ConsoleWriterEventArgs> WriteEvent;
		public event EventHandler<ConsoleWriterEventArgs> WriteLineEvent;
	}
}
