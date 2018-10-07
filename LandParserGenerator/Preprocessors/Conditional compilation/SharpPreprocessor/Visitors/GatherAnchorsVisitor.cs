using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Land.Core;
using Land.Core.Parsing.Tree;

namespace SharpPreprocessing.ConditionalCompilation
{
	/// <summary>
	/// Визитор собирает координаты по всем узлам и сбрасывает предвычисленные
	/// координаты для нелистовых узлов
	/// </summary>
	internal class GatherAnchorsVisitor : BaseTreeVisitor
	{
		public List<PointLocation> Locations { get; set; } = new List<PointLocation>();

		public override void Visit(Node node)
		{
			/// У нелистового узла сбрасываем якорь, его нужно перевычислить
			/// после правки якорей листьев-потомков
			if (node.Children.Count > 0)
			{
				node.ResetAnchor();
			}
			else
			{
				if (node.Anchor != null)
				{
					Locations.Add(node.Anchor.Start);
					Locations.Add(node.Anchor.End);
				}
			}

			base.Visit(node);
		}
	}
}
