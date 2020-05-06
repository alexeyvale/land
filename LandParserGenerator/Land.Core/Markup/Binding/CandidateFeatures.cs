using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
		public double SimH { get; set; }
		public double SimI { get; set; }
		public double SimA { get; set; }
		public double SimS { get; set; }
		#endregion

		#region Максимальные похожести каждого из контекстов
		public double MaxSimH { get; set; }
		public double MaxSimI { get; set; }
		public double MaxSimA { get; set; }
		public double MaxSimS { get; set; }
		#endregion

		#region Максимальные похожести в рамках того же контекста предков, что и у рассматриваемого кандидата
		public double MaxSimH_SameA { get; set; }
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
