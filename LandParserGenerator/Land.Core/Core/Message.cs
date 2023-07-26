using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Land.Core
{
	public enum MessageType { Trace, Error, Warning }

	public enum MessageAddInfoKey { UnexpectedToken, UnexpectedLexeme, ExpectedTokens }

	[Serializable]
	public class Message
	{
		public PointLocation Location { get; set; }
		public string FileName { get; set; }
		public string Source { get; set; }
		public string Text { get; set; }
		public MessageType Type { get; set; }

		public Dictionary<MessageAddInfoKey, object> AddInfo { get; set; }

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

		private static Message Create(MessageType type, string text, PointLocation loc, string src = null, string fileName = null, Dictionary<MessageAddInfoKey, object> addInfo = null)
		{
			return new Message()
			{
				Location = loc,
				Text = text,
				Source = src,
				FileName = fileName,
				Type = type,
				AddInfo = addInfo
			};
		}

		public static Message Trace(string text, PointLocation loc, string src = null, Dictionary<MessageAddInfoKey, object> addInfo = null)
		{
			return Create(MessageType.Trace, text, loc, src, null, addInfo);
		}

		public static Message Error(string text, PointLocation loc, string src = null, Dictionary<MessageAddInfoKey, object> addInfo = null)
		{
			return Create(MessageType.Error, text, loc, src, null, addInfo);
		}

		public static Message Warning(string text, PointLocation loc, string src = null, Dictionary<MessageAddInfoKey, object> addInfo = null)
		{
			return Create(MessageType.Warning, text, loc, src, null, addInfo);
		}
	}
}
