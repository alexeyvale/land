using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public class RemapCandidateInfo
	{
		private const double HeaderContextWeight = 1;
		private const double AncestorsContextWeight = 0.5;
		private const double InnerContextWeight = 0.6;
		private const double SiblingsContextWeight = 0.4;

		public Node Node { get; set; }
		public PointContext Context { get; set; }

		public double? HeaderSimilarity { get; set; }
		public double? AncestorSimilarity { get; set; }
		public double? InnerSimilarity { get; set; }
		public double? SiblingsSimilarity { get; set; }

		public double Similarity => 
			((HeaderSimilarity ?? 1) * HeaderContextWeight 
			+ (AncestorSimilarity ?? 1) * AncestorsContextWeight
			+ (InnerSimilarity ?? 1) * InnerContextWeight)
			/ (HeaderContextWeight + AncestorsContextWeight + InnerContextWeight);

		public override string ToString()
		{
			return $"{String.Format("{0:f4}", Similarity)} [H: {String.Format("{0:f2}", HeaderSimilarity)}; A: {String.Format("{0:f2}", AncestorSimilarity)}; I: {String.Format("{0:f2}", InnerSimilarity)}]";
		}
	}
}
