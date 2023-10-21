using Land.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.GUI
{
	public class LogMessage
	{
		public PointLocation Location { get; set; }

		[ColumnSettings("Источник")]
		public string Source { get; set; }

		[ColumnSettings("Описание")]
		public string Text { get; set; }

		[ColumnSettings("Тип")]
		public MessageType Type { get; set; }

		[ColumnSettings("(Стр, Столб)")]
		public string Coords => $"({Location?.Line},{Location?.Column})";

		public LogMessage(Message msg)
		{
			Location = msg.Location;
			Text = msg.Text;
			Type = msg.Type;
			Source = msg.Source;
		}
	}
}
