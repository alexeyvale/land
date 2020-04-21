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
		public double HeaderSim { get; set; }
		public double InnerSim { get; set; }
		public double AncestorsSim { get; set; }

		public double MaxAncestorsSim { get; set; }
		public double MaxHeaderSimGlobal { get; set; }
		public double MaxInnerSimGlobal { get; set; }
		public double MaxHeaderSimSameAncestors { get; set; }
		public double MaxInnerSimSameAncestors { get; set; }

		public double MoreSimilarAncestorsRatio { get; set; }
		public double SameAncestorsRatio { get; set; }
		public double MoreSimilarHeaderSameAncestorsRatio { get; set; }
		public double MoreSimilarInnerSameAncestorsRatio { get; set; }

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
