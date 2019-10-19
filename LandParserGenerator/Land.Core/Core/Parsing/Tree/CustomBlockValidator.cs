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

				/// Блок можно вставить как новый корень, если он 
				/// охватывает область, соответствующую корню, или если корень - это Any
				if (BlockLocation.Includes(root.Location) || root.Symbol == Grammar.ANY_TOKEN_NAME)
					CanInsert = true;
				else
					VisitInner(root);
			}

			public void VisitInner(Node node)
			{
				/// Потомки, вложенные в область блока или совпадающие с ним
				var included = node.Children.Where(c => BlockLocation.Includes(c.Location)).ToList();
				/// Потомки, строго пересекающиеся с блоком
				var overlapped = node.Children.Where(c => BlockLocation.Overlaps(c.Location)).ToList();
				/// Потомки, охватывающие область блока, но не совпадающие с ним
				var outer = node.Children.Where(c => c.Location != null 
					&& c.Location.Includes(BlockLocation) && !c.Location.Equals(BlockLocation)).ToList();

				if (outer.Count == 1)
				{
					/// Если блок вложен в Any, его можно сделать 
					/// дочерним по отношению к Any
					if (outer[0].Symbol == Grammar.ANY_TOKEN_NAME
						&& outer[0].Children.Count == 0)
						CanInsert = true;
					else
						VisitInner(outer[0]);
				}
				else if (overlapped.Count == 1 && included.Count == 0)
				{
					/// Если блок перекрывает Any и ничего не включает в себя,
					/// его можно сделать дочерним по отношению к Any
					if (overlapped[0].Symbol == Grammar.ANY_TOKEN_NAME
						&& overlapped[0].Children.Count == 0)
						CanInsert = true;
					/// В противном случае смотрим на структуру того, с чем есть перекрытие,
					/// при этом пользовательский блок не может перекрываться с пользовательским блоком
					else if(overlapped[0].Symbol != Grammar.CUSTOM_BLOCK_RULE_NAME)
						VisitInner(overlapped[0]);
				}
				/// Если блок включает в себя некоторое количество 
				/// дочерних узлов текущего узла и ничего не перекрывает,
				/// им можно заменить эти дочерние узлы;
				/// если блок вообще ничего не перекрывает и ни с чем не пересекается,
				/// его можно вставить между дочерними узлами;
				/// при этом не допускается включение в блок
				/// границы другого блока
				else if (included.Count > 0 && overlapped.Count == 0 &&
						(node.Symbol != Grammar.CUSTOM_BLOCK_RULE_NAME ||
							!included.Any(n => n.Location.Start.Line == node.Location.Start.Line) &&
							!included.Any(n => n.Location.Start.Line == node.Location.End.Line))
						|| included.Count == 0 && overlapped.Count == 0)
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
