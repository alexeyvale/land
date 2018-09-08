using System;
using System.Collections.Generic;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	/// <summary>
	/// Визитор для группировки точек привязки по файлам,
	/// к которым эти точки привязаны.
	/// </summary>
	public class GroupPointsVisitor : BaseMarkupVisitor
	{
		public Dictionary<string, HashSet<ConcernPoint>> Points { get; set; }
			= new Dictionary<string, HashSet<ConcernPoint>>();

		public override void Visit(ConcernPoint point)
		{
			if (!Points.ContainsKey(point.Context.FileName))
			{
				Points[point.Context.FileName] = new HashSet<ConcernPoint>();
			}

			Points[point.Context.FileName].Add(point);
		}
	}
}
