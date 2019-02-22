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

		[Description("Непосредственно вложен в Land-сущность, соответствующую")]
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

		[Description("Вложен в Land-сущность, соответствующую")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: false)]
		Internal_IsPhysicalDescendantOf,

		[Description("Соответствует Land-сущности, непосредственно объемлющей")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: false)]
		Internal_IsPhysicalParentOf,

		[Description("Соответствует Land-сущности, объемлющей")]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: false)]
		Internal_IsPhysicalAncestorOf,

		#endregion

		#region Отношения, определяемые внешним контекстом

		[Description("Должен предшествовать")]
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
				return !Cache.ContainsKey(relType)
					? null
					: !Cache[relType].ContainsKey(elem)
						? new HashSet<MarkupElement>()
						: Cache[relType][elem];
			}
		}

		public Dictionary<MarkupElement, HashSet<MarkupElement>> this[RelationType relType] 
			=> !Cache.ContainsKey(relType)? null : Cache[relType];

		public bool HasCache => Cache != null && Cache.Count != 0;

		/// Убираем из кеша информацию об определённых группах отношений
		public void Clear(RelationGroup group)
		{
			foreach (RelationType relType in ((RelationType[])Enum.GetValues(typeof(RelationType)))
				.Where(e=>group.ToString() == e.ToString().Split('_')[0]))
			{
				Cache[relType] = new Dictionary<MarkupElement, HashSet<MarkupElement>>();
			}
		}

		public void AddRelation(RelationType type, MarkupElement from, MarkupElement to)
		{
			if (!Cache.ContainsKey(type))
				Cache[type] = new Dictionary<MarkupElement, HashSet<MarkupElement>>();

			if (!Cache[type].ContainsKey(from))
				Cache[type][from] = new HashSet<MarkupElement>();

			Cache[type][from].Add(to);

			if (type.GetAttribute<RelationPropertiesAttribute>().IsSymmetric)
			{
				if (!Cache[type].ContainsKey(to))
					Cache[type][to] = new HashSet<MarkupElement>();

				Cache[type][to].Add(from);
			}

			if (type.GetAttribute<RelationPropertiesAttribute>().IsTransitive)
			{
				FillAsClosure(type, type);
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
			foreach (var concern in concerns)
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
					}

					if (point2.Location.Includes(point1.Location))
					{
						Cache[RelationType.Internal_IsPhysicalDescendantOf][point1].Add(point2);
					}
				}

			FillAsTransposition(RelationType.Internal_Follows, RelationType.Internal_Preceeds);
			FillAsTransposition(RelationType.Internal_IsPhysicalAncestorOf, RelationType.Internal_IsPhysicalDescendantOf);
		}

		public HashSet<RelationType> GetPossibleExternalRelations(MarkupElement from, MarkupElement to)
		{
			var result = new HashSet<RelationType>();

			if(from is ConcernPoint)
			{
				/// CP CP
				if(to is ConcernPoint)
				{
					result.Add(RelationType.External_Uses);

					if (Cache[RelationType.Internal_Preceeds][from].Contains(to))
					{
						result.Add(RelationType.External_MustPreceed);
					}
				}
				/// CP C
				else
				{
					if (Cache[RelationType.Internal_IsLogicalDescendantOf][from].Contains(to) ||
						Cache[RelationType.Internal_IsLogicalAncestorOf][to].Any(desc => Cache[RelationType.Internal_IsPhysicalAncestorOf][desc].Contains(from)))
					{
						result.Add(RelationType.External_ExistsIfAll);
						result.Add(RelationType.External_ExistsIfAny);
					}
				}
			}
			else
			{
				/// C CP
				if (to is ConcernPoint)
				{
					result.Add(RelationType.External_Uses);
				}
				/// C C
				else
				{
					result.Add(RelationType.External_Modifies);
				}
			}

			return result;
		}

		private void FillAsTransposition(RelationType target, RelationType source)
		{
			if (Cache.ContainsKey(source))
			{
				var result = Cache[source].SelectMany(kvp=>kvp.Value).Distinct()
					.ToDictionary(v=>v, v => new HashSet<MarkupElement>());

				foreach (var leftPart in Cache[source].Keys)
					foreach (var rightPart in Cache[source][leftPart])
						result[rightPart].Add(leftPart);

				Cache[target] = result;
			}
		}

		private void FillAsClosure(RelationType target, RelationType source)
		{
			if (Cache.ContainsKey(source))
			{
				/// Копируем исходное отношение в целевое
				var sourceCopy = new Dictionary<MarkupElement, HashSet<MarkupElement>>();

				foreach (var leftPart in Cache[source].Keys)
					sourceCopy[leftPart] = new HashSet<MarkupElement>(Cache[source][leftPart]);

				Cache[target] = sourceCopy;

				/// Итеративно строим замыкание
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
