using System;
using System.Linq;
using System.Reflection;

namespace Land.Markup.Binding
{
	public struct CandidateFeatures
	{
		public int IsSingleCandidate { get; set; }

		#region Флаги существования контекстов
		public int ExistsHSeq_Point { get; set; }
		public int ExistsHCore_Point { get; set; }
		public int ExistsI_Point { get; set; }
		public int ExistsA_Point { get; set; }
		public int ExistsSBeforeGlobal_Point { get; set; }
		public int ExistsSAfterGlobal_Point { get; set; }
		public int ExistsSBeforeEntity_Point { get; set; }
		public int ExistsSAfterEntity_Point { get; set; }

		public int ExistsHCore_Candidate { get; set; }
		public int ExistsI_Candidate { get; set; }
		#endregion

		#region Похожести текущего кандидата
		public double SimHSeq { get; set; }
		public double SimHCore { get; set; }
		public double SimI { get; set; }
		public double SimA { get; set; }
		public double SimSBeforeGlobal { get; set; }
		public double SimSAfterGlobal { get; set; }
		//public double SimSBeforeEntity {get;set;}
		//public double SimSAfterEntity { get; set; }
		#endregion

		#region Дополнительные проверки
		public int AncestorHasBeforeSibling { get; set; }
		public int AncestorHasAfterSibling { get; set; }
		public int CorrectBefore { get; set; }
		public int CorrectAfter { get; set; }
		#endregion

		#region Максимальные похожести каждого из контекстов
		public double MaxSimHSeq { get; set; }
		public double MaxSimHCore { get; set; }
		public double MaxSimI { get; set; }
		public double MaxSimA { get; set; }
		public double MaxSimSBeforeGlobal { get; set; }
		public double MaxSimSAfterGlobal { get; set; }
		//public double MaxSimSBeforeEntity { get; set; }
		//public double MaxSimSAfterEntity { get; set; }
		#endregion

		#region Максимальные похожести в рамках того же контекста предков, что и у рассматриваемого кандидата
		public double MaxSimHSeq_SameA { get; set; }
		public double MaxSimHCore_SameA { get; set; }
		public double MaxSimI_SameA { get; set; }
		public double MaxSimSBeforeGlobal_SameA { get; set; }
		public double MaxSimSAfterGlobal_SameA { get; set; }
		//public double MaxSimSBeforeEntity_SameA { get; set; }
		//public double MaxSimSAfterEntity_SameA { get; set; }
		#endregion

		#region Доля элементов с лучшими похожестями контекстов 
		public double RatioBetterSimHSeq { get; set; }
		public double RatioBetterSimI { get; set; }
		public double RatioBetterSimA { get; set; }
		//public double RatioBetterSimSBeforeGlobal { get; set; }
		//public double RatioBetterSimSAfterGlobal { get; set; }
		#endregion

		#region Доля элементов с лучшими похожестями контекстов в пределах того же контекста предков
		public double RatioSameAncestor { get; set; }

		public double RatioBetterSimHSeq_SameA { get; set; }
		public double RatioBetterSimI_SameA { get; set; }
		//public double RatioBetterSimSBeforeGlobal_SameA { get; set; }
		//public double RatioBetterSimSAfterGlobal_SameA { get; set; }
		#endregion

		#region Разные соотношения длин
		public int IsCandidateInnerContextLonger { get; set; }
		public int IsCandidateHeaderCoreLonger { get; set; }
		
		public double InnerLengthRatio { get; set; }
		public double InnerLengthRatio1000_Point { get; set; }
		public double InnerLengthRatio1000_Candidate { get; set; }

		public double HeaderCoreLengthRatio { get; set; }

		//public double WordsRatio { get; set; }
		//public double WordsRatio10_Point { get; set; }
		//public double WordsRatio10_Candidate { get; set; }
		public double WordsMatchRatioFromBeginning { get; set; }
		public double WordsMatchRatioFromEnd { get; set; }
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
