using System;
using System.Linq;
using System.Collections.Generic;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public class RemapVisitor : BaseMarkupVisitor
	{
		private const double MIN_SIMILARITY = 0.4;

		private MarkupTargetInfo TargetInfo { get; set; }

		public RemapVisitor(string fileName, Tuple<Node, string> parsed)
		{
			TargetInfo = new MarkupTargetInfo()
			{
				FileName = fileName,
				FileText = parsed.Item2,
				TargetNode = parsed.Item1
			};
		}

		public override void Visit(ConcernPoint point)
		{
			if(point.Context.FileName == TargetInfo.FileText)
			{
				var candidate = ContextFinder.Find(point.Context, TargetInfo).FirstOrDefault();

				if (candidate != null && candidate.Similarity > MIN_SIMILARITY)
				{
					point.Context = candidate.Context;
					point.Location = candidate.Node.Anchor;
				}
				else
				{
					point.Location = null;
				}
			}
		}
	}
}
