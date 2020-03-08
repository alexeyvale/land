using System;
using System.Collections.Generic;

namespace Land.Markup.Tree
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
			if (!Grouped.ContainsKey(point.Context.Type))
			{
				Grouped[point.Context.Type] = new List<ConcernPoint>();
			}

			Grouped[point.Context.Type].Add(point);
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
