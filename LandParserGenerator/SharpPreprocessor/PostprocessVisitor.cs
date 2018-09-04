using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Land.Core;
using Land.Core.Parsing;
using Land.Core.Parsing.Tree;

using sharp_preprocessor;

namespace SharpPreprocessor
{
	internal class PostprocessVisitor : BaseTreeVisitor
	{
		private List<Segment> SkippedSegments { get; set; }

		private int LastSum { get; set; } = 0;
		private int LastIdx { get; set; } = 0;

		public PostprocessVisitor(List<Segment> segments)
		{
			SkippedSegments = segments;
		}

		public override void Visit(Node node)
		{
			if (node.Children.Count > 0)
			{
				node.ResetAnchor();
			}
			else
			{
				if (node.StartOffset.HasValue)
				{
					var start = node.StartOffset.Value + LastSum;

					while (LastIdx < SkippedSegments.Count && SkippedSegments[LastIdx].StartOffset <= start)
					{
						LastSum += SkippedSegments[LastIdx].Length;
						start += SkippedSegments[LastIdx].Length;
						LastIdx += 1;
					}

					node.SetAnchor(start, node.EndOffset.Value + LastSum);
				}
			}

			base.Visit(node);
		}
	}
}
