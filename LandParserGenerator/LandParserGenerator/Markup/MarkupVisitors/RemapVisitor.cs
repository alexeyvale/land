using System;
using System.Linq;
using System.Collections.Generic;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public class RemapVisitor : BaseMarkupVisitor
	{
		private const double MIN_SIMILARITY = 0.4;

		private string FileName { get; set; }
		private Node TreeRoot { get; set; }

		public RemapVisitor(string fileName, Node root)
		{
			FileName = fileName;
			TreeRoot = root;
		}

		public override void Visit(ConcernPoint point)
		{
			if(point.Context.FileName == FileName)
			{
				var candidate = ContextFinder.Find(point.Context, FileName, TreeRoot).FirstOrDefault();

				if (candidate != null && candidate.Similarity > MIN_SIMILARITY)
				{
					point.Context = candidate.Context;
					point.TreeNode = candidate.Node;
				}
				else
				{
					point.TreeNode = null;
				}
			}
		}
	}
}
