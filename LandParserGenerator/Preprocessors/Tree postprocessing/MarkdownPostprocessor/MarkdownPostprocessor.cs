using System;
using System.Collections.Generic;

using Land.Core;
using Land.Core.Parsing.Preprocessing;
using Land.Core.Parsing.Tree;

namespace MarkdownPreprocessing.TreePostprocessing
{
	public class MarkdownPostprocessor : BasePreprocessor
	{
		public override void Postprocess(Node root, List<Message> log)
		{
			if (root != null)
			{
				var visitor = new SectionsHierarchyVisitor(NodeGenerator);
				root.Accept(visitor);
				Log.AddRange(visitor.Log);
			}

			foreach (var rec in Log)
				rec.Source = this.GetType().FullName;	
		}

		public override string Preprocess(string text, out bool success)
		{
			success = true;
			return text;
		}
	}
}
