using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

using LandParserGenerator.Parsing.Tree;
using LandParserGenerator.Lexing;

namespace LandParserGenerator.Parsing.LR
{
	public class Statistics: BaseStatistics
	{
		public int ChangeTransitionDecisionChanges { get; set; }
		public int FinishAnyDecisionChanges { get; set; }
		public int FailedRuleReenterRejections { get; set; }

		public override string ToString()
		{
			var result = $"Всего вызовов бэктрекинга:\t{BacktracingCalled}{System.Environment.NewLine}"
				+ $"Наибольшее число последовательно отменённых решений:\t{LongestBacktracking}{System.Environment.NewLine}"
				+ $"Переходов по Any:\t{ChangeTransitionDecisionChanges}{System.Environment.NewLine}"
				+ $"Продлений Any:\t{FinishAnyDecisionChanges}{System.Environment.NewLine}"
				+ $"Предотвращено заходов в заведомо неуспешные правила:\t{FailedRuleReenterRejections}{System.Environment.NewLine}"
				+ $"Потрачено времени:\t{TimeSpent}{System.Environment.NewLine}";

			return result;
		}
	}
}
