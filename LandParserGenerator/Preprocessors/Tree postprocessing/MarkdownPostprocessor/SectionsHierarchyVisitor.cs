using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Land.Core;
using Land.Core.Parsing.Tree;

namespace MarkdownPreprocessing.TreePostprocessing
{
	public class SectionsHierarchyVisitor : BaseTreeVisitor
	{
		public List<Message> Log { get; set; } = new List<Message>();

		private int CurrentLevel { get; set; } = 0;
		private Node ParentSection { get; set; }
		private PointLocation PossibleEnd { get; set; }

		public override void Visit(Node node)
		{
			var level = 0;

			switch (node.Type)
			{
				case "file":
					ParentSection = new Node("file", node.Options);
					base.Visit(node);

					for (; level < CurrentLevel; --CurrentLevel)
					{
						ParentSection.SetAnchor(ParentSection.Anchor.Start, node.Anchor.End);
						ParentSection = ParentSection.Parent;
					}
					node.CopyFromNode(ParentSection);
					return;
				case "header_h1":
					level = 1;
					break;
				case "header_h2":
					level = 2;
					break;
				case "header_any":
					level = node.Value[0].TakeWhile(c => c == '#').Count();
					break;
				default:
					PossibleEnd = node.Anchor.End;
					return;
			}

			/// Если текущий заголовок - заголовок более низкого уровня
			if (level > CurrentLevel)
			{
				Node newNode = null;

				/// опускаемся до него
				for (; CurrentLevel < level; ++CurrentLevel)
				{
					newNode = new Node("section", new LocalOptions() { IsLand = true });
					newNode.SetAnchor(node.Anchor.Start, node.Anchor.End);

					ParentSection.AddLastChild(newNode);
					ParentSection = newNode;
				}

				newNode.AddLastChild(node);
			}
			/// иначе, если уровень более высокий, поднимаемся,
			/// закрывая более вложенные секции и секцию на том же уровне
			else if (level <= CurrentLevel)
			{
				for (; level <= CurrentLevel; --CurrentLevel)
				{
					if (PossibleEnd != null)
						ParentSection.SetAnchor(ParentSection.Anchor.Start, PossibleEnd);
					ParentSection = ParentSection.Parent;
				}
				PossibleEnd = null;

				var newNode = new Node("section", new LocalOptions() { IsLand = true });
				newNode.SetAnchor(node.Anchor.Start, node.Anchor.End);
				newNode.AddLastChild(node);

				ParentSection.AddLastChild(newNode);
				ParentSection = newNode;
				++CurrentLevel;
			}
		}
	}
}
