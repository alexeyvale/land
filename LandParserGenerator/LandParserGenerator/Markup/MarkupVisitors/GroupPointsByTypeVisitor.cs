using System;
using System.Collections.Generic;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	/// <summary>
	/// Визитор для группировки точек привязки по типам узлов,
	/// к которым эти точки привязаны.
	/// </summary>
	public class GroupPointsByTypeVisitor : BaseMarkupVisitor
	{
		public Dictionary<string, List<ConcernPoint>> Grouped { get; set; }
			= new Dictionary<string, List<ConcernPoint>>();

		public override void Visit(ConcernPoint point)
		{
			if (!Grouped.ContainsKey(point.Context.NodeType))
			{
				Grouped[point.Context.NodeType] = new List<ConcernPoint>();
			}

			Grouped[point.Context.NodeType].Add(point);
		}

		public static Dictionary<string, List<ConcernPoint>> GetGroups(IEnumerable<MarkupElement> roots)
		{
			var visitor = new GroupPointsByTypeVisitor();

			foreach (var root in roots)
				root.Accept(visitor);

			return visitor.Grouped;
		}
	}
}
