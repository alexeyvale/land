using System;
using System.Collections.Generic;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	/// <summary>
	/// Визитор для группировки точек привязки по файлам,
	/// к которым эти точки привязаны.
	/// </summary>
	public class GroupPointsByFileVisitor : BaseMarkupVisitor
	{
		public Dictionary<string, List<ConcernPoint>> Grouped { get; set; }
			= new Dictionary<string, List<ConcernPoint>>();

		public override void Visit(ConcernPoint point)
		{
			if (!Grouped.ContainsKey(point.Context.FileName))
			{
				Grouped[point.Context.FileName] = new List<ConcernPoint>();
			}

			Grouped[point.Context.FileName].Add(point);
		}

		public static Dictionary<string, List<ConcernPoint>> GetGroups(IEnumerable<MarkupElement> roots)
		{
			var visitor = new GroupPointsByFileVisitor();

			foreach (var root in roots)
				root.Accept(visitor);

			return visitor.Grouped;
		}
	}
}
