using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Land.Core
{
	public enum MessageType { Trace, Error, Warning }

	public class Message
	{
		public Anchor Location { get; set; }
		public string FileName { get; set; }
		public string Source { get; set; }
		public string Text { get; set; }
		public MessageType Type { get; set; }

		public override string ToString()
		{
			var resString = String.Empty;

			if (!String.IsNullOrEmpty(Source))
				resString += Source + ":\t";

			if (!String.IsNullOrEmpty(FileName))
				resString += Path.GetFileName(FileName) + "\t";

			if (Location != null)
				resString += $"({Location.Line},{Location.Column})\t";

			return $"{resString} {Text}";
		}

		private Message() { }

		private static Message Create(MessageType type, string text, Anchor loc, string src = null, string fileName = null)
		{
			return new Message()
			{
				Location = loc,
				Text = text,
				Source = src,
				FileName = fileName,
				Type = type
			};
		}

		private static Message Create(MessageType type, string text, int line, int col, string src = null)
		{
			return Create(type, text, new Anchor(line, col), src);
		}

		public static Message Trace(string text, Anchor loc, string src = null)
		{
			return Create(MessageType.Trace, text, loc, src);
		}

		public static Message Error(string text, Anchor loc, string src = null)
		{
			return Create(MessageType.Error, text, loc, src);
		}

		public static Message Warning(string text, Anchor loc, string src = null)
		{
			return Create(MessageType.Warning, text, loc, src);
		}
	}
}
