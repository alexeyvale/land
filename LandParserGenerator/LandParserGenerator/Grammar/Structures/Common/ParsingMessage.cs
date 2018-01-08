using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
	public enum MessageType { Trace, Error, Warning }

	public class Message
	{
		public Anchor Location { get; set; }
		public string Source { get; set; }
		public string Text { get; set; }
		public MessageType Type { get; set; }

		public override string ToString()
		{
			var resString = String.Empty;

			if (!String.IsNullOrEmpty(Source))
				resString += Source + ":";

			if (Location != null)
				resString += $"({Location.Line},{Location.Column})";

			return $"{resString} {Text}";
		}

		private Message() { }

		private static Message Create(MessageType type, string text, int line, int col, string src = null)
		{
			return new Message()
			{
				Location = new Anchor(line, col),
				Text = text,
				Source = src,
				Type = type
			};
		}

		private static Message Create(MessageType type, string text, string src = null)
		{
			return new Message()
			{
				Location = null,
				Text = text,
				Source = src,
				Type = type
			};
		}

		public static Message Trace(string text, int line, int col, string src = null)
		{
			return Create(MessageType.Trace, text, line, col, src);
		}

		public static Message Error(string text, int line, int col, string src = null)
		{
			return Create(MessageType.Error, text, line, col, src);
		}

		public static Message Warning(string text, int line, int col, string src = null)
		{
			return Create(MessageType.Warning, text, line, col, src);
		}

		public static Message Trace(string text, string src = null)
		{
			return Create(MessageType.Trace, text, src);
		}

		public static Message Error(string text, string src = null)
		{
			return Create(MessageType.Error, text, src);
		}

		public static Message Warning(string text, string src = null)
		{
			return Create(MessageType.Warning, text, src);
		}
	}
}
