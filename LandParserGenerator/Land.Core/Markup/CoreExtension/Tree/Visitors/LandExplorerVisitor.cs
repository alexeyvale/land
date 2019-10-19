using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Parsing.Tree;

namespace Land.Markup.CoreExtension
{
	public class LandExplorerVisitor : BaseTreeVisitor
	{
		public List<Node> Land { get; set; } = new List<Node>();
		public List<Node> HighLevelLand { get; set; } = new List<Node>();

		private bool CameFromLand { get; set; } = false;

		public override void Visit(Node node)
		{
			/// Индикатор того, является ли данный узел островом,
			/// не вложенным в другие острова
			bool isHighLevelLand = false;

			if (node.Options.IsSet(MarkupOption.LAND))
			{
				Land.Add(node);
				if (!CameFromLand)
				{
					HighLevelLand.Add(node);
					isHighLevelLand = true;
					CameFromLand = true;
				}
			}

			base.Visit(node);

			if (isHighLevelLand)
				CameFromLand = false;
		}
	}
}
