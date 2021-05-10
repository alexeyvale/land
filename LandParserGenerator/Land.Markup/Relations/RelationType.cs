using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using Land.Core;
using Land.Core.Parsing.Tree;

namespace Land.Markup.Relations
{
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
		[RelationGroup(RelationGroup.Internal)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: true)]
		DirectlyPreceeds,

		[Description("Непосредственная часть функциональности")]
		[RelationGroup(RelationGroup.Internal)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: true)]
		IsLogicalChildOf,

		[Description("Непосредственно вложен в Land-сущность, соответствующую")]
		[RelationGroup(RelationGroup.Internal)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: true)]
		IsPhysicalChildOf,

		[Description("Соответствует той же Land-сущности, что и")]
		[RelationGroup(RelationGroup.Internal)]
		[RelationProperties(isReflexive: false, isSymmetric: true, isTransitive: true, isBasic: true)]
		MarksTheSameAs,

		#endregion

		#region Производные от Internal_DirectlyPreceeds

		[Description("Предшествует")]
		[RelationGroup(RelationGroup.Internal)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: false)]
		Preceeds,

		[Description("Непосредственно следует за")]
		[RelationGroup(RelationGroup.Internal)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: false)]
		DirectlyFollows,

		[Description("Следует за")]
		[RelationGroup(RelationGroup.Internal)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: false)]
		Follows,

		#endregion

		#region Производные от Internal_IsLogicalChildOf и Internal_IsPhysicalChildOf

		[Description("Часть функциональности или её подфункциональностей")]
		[RelationGroup(RelationGroup.Internal)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: false)]
		IsLogicalDescendantOf,

		[Description("Непосредственно объемлющая функциональность")]
		[RelationGroup(RelationGroup.Internal)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: false)]
		IsLogicalParentOf,

		[Description("Объемлющая функциональность")]
		[RelationGroup(RelationGroup.Internal)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: false)]
		IsLogicalAncestorOf,

		[Description("Вложен в Land-сущность, соответствующую")]
		[RelationGroup(RelationGroup.Internal)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: false)]
		IsPhysicalDescendantOf,

		[Description("Соответствует Land-сущности, непосредственно объемлющей")]
		[RelationGroup(RelationGroup.Internal)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: false)]
		IsPhysicalParentOf,

		[Description("Соответствует Land-сущности, объемлющей")]
		[RelationGroup(RelationGroup.Internal)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: false)]
		IsPhysicalAncestorOf,

		#endregion

		#region Отношения, определяемые внешним контекстом

		[Description("Должен предшествовать")]
		[RelationGroup(RelationGroup.External)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: true)]
		MustPreceed,

		[Description("Присутствует, только если есть")]
		[RelationGroup(RelationGroup.External)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: true)]
		ExistsIfAll,

		[Description("Присутствует, если есть хотя бы")]
		[RelationGroup(RelationGroup.External)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: true)]
		ExistsIfAny,

		[Description("Использует")]
		[RelationGroup(RelationGroup.External)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: true, isBasic: true)]
		Uses,

		[Description("Модифицирует")]
		[RelationGroup(RelationGroup.External)]
		[RelationProperties(isReflexive: false, isSymmetric: false, isTransitive: false, isBasic: true)]
		Modifies,

		#endregion
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

	public class RelationGroupAttribute: Attribute
	{
		public RelationGroup Group { get; set; }

		public RelationGroupAttribute(RelationGroup group)
		{
			Group = group;
		}
	}

	public static class EnumExtension
	{
		/// <summary>
		/// Получение атрибута заданного типа для значения перечислимого типа
		/// </summary>
		public static T GetAttribute<T>(this Enum enumVal) where T : System.Attribute
		{
			var type = enumVal.GetType();
			var memInfo = type.GetMember(enumVal.ToString());
			var attributes = memInfo[0].GetCustomAttributes(typeof(T), false);
			return (attributes.Length > 0) ? (T)attributes[0] : null;
		}

		/// <summary>
		/// Получение атрибута Description
		/// </summary>
		public static string GetDescription(this Enum enumVal)
		{
			return enumVal.GetAttribute<DescriptionAttribute>().Description;
		}

		public static bool IsReflexive(this RelationType enumVal)
		{
			return enumVal.GetAttribute<RelationPropertiesAttribute>().IsReflexive;
		}

		public static bool IsSymmetric(this RelationType enumVal)
		{
			return enumVal.GetAttribute<RelationPropertiesAttribute>().IsSymmetric;
		}

		public static bool IsTransitive(this RelationType enumVal)
		{
			return enumVal.GetAttribute<RelationPropertiesAttribute>().IsTransitive;
		}

		public static bool IsBasic(this RelationType enumVal)
		{
			return enumVal.GetAttribute<RelationPropertiesAttribute>().IsBasic;
		}

		public static RelationGroup GetGroup(this RelationType enumVal)
		{
			return enumVal.GetAttribute<RelationGroupAttribute>().Group;
		}
	}
}
