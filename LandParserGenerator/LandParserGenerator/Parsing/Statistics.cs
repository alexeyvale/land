using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace Land.Core.Parsing
{
	public class Statistics
	{
		public TimeSpan TimeSpent { get; set; }
		public int RecoveryTimes { get; set; }

		public override string ToString()
		{
			return $"Время парсинга: {TimeSpent.ToString(@"hh\:mm\:ss\:ff")}{Environment.NewLine}Восстановлений от ошибки: {RecoveryTimes}";
		}
	}
}
