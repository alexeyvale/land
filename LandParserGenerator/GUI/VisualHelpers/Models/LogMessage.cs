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

		[ColumnSettings("Источник", 4)]
		public string Source { get; set; }

		[ColumnSettings("Описание", 1)]
		public string Text { get; set; }

		[ColumnSettings("Тип", 0)]
		public MessageType Type { get; set; }

		[ColumnSettings("Строка", 2)]
		public int? Line => Location?.Line;

		[ColumnSettings("Столбец", 3)]
		public int? Column => Location?.Column;

		public LogMessage(Message msg)
		{
			Location = msg.Location;
			Text = msg.Text;
			Type = msg.Type;
			Source = msg.Source;
		}
	}
}
