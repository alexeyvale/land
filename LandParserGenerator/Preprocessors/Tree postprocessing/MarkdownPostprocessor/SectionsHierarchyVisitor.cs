using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Land.Core;
using Land.Core.Specification;
using Land.Core.Parsing.Tree;

namespace MarkdownPreprocessing.TreePostprocessing
{
	public class SectionsHierarchyVisitor : BaseTreeVisitor
	{
		private const string SECTION_RULE_NAME = "section";

		public List<Message> Log { get; set; } = new List<Message>();
		private BaseNodeGenerator NodeGenerator { get; set; }

		private int CurrentLevel { get; set; } = 0;
		private Node ParentSection { get; set; }
		private PointLocation PossibleEnd { get; set; }

		public SectionsHierarchyVisitor(BaseNodeGenerator nodeGenerator)
		{
			NodeGenerator = nodeGenerator;
		}

		public override void Visit(Node node)
		{
			var level = 0;

			switch (node.Type)
			{
				case "file":
					ParentSection = NodeGenerator.Generate("file", node.Options);
					base.Visit(node);

					for (; level < CurrentLevel; --CurrentLevel)
					{
						ParentSection.SetLocation(ParentSection.Location.Start, node.Location.End);
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
					PossibleEnd = node.Location.End;
					return;
			}

			/// Если текущий заголовок - заголовок более низкого уровня
			if (level > CurrentLevel)
			{
				Node newNode = null;

				/// опускаемся до него
				for (; CurrentLevel < level; ++CurrentLevel)
				{
					newNode = NodeGenerator.Generate(SECTION_RULE_NAME);
					newNode.SetLocation(node.Location.Start, node.Location.End);

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
						ParentSection.SetLocation(ParentSection.Location.Start, PossibleEnd);
					ParentSection = ParentSection.Parent;
				}
				PossibleEnd = null;

				var newNode = NodeGenerator.Generate(SECTION_RULE_NAME);
				newNode.SetLocation(node.Location.Start, node.Location.End);
				newNode.AddLastChild(node);

				ParentSection.AddLastChild(newNode);
				ParentSection = newNode;
				++CurrentLevel;
			}
		}
	}
}
