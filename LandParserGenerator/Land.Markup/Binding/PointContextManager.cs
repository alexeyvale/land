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
		private Dictionary<string, Dictionary<Node, List<AncestorsContextElement>>> Ancestors { get; set; } = new Dictionary<string, Dictionary<Node, List<AncestorsContextElement>>>();

		public PointContext GetContext(
			Node node,
			ParsedFile file)
		{
			if (!Cache.ContainsKey(node))
			{
				var ancestor = PointContext.GetAncestor(node);

				var cachedAncestorsContext = ancestor != null && Ancestors.ContainsKey(file.Name) && Ancestors[file.Name].ContainsKey(ancestor)
					? Ancestors[file.Name][ancestor] : null;

				Cache[node] = PointContext.GetCoreContext(node, file, cachedAncestorsContext);

				if (cachedAncestorsContext == null && ancestor != null)
				{
					if (!Ancestors.ContainsKey(file.Name))
					{
						Ancestors[file.Name] = new Dictionary<Node, List<AncestorsContextElement>>();
					}

					Ancestors[file.Name][ancestor] = Cache[node].AncestorsContext;
				}
			}

			return Cache[node];
		}

		public PointContext GetContext(
			Node node,
			ParsedFile file,
			SiblingsConstructionArgs siblingsArgs,
			ClosestConstructionArgs closestArgs,
			SearchScopeConstructionArgs searchScopeArgs)
		{
			return PointContext.GetExtendedContext(
				node, 
				file, 
				siblingsArgs, 
				closestArgs,
				searchScopeArgs,
				GetContext(node, file)
			);
		}

		public void ClearCache(string fileName)
		{
			var keysToRemove = Cache
				.Where(e => e.Value.FileName == fileName)
				.Select(e => e.Key)
				.ToList();

			foreach (var key in keysToRemove)
			{
				Cache.Remove(key);
			}

			if (Ancestors.ContainsKey(fileName))
			{
				Ancestors.Remove(fileName);
			}
		}
	}
}
