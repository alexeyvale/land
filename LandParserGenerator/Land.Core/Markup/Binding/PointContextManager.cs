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

		public void ClearCache(string fileName)
		{
			var keysToRemove = Cache.Where(e => e.Value.FileContext.Name == fileName)
				.Select(e=>e.Key).ToList();

			foreach(var key in keysToRemove)
			{
				Cache.Remove(key);
			}
		}

		public List<FileContext> GetFileContexts() =>
			Cache.Values.Select(e => e.FileContext).Distinct().ToList();
	}
}
