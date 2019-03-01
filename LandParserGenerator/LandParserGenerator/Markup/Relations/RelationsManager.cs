using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Land.Core.Markup
{
	public class RelationNotification
	{
		public RelatedPair<MarkupElement> Pair { get; set; }

		public string Message { get; set; }
	}

	public class RelationsManager
	{
		public RelationsCache InternalRelations { get; private set; } = new RelationsCache(RelationGroup.Internal);

		public RelationsCache ExternalRelations { get; private set; } = new RelationsCache(RelationGroup.External);

		/// <summary>
		/// Признак соответствия кеша текущей разметке
		/// </summary>
		public bool IsValid { get; set; } = false;

		/// <summary>
		/// Проверка того, что элементы состоят в указанном отношении
		/// </summary>
		public bool AreRelated(MarkupElement a, MarkupElement b, RelationType relation) => relation.GetGroup() == RelationGroup.Internal 
			? InternalRelations.AreRelated(a, b, relation) : ExternalRelations.AreRelated(a, b, relation);

		public void RefreshElements(IEnumerable<MarkupElement> markup)
		{
			var elements = GetLinearSequenceVisitor.GetElements(markup);

			InternalRelations.SetElements(elements);
			ExternalRelations.RefreshElements(elements);

			IsValid = false;
		}

		public void RefreshCache(IEnumerable<MarkupElement> markup)
		{
			//var elements = GetLinearSequenceVisitor.GetElements(markup);
			//var concerns = elements.OfType<Concern>().ToList();
			//var points = elements.OfType<ConcernPoint>().ToList();

			//InternalRelations.SetElements(elements);
			//ExternalRelations.RefreshElements(elements);

			///// Строим отношения иерархической принадлежности точек и функциональностей объемлющим функциональностям
			//foreach (var concern in concerns)
			//	InternalRelations.AddRelation(RelationType.IsLogicalParentOf, concern, concern.Elements);

			//InternalRelations.FillAsClosure(RelationType.IsLogicalAncestorOf, RelationType.IsLogicalParentOf);
			//InternalRelations.FillAsTransposition(RelationType.IsLogicalChildOf, RelationType.IsLogicalParentOf);
			//InternalRelations.FillAsTransposition(RelationType.IsLogicalDescendantOf, RelationType.IsLogicalAncestorOf);

			///// Строим отношения, связанные с пространственным взаимным расположением точек
			//for (var i = 0; i < points.Count; ++i)
			//	for (var j = i + 1; j < points.Count; ++j)
			//	{
			//		var point1 = points[i];
			//		var point2 = points[j];

			//		if (point1.Location.End.Offset <= point2.Location.Start.Offset)
			//			InternalRelations.AddRelation(RelationType.Preceeds, point1, point2);
					
			//		if (point1.Location.Includes(point2.Location))
			//			InternalRelations.AddRelation(RelationType.IsPhysicalDescendantOf, point2, point1);

			//		if (point2.Location.Includes(point1.Location))
			//			InternalRelations.AddRelation(RelationType.IsPhysicalDescendantOf, point1, point2);
			//	}

			//InternalRelations.FillAsTransposition(RelationType.Follows, RelationType.Preceeds);
			//InternalRelations.FillAsTransposition(RelationType.IsPhysicalAncestorOf, RelationType.IsPhysicalDescendantOf);

			///// Строим отношение, связывающее точки, помечающие одну и ту же сущность
			//var grouped = points.GroupBy(p => p.AstNode);

			//foreach(var group in grouped)
			//{
			//	foreach(var point1 in group)
			//		foreach(var point2 in group)
			//		{
			//			if(point1 != point2)
			//				InternalRelations.AddRelation(RelationType.MarksTheSameAs, point2, point1);
			//		}
			//}

			//IsValid = true;
		}

		public HashSet<RelationType> GetPossibleExternalRelations(MarkupElement from, MarkupElement to)
		{
			var result = new HashSet<RelationType>();

			if(from is ConcernPoint)
			{
				/// CP CP
				if(to is ConcernPoint)
				{
					result.Add(RelationType.Uses);

					if (MustPreceedConstraint(from, to))
					{
						result.Add(RelationType.MustPreceed);
					}
				}
				/// CP C
				else
				{
					if (ExistsIfConstraint(from, to))
					{
						result.Add(RelationType.ExistsIfAll);
						result.Add(RelationType.ExistsIfAny);
					}
				}
			}
			else
			{
				/// C CP
				if (to is ConcernPoint)
				{
				}
				/// C C
				else
				{
					result.Add(RelationType.Modifies);
				}
			}

			return result;
		}

		private bool MustPreceedConstraint(MarkupElement point1, MarkupElement point2) => 
			InternalRelations.AreRelated(point1, point2, RelationType.Preceeds);

		private bool ExistsIfConstraint(MarkupElement point, MarkupElement concern) =>
			InternalRelations.GetRelated(concern, RelationType.IsLogicalAncestorOf)
				.OfType<ConcernPoint>().Any(p=> InternalRelations.GetRelated(p, RelationType.MarksTheSameAs).Contains(point));

		public void AddExternalRelation(RelationType type, MarkupElement from, MarkupElement to)
		{
			switch(type)
			{
				case RelationType.ExistsIfAll:
				case RelationType.ExistsIfAny:
					ExternalRelations.AddRelation(type, from, to);

					foreach (var point in InternalRelations.GetRelated(from, RelationType.MarksTheSameAs))
						ExternalRelations.AddRelation(type, point, to);
					break;
				case RelationType.MustPreceed:
					foreach (var point1 in InternalRelations.GetRelated(from, RelationType.MarksTheSameAs))
						foreach (var point2 in InternalRelations.GetRelated(to, RelationType.MarksTheSameAs))
							ExternalRelations.AddRelation(type, point1, point2);
					break;
				default:
					ExternalRelations.AddRelation(type, from, to);
					break;
			}
		}

		public void RemoveExternalRelation(RelationType type, MarkupElement from, MarkupElement to)
		{
			ExternalRelations.RemoveRelation(type, from, to);

			switch (type)
			{
				case RelationType.ExistsIfAll:
				case RelationType.ExistsIfAny:
					foreach (var point in InternalRelations.GetRelated(from, RelationType.MarksTheSameAs))
						ExternalRelations.AddRelation(type, point, to);
					break;
			}
		}

		public List<RelationNotification> CheckConsistency()
		{
			var notifications = new List<RelationNotification>();
			
			if(IsValid)
			{
				var pairs = ExternalRelations.GetRelatedPairs();

				foreach (var pair in pairs.Where(p=>p.RelationType == RelationType.MustPreceed))
				{
					if(!MustPreceedConstraint(pair.Item0, pair.Item1))
					{
						notifications.Add(new RelationNotification()
						{
							Pair = pair,
							Message = "Элементы не могут быть связаны отношением \"Должен предшествовать\""
						});
					}

					if (!ExistsIfConstraint(pair.Item0, pair.Item1))
					{
						notifications.Add(new RelationNotification()
						{
							Pair = pair,
							Message = "Элементы не могут быть связаны отношением зависимости существования"
						});
					}
				}
			}
			else
			{
				notifications.Add(new RelationNotification()
				{
					Message = "Отношения не согласованы с текущей разметкой"
				});
			}

			return notifications;
		}
	}
}
