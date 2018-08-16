using System;
using System.Collections.Generic;

namespace Land.Core.Markup
{
	/// <summary>
	/// Визитор для группировки точек привязки по файлам и идентификаторам узлов AST,
	/// к которым эти точки привязаны. Применяется при десериализации разметки
	/// для восстановления в точках ссылок на узлы
	/// </summary>
	public class GroupPointsVisitor : BaseMarkupVisitor
	{
		public Dictionary<string, Dictionary<int, List<ConcernPoint>>> Points { get; set; }
			= new Dictionary<string, Dictionary<int, List<ConcernPoint>>>();

		public override void Visit(ConcernPoint point)
		{
			if (point.TreeNodeId.HasValue)
			{
				if (!Points.ContainsKey(point.FileName))
				{
					Points[point.FileName] = new Dictionary<int, List<ConcernPoint>>();
				}

				if (!Points[point.FileName].ContainsKey(point.TreeNodeId.Value))
				{
					Points[point.FileName][point.TreeNodeId.Value] = new List<ConcernPoint>();
				}

				Points[point.FileName][point.TreeNodeId.Value].Add(point);
			}
		}
	}
}
