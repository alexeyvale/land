using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public class Relation<T>
	{
		public RelationType Type { get; set; }
		public HashSet<Tuple<T, T>> Elements { get; set; }
	}

	public class RelatedPair<T>
	{
		public RelationType RelationType { get; set; }
		public T Item0 { get; set; }
		public T Item1 { get; set; }
	}

	public class RelationPropertiesAttribute : Attribute
	{
		public bool IsReflexive { get; private set; }
		public bool IsSymmetric { get; private set; }
		public bool IsTransitive { get; private set; }
		public bool IsBasic { get; private set; }

		public RelationPropertiesAttribute(bool isReflexive, bool isSymmetric, bool isTransitive, bool isBasic)
		{
			IsReflexive = isReflexive;
			IsSymmetric = isSymmetric;
			IsTransitive = isTransitive;
			IsBasic = isBasic;
		}
	}

	public enum RelationGroup
	{
		[Description("Выявляемые автоматически")]
		Internal,
		[Description("Задаваемые пользователем или внешним инструментом")]
		External
	}

	public enum RelationType
	{
		#region Базисные автоопределяемые отношения

		[Description("Непосредственно предшествует")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: true)]
		Internal_DirectlyPreceeds,

		[Description("Непосредственная часть функциональности")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: true)]
		Internal_IsLogicalChildOf,

		[Description("Вложена в область текста, соответствующую")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: true)]
		Internal_IsPhysicalChildOf,

		[Description("Помечает")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: true)]
		Internal_Marks,

		#endregion

		#region Производные от Internal_DirectlyPreceeds

		[Description("Предшествует")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: false)]
		Internal_Preceeds,

		[Description("Непосредственно следует за")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: false)]
		Internal_DirectlyFollows,

		[Description("Следует за")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: false)]
		Internal_Follows,

		#endregion

		#region Производные от Internal_IsLogicalChildOf и Internal_IsPhysicalChildOf

		[Description("Часть функциональности или её подфункциональностей")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: false)]
		Internal_IsLogicalDescendantOf,

		[Description("Непосредственно объемлющая функциональность")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: false)]
		Internal_IsLogicalParentOf,

		[Description("Объемлющая функциональность")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: false)]
		Internal_IsLogicalAncestorOf,

		[Description("Часть функциональности или её подфункциональностей")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: false)]
		Internal_IsPhysicalDescendantOf,

		[Description("Непосредственно объемлющая функциональность")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: false)]
		Internal_IsPhysicalParentOf,

		[Description("Объемлющая функциональность")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: false)]
		Internal_IsPhysicalAncestorOf,

		#endregion

		#region Отношения, определяемые внешним контекстом

		[Description("Должна предшествовать")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: true)]
		External_MustPreceed,

		[Description("Присутствует, только если есть")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: true)]
		External_ExistsIfAll,

		[Description("Присутствует, если есть хотя бы")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: true)]
		External_ExistsIfAny,

		[Description("Использует")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: true)]
		External_Uses,

		[Description("Модифицирует")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: true)]
		External_Modifies,

		#endregion
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
		public void Clear(RelationGroup group)
		{
			foreach (RelationType relType in ((RelationType[])Enum.GetValues(typeof(RelationType)))
				.Where(e=>group.ToString() == e.ToString().Split('_')[0]))
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
			Clear(RelationGroup.Internal);

			var elements = GetLinearSequenceVisitor.GetElements(markup);
			var concerns = elements.OfType<Concern>().ToList();
			var points = elements.OfType<ConcernPoint>().ToList();

			/// Создаём заготовки для каждого отношения и элемента
			foreach (RelationType relType in ((RelationType[])Enum.GetValues(typeof(RelationType)))
						.Where(t => t.ToString().StartsWith(RelationGroup.Internal.ToString())))
			{
				Cache[relType] = elements.ToDictionary(c => c, c => new HashSet<MarkupElement>());
			}

			/// Строим отношения иерархической принадлежности точек и функциональностей объемлющим функциональностям
			foreach (var concern in concerns.Where(e => e is Concern))
				Cache[RelationType.Internal_IsLogicalParentOf][concern] = new HashSet<MarkupElement>(concern.Elements);

			FillAsClosure(RelationType.Internal_IsLogicalAncestorOf, RelationType.Internal_IsLogicalParentOf);
			FillAsTransposition(RelationType.Internal_IsLogicalChildOf, RelationType.Internal_IsLogicalParentOf);
			FillAsTransposition(RelationType.Internal_IsLogicalDescendantOf, RelationType.Internal_IsLogicalAncestorOf);

			/// Строим отношения, связанные с пространственным взаимным расположением точек
			for (var i = 0; i < points.Count; ++i)
				for (var j = i + 1; j < points.Count; ++j)
				{
					var point1 = points[i];
					var point2 = points[j];

					if (GetAstLandParent(point1) == GetAstLandParent(point2)
						&& point1.Location.End.Offset <= point2.Location.Start.Offset)
					{
						Cache[RelationType.Internal_Preceeds][point1].Add(point2);
					}

					if (point1.Location.Includes(point2.Location))
					{
						Cache[RelationType.Internal_IsPhysicalDescendantOf][point2].Add(point1);

						foreach (Concern concern in Cache[RelationType.Internal_IsLogicalDescendantOf][point1])
							Cache[RelationType.Internal_IsPhysicalDescendantOf][point2].Add(concern);
					}

					if (point2.Location.Includes(point1.Location))
					{
						Cache[RelationType.Internal_IsPhysicalDescendantOf][point1].Add(point2);

						foreach (Concern concern in Cache[RelationType.Internal_IsLogicalDescendantOf][point2])
							Cache[RelationType.Internal_IsPhysicalDescendantOf][point1].Add(concern);
					}
				}

			FillAsTransposition(RelationType.Internal_Follows, RelationType.Internal_Preceeds);

			/// Строим отношения, связанные с пространственным взаимным расположением функциональностей
			for (var i = 0; i < concerns.Count; ++i)
				for (var j = i + 1; j < concerns.Count; ++j)
				{
					if(Cache[RelationType.Internal_IsLogicalAncestorOf][concerns[i]].OfType<ConcernPoint>()
						.All(p => Cache[RelationType.Internal_IsPhysicalDescendantOf][p].Contains(concerns[j])))
					{
						Cache[RelationType.Internal_IsPhysicalDescendantOf][concerns[i]].Add(concerns[j]);
					}

					if (Cache[RelationType.Internal_IsLogicalAncestorOf][concerns[j]].OfType<ConcernPoint>()
						.All(p => Cache[RelationType.Internal_IsPhysicalDescendantOf][p].Contains(concerns[i])))
					{
						Cache[RelationType.Internal_IsPhysicalDescendantOf][concerns[j]].Add(concerns[i]);
					}
				}
		}

		private void FillAsTransposition(RelationType target, RelationType source)
		{
			if (Cache.ContainsKey(source) && Cache.ContainsKey(target))
			{
				foreach (var leftPart in Cache[source].Keys)
					foreach (var rightPart in Cache[source][leftPart])
						Cache[target][rightPart].Add(leftPart);
			}
		}

		private void FillAsClosure(RelationType target, RelationType source)
		{
			if (Cache.ContainsKey(source) && Cache.ContainsKey(target))
			{
				foreach (var leftPart in Cache[source].Keys)
					Cache[target][leftPart] = new HashSet<MarkupElement>(Cache[source][leftPart]);

				var changed = true;

				while (changed)
				{
					changed = false;

					foreach (var leftPart in Cache[target].Keys)
					{
						var toAdd = new HashSet<MarkupElement>();
						var oldCount = Cache[target][leftPart].Count;

						foreach (var rightPart in Cache[target][leftPart])
							toAdd.UnionWith(Cache[target][rightPart]);

						Cache[target][leftPart].UnionWith(toAdd);

						if (Cache[target][leftPart].Count > oldCount)
							changed = true;
					}
				}
			}
		}

		private Node GetAstLandParent(ConcernPoint p)
		{
			var currentNode = p.AstNode;

			while (!(currentNode.Parent?.Options.IsLand ?? true))
				currentNode = currentNode.Parent;

			return currentNode.Parent;
		}
	}
}
