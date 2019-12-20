using System;
using System.Collections.Generic;
using System.Linq;

using Land.Core;
using Land.Core.Parsing.Tree;
using Land.Markup.Binding;

namespace Land.Control
{
	public abstract class ConcernPointCandidate
	{
		public string ViewHeader { get; set; }

		public override string ToString()
		{
			return ViewHeader;
		}
	}

	public class ExistingConcernPointCandidate : ConcernPointCandidate
	{
		public Node Node { get; set; }

		public ExistingConcernPointCandidate(Node node)
		{
			Node = node;
			ViewHeader = String.Join(" ",
				ContextCore.GetHeaderContext(node).Select(c => String.Join("", c.Value))
			);
		}
	}

	public class CustomConcernPointCandidate : ConcernPointCandidate
	{
		public SegmentLocation RealSelection { get; set; }
		public SegmentLocation AdjustedSelection { get; set; }

		public CustomConcernPointCandidate(SegmentLocation realSelection, 
			SegmentLocation adjustedSelection, string viewHeader)
		{
			RealSelection = realSelection;
			AdjustedSelection = adjustedSelection;
			ViewHeader = viewHeader;
		}
	}
}
