using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public class RelationPropertiesAttribute : Attribute
	{
		public bool IsReflexive { get; private set; }
		public bool IsSymmetric { get; private set; }
		public bool IsTransitive { get; private set; }

		public RelationPropertiesAttribute(bool isReflexive, bool isSymmetric, bool isTransitive)
		{
			IsReflexive = isReflexive;
			IsSymmetric = isSymmetric;
			IsTransitive = isTransitive;
		}
	}

	public enum RelationGroup
	{
		[Description("Пространственные")]
		Spatial,
		[Description("Теоретико-множественные")]
		Set,
		[Description("Семантические")]
		Semantic
	}

	public enum RelationType
	{
		[Description("Предшествует в пределах объемлющей сущности")]
		[RelationProperties(isReflexive:false, isSymmetric: false, isTransitive: true)]
		Spatial_Preceeds,

		[Description("Следует за в пределах объемлющей сущности")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true)]
		Spatial_Follows,

		[Description("Содержатся в пределах одной непосредственно объемлющей сущности")]
		[RelationProperties(isReflexive: true, isSymmetric: true, isTransitive: true)]
		Spatial_IsSiblingOf,

		[Description("Включает в себя")]
		[RelationProperties(isReflexive: true, isSymmetric: false, isTransitive: true)]
		Spatial_Includes,

		[Description("Код пересекается с кодом")]
		[RelationProperties(isReflexive: true, isSymmetric: true, isTransitive: false)]
		Spatial_Intersects,


		[Description("Включает в себя")]
		[RelationProperties(isReflexive: true, isSymmetric: false, isTransitive: true)]
		Set_Includes,

		[Description("Пересекается с")]
		[RelationProperties(isReflexive: true, isSymmetric: true, isTransitive: false)]
		Set_Intersects,


		[Description("Использует")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false)]
		Semantic_Uses,

		[Description("Модифицирует")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false)]
		Semantic_Modifies
	}

	public class RelationsManager
	{
		private Dictionary<RelationType, Dictionary<MarkupElement, HashSet<MarkupElement>>> Cache { get; set; } = 
			new Dictionary<RelationType, Dictionary<MarkupElement, HashSet<MarkupElement>>>();
		
		public HashSet<MarkupElement> this[RelationType relType, MarkupElement elem]
		{
			get
			{
				return !Cache.ContainsKey(relType) || Cache[relType].ContainsKey(elem)
					? null
					: Cache[relType][elem];
			}
		}

		public Dictionary<MarkupElement, HashSet<MarkupElement>> this[RelationType relType] 
			=> !Cache.ContainsKey(relType)? null : Cache[relType];


		/// Убираем из кеша информацию об определённых группах отношений
		public void Clear(params RelationGroup[] groups)
		{
			foreach (RelationType relType in ((RelationType[])Enum.GetValues(typeof(RelationType)))
				.Where(e=>groups.Select(g=>g.ToString()).Contains(e.ToString().Split('_')[0])))
			{
				Cache[relType] = null;
			}
		}

		public void AddRelation(RelationType type, MarkupElement from, MarkupElement to)
		{
			if (!Cache[type].ContainsKey(from))
				Cache[type][from] = new HashSet<MarkupElement>();

			Cache[type][from].Add(to);

			if (type.GetAttribute<RelationPropertiesAttribute>().IsSymmetric)
			{
				if (!Cache[type].ContainsKey(to))
					Cache[type][to] = new HashSet<MarkupElement>();

				Cache[type][to].Add(from);
			}
		}

		public void BuildRelations(IEnumerable<MarkupElement> markup)
		{
			BuildSpatialRelations(markup);
			BuildSetRelations(markup);
		}

		private void BuildSpatialRelations(IEnumerable<MarkupElement> markup)
		{
			Clear(RelationGroup.Spatial);

			var elements = GetLinearSequenceVisitor.GetElements(markup);

			foreach (RelationType relType in ((RelationType[])Enum.GetValues(typeof(RelationType)))
						.Where(t => t.ToString().StartsWith(RelationGroup.Spatial.ToString())))
			{
				Cache[relType] = elements.ToDictionary(c => c, c => new HashSet<MarkupElement>());
			}

			for (var i = 0; i < elements.Count; ++i)
				for (var j = i + 1; j < elements.Count; ++j)
				{
					/// Если имеем дело с двумя точками
					if (elements[i] is ConcernPoint point1 && elements[j] is ConcernPoint point2)
					{
						if (point1.Spatial_Preceeds(point2))
						{
							Cache[RelationType.Spatial_IsSiblingOf][point1].Add(point2);
							Cache[RelationType.Spatial_IsSiblingOf][point2].Add(point1);
							Cache[RelationType.Spatial_Preceeds][point1].Add(point2);
							Cache[RelationType.Spatial_Follows][point2].Add(point1);
						}
						else if(point1.Spatial_IsSiblingOf(point2))
						{
							Cache[RelationType.Spatial_IsSiblingOf][point1].Add(point2);
							Cache[RelationType.Spatial_IsSiblingOf][point2].Add(point1);

							if(point1 != point2)
							{
								Cache[RelationType.Spatial_Preceeds][point2].Add(point1);
								Cache[RelationType.Spatial_Follows][point1].Add(point2);
							}
						}

						if(point1.Spatial_Intersects(point2))
						{
							Cache[RelationType.Spatial_Intersects][point1].Add(point2);
							Cache[RelationType.Spatial_Intersects][point2].Add(point1);

							if(point1.Spatial_Includes(point2))
							{
								Cache[RelationType.Spatial_Includes][point1].Add(point2);
							}
							else if(point2.Spatial_Includes(point1))
							{
								Cache[RelationType.Spatial_Includes][point2].Add(point1);
							}
						}
					}

					if((elements[i] is Concern ^ elements[j] is Concern) 
						&& (elements[i] is ConcernPoint ^ elements[j] is ConcernPoint))
					{
						var concern = elements[i] as Concern ?? elements[j] as Concern;
						var point = elements[i] as ConcernPoint ?? elements[j] as ConcernPoint;

						//// TODO: Отношения между функциональностью и точкой, функциональностью и функциональностью 
					}
				}
		}

		private void BuildSetRelations(IEnumerable<MarkupElement> markup)
		{
			/// Получаем словарь функциональностей со списком включённых в них точек
			var concerns = GetLinearSequenceVisitor.GetConcerns(markup)
				.ToDictionary(e=>e, e=>
					{
						var points = GetLinearSequenceVisitor.GetPoints(new List<Concern> { e });
						return new Tuple<List<ConcernPoint>, HashSet<Node>>(points, new HashSet<Node>(points.Select(p => p.AstNode)));
					})
				.ToList();

			/// Для каждого из теоретико-множественных отношений готовим списки связанных элементов
			/// для каждой из функциональностей
			foreach (RelationType relType in
						((RelationType[])Enum.GetValues(typeof(RelationType))).Where(t => t.ToString().StartsWith(RelationGroup.Set.ToString())))
			{
				Cache[relType] = concerns.ToDictionary(c => (MarkupElement)c.Key, c => new HashSet<MarkupElement>());
			}

			for (var i=0; i< concerns.Count; ++i)
			{			
				for (var j = i + 1; i < concerns.Count; ++i)
				{
					var intersectionCount = concerns[i].Value.Item2.Intersect(concerns[j].Value.Item2).Count();

					/// Пересечение симметрично
					if(intersectionCount > 0)
					{
						Cache[RelationType.Set_Intersects][concerns[i].Key].Add(concerns[j].Key);
						Cache[RelationType.Set_Intersects][concerns[j].Key].Add(concerns[i].Key);
					}

					if (intersectionCount == concerns[i].Value.Item2.Count)
					{
						Cache[RelationType.Set_Includes][concerns[i].Key].UnionWith(concerns[j].Value.Item1);
						Cache[RelationType.Set_Includes][concerns[i].Key].Add(concerns[j].Key);
					}

					if (intersectionCount == concerns[j].Value.Item2.Count)
						Cache[RelationType.Set_Includes][concerns[j].Key].UnionWith(concerns[i].Value.Item1);
				}
			}
		}
	}
}
