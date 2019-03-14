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
		public int TokensCount { get; set; }
		public TimeSpan GeneralTimeSpent { get; set; }
		public TimeSpan RecoveryTimeSpent { get; set; }
		public int RecoveryTimes { get; set; }
		public int RecoveryTimesAny { get; set; }
		public int LongestRollback { get; set; }

		public static Statistics operator+(Statistics a, Statistics b)
		{
			return new Statistics
			{
				TokensCount = a.TokensCount + b.TokensCount,
				GeneralTimeSpent = a.GeneralTimeSpent + b.GeneralTimeSpent,
				RecoveryTimeSpent = a.RecoveryTimeSpent + b.RecoveryTimeSpent,
				LongestRollback = a.LongestRollback + b.LongestRollback,
				RecoveryTimes = a.RecoveryTimes + b.RecoveryTimes,
				RecoveryTimesAny = Math.Max(a.RecoveryTimesAny, b.RecoveryTimesAny)
			};
		}

		public override string ToString()
		{
			return $"Количество токенов: {TokensCount};{Environment.NewLine}" +
				$"Время парсинга: {GeneralTimeSpent.ToString(@"hh\:mm\:ss\:ff")};{Environment.NewLine}" +
				$"Время восстановлений от ошибки: {RecoveryTimeSpent.ToString(@"hh\:mm\:ss\:ff")};{Environment.NewLine}" +
				$"Количество восстановлений от ошибки: {RecoveryTimes};{Environment.NewLine}" +
				$"Восстановлений при сопоставлении Any: {RecoveryTimesAny};{Environment.NewLine}" +
				$"Количество токенов в самом длинном возврате: {LongestRollback}{Environment.NewLine}";
		}
	}
}
