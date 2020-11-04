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
			ParsedFile file,
			Dictionary<string, List<ushort>> structureCodes)
		{
			if (!Cache.ContainsKey(node))
			{
				Cache[node] = PointContext.GetCoreContext(node, file, structureCodes);
			}

			return Cache[node];
		}

		public PointContext GetContext(
			Node node,
			ParsedFile file,
			Dictionary<string, List<ushort>> structureCodes,
			SiblingsConstructionArgs siblingsArgs,
			ClosestConstructionArgs closestArgs)
		{
			if (!Cache.ContainsKey(node))
			{
				return Cache[node] = PointContext
					.GetExtendedContext(node, file, structureCodes, siblingsArgs, closestArgs);
			}
			else
			{
				return PointContext.GetExtendedContext(
					node, file, structureCodes, siblingsArgs, closestArgs, Cache[node]
				);
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
