using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public interface IRemapCandidateInfo
	{
		Node Node { get; set; }
		PointContext Context { get; set; }

		double? HeaderSimilarity { get; set; }
		double? AncestorSimilarity { get; set; }
		double? InnerSimilarity { get; set; }
		double? SiblingsSimilarity { get; set; }

		double? Similarity { get; }

		bool IsAuto { get; set; }
	}
}
