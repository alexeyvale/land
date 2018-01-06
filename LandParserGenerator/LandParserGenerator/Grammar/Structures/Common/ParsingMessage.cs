using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
	public class ParsingMessage
	{
		public Anchor Location { get; set; }
		public string Source { get; set; }
		public string Message { get; set; }

		public static implicit operator ParsingMessage(string msg)
		{
			return new ParsingMessage()
			{
				Location = null,
				Message = msg,
				Source = null
			};
		}

		public override string ToString()
		{
			var resString = String.Empty;

			if (!String.IsNullOrEmpty(Source))
				resString += Source + ":";

			if (Location != null)
				resString += $"({Location.Line}, {Location.Column}) ";

			return resString + Message;
		}
	}
}
