using System;
using System.Collections.Generic;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public class GetPointsVisitor : BaseMarkupVisitor
	{
		public List<ConcernPoint> Points { get; set; }
			= new List<ConcernPoint>();

		public override void Visit(ConcernPoint point)
		{
			Points.Add(point);
		}

		public static List<ConcernPoint> GetPoints(IEnumerable<MarkupElement> roots)
		{
			var visitor = new GetPointsVisitor();

			foreach (var root in roots)
				root.Accept(visitor);

			return visitor.Points;
		}
	}
}
