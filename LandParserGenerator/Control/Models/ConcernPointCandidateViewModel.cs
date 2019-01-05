using System;
using System.Collections.Generic;
using System.Linq;

using Land.Core.Parsing.Tree;
using Land.Core.Markup;

namespace Land.Control
{
	public class ConcernPointCandidateViewModel
	{
		public Node Node { get; set; }
		public string ViewHeader { get; set; }

		public ConcernPointCandidateViewModel(Node node)
		{
			Node = node;
			ViewHeader = String.Join(" ",
				PointContext.GetHeaderContext(node).Select(c => String.Join("", c.Value)));
		}

		public override string ToString()
		{
			return ViewHeader;
		}
	}
}
