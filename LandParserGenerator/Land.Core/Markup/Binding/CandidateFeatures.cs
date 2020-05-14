using System;
using System.Linq;
using System.Reflection;

namespace Land.Markup.Binding
{
	public struct CandidateFeatures
	{
		#region Флаги существования контекстов
		public int ExistsH { get; set; }
		public int ExistsI { get; set; }
		public int ExistsA { get; set; }
		public int ExistsS { get; set; }
		#endregion

		#region Похожести текущего кандидата
		public double SimHSeq { get; set; }
		public double SimHCore { get; set; }
		public double SimI { get; set; }
		public double SimA { get; set; }
		public double SimS { get; set; }
		public double SimF { get; set; }
		#endregion

		#region Дополнительные проверки
		public int FileHasSameName { get; set; }
		public int FileHasBeforeSibling { get; set; }
		public int FileHasAfterSibling { get; set; }
		public int CorrectBefore { get; set; }
		public int CorrectAfter { get; set; }
		public double LineShiftRatio { get; set; }
		#endregion

		#region Максимальные похожести каждого из контекстов
		public double MaxSimHSeq { get; set; }
		public double MaxSimHCore { get; set; }
		public double MaxSimI { get; set; }
		public double MaxSimA { get; set; }
		public double MaxSimS { get; set; }
		public double MaxSimF { get; set; }
		#endregion

		#region Максимальные похожести в рамках того же контекста предков, что и у рассматриваемого кандидата
		public double MaxSimHSeq_SameA { get; set; }
		public double MaxSimHCore_SameA { get; set; }
		public double MaxSimI_SameA { get; set; }
		public double MaxSimS_SameA { get; set; }
		#endregion

		#region Максимальные похожести других контекстов у элементов с максимальной похожестью указанного
		public double MaxSimH_MaxSimI { get; set; }
		public double MaxSimH_MaxSimA { get; set; }
		public double MaxSimI_MaxSimH { get; set; }
		public double MaxSimI_MaxSimA { get; set; }
		public double MaxSimA_MaxSimH { get; set; }
		public double MaxSimA_MaxSimI { get; set; }
		#endregion

		#region Доля элементов с лучшими похожестями контекстов 
		public double RatioBetterSimH { get; set; }
		public double RatioBetterSimI { get; set; }
		public double RatioBetterSimA { get; set; }
		public double RatioBetterSimS { get; set; }
		#endregion

		// Доля кандидатов с тем же контекстом предков
		public double RatioSameAncestor { get; set; }

		#region Доля элементов с лучшими похожестями контекстов в пределах того же контекста предков
		public double RatioBetterSimH_SameA { get; set; }
		public double RatioBetterSimI_SameA { get; set; }
		public double RatioBetterSimS_SameA { get; set; }
		#endregion

		#region Разные соотношения длин
		public int IsCandidateInnerContextLonger { get; set; }

		public int IsCandidateHeaderCoreLonger { get; set; }

		public double InnerLengthRatio { get; set; }

		public double InnerLengthRatio1000 { get; set; }

		public double HeaderCoreLengthRatio { get; set; }
		#endregion

		public int IsAuto { get; set; }
		
		public string ToString(string separator)
		{
			var currentInstance = this;

			return String.Join(separator, this.GetType()
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Select(p=>p.GetValue(currentInstance, null)));
		}

		public static string ToHeaderString(string separator)
		{
			return String.Join(separator, typeof(CandidateFeatures)
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Select(p => p.Name));
		}
	}
}
