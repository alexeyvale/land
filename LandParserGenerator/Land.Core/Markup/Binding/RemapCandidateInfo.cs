using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Parsing.Tree;

namespace Land.Markup.Binding
{
	public class RemapCandidateInfo
	{
		public Node Node { get; set; }
		public ParsedFile File { get; set; }
		public PointContext Context { get; set; }

		public double HeaderSimilarity { get; set; }
		public double AncestorSimilarity { get; set; }
		public double InnerSimilarity { get; set; }
		public double SiblingsSimilarity { get; set; }

		public double? Similarity { get; set; }

		public bool IsAuto { get; set; }

		public string DebugInfo { get; set; }

		public override string ToString()
		{
			return $"{String.Format("{0:f4}", Similarity)} [H: {String.Format("{0:f2}", HeaderSimilarity)}; A: {String.Format("{0:f2}", AncestorSimilarity)}; I: {String.Format("{0:f2}", InnerSimilarity)}]";
		}
	}
}
