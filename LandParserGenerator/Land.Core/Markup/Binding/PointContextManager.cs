using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Land.Core.Parsing.Tree;

namespace Land.Markup.Binding
{
	public class PointContextManager
	{
		private Dictionary<Node, PointContext> Cache { get; set; } = new Dictionary<Node, PointContext>();

		public void CollectGarbage()
		{
			var keysToDispose = Cache.Where(c => c.Value.LinksCounter <= 0).Select(c => c.Key).ToList();

			foreach (var key in keysToDispose)
				Cache.Remove(key);
		}

		public PointContext GetContext(
			Node node,
			ParsedFile file)
		{
			if (!Cache.ContainsKey(node))
			{
				Cache[node] = PointContext.GetCoreContext(node, file);
			}

			return Cache[node];
		}

		public PointContext GetContext(
			Node node,
			ParsedFile file,
			List<ParsedFile> searchArea,
			Func<string, ParsedFile> getParsed,
			ContextFinder contextFinder)
		{
			if (!Cache.ContainsKey(node))
			{
				return Cache[node] = PointContext
					.GetFullContext(node, file, searchArea, getParsed, contextFinder);
			}
			else
			{
				return PointContext
					.GetFullContext(node, file, searchArea, getParsed, contextFinder, Cache[node]);
			}
		}
	}
}
