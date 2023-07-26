using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Specification;

namespace Land.Core.Parsing.Tree
{
	public static class CustomBlockValidator
	{
		public class ValidatorVisitor: BaseTreeVisitor
		{
			public SegmentLocation BlockLocation { get; private set; }

			public bool? CanInsert { get; private set; }

			public ValidatorVisitor(SegmentLocation blockLocation)
			{
				BlockLocation = blockLocation;
			}

			public override void Visit(Node root)
			{
				CanInsert = false;

				/// Многострочный фрагмент можно оформить в виде ПБ, если он охватывает область, соответствующую корню
				if (BlockLocation.Includes(root.Location))
					CanInsert = true;
				else
					VisitInner(root);
			}

			public void VisitInner(Node node)
			{
				/// Потомки, вложенные в многострочную область или совпадающие с ней
				var included = node.Children.Where(c => BlockLocation.Includes(c.Location)).ToList();
				/// Потомки, строго пересекающиеся с многострочной областью
				var overlapped = node.Children.Where(c => BlockLocation.Overlaps(c.Location)).ToList();
				/// Потомки, охватывающие многострочную область, но не совпадающие с ней
				var outer = node.Children.Where(c => c.Location != null 
					&& c.Location.Includes(BlockLocation) && !c.Location.Equals(BlockLocation)).ToList();

				/// Если есть один потомок, охватывающий область, идём вглубь
				if (outer.Count == 1)
				{
					VisitInner(outer[0]);
				}
				/// Если есть один строго пересекающийся потомок, идём вглубь
				else if (overlapped.Count == 1 && included.Count == 0)
				{
					VisitInner(overlapped[0]);
				}
				/// Если многострочный фрагмент включает в себя целиком нескольких потомков и ни с кем больше не пересекается
				else if (included.Count > 0 && overlapped.Count == 0)
				{
					/// Если нет ситуации, когда выделили фрагмент, который включает в себя границу ПБ
					if (node.Symbol != Grammar.CUSTOM_BLOCK_RULE_NAME
						|| !included.Any(n => n.Symbol == Grammar.CUSTOM_BLOCK_START_TOKEN_NAME 
							|| n.Symbol == Grammar.CUSTOM_BLOCK_END_TOKEN_NAME))
					{
						CanInsert = true;
					}
				}
				else if (included.Count == 0 && overlapped.Count == 0)
				{
					CanInsert = true;
				}

			}
		}

		public static bool IsValid(Node root, SegmentLocation blockLocation)
		{
			var visitor = new ValidatorVisitor(blockLocation);
			visitor.Visit(root);

			return visitor.CanInsert.Value;
		}
	}
}
