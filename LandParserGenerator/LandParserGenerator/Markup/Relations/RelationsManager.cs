using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
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
		Spatial_Preceeds,
		[Description("Следует за в пределах объемлющей сущности")]
		Spatial_Follows,
		[Description("Содержатся в пределах одной непосредственно объемлющей сущности")]
		Spatial_IsSiblingOf,
		[Description("Включает в себя")]
		Spatial_Includes,
		[Description("Код пересекается с кодом")]
		Spatial_Intersects,

		[Description("Включает в себя")]
		Set_Includes,
		[Description("Пересекается с")]
		Set_Intersects,

		[Description("Использует")]
		Semantic_Uses,
		[Description("Модифицирует")]
		Semantic_Modifies
	}

	public class RelationsManager
	{
		public Dictionary<RelationType, Dictionary<MarkupElement, List<MarkupElement>>> Cache { get; private set; } = 
			new Dictionary<RelationType, Dictionary<MarkupElement, List<MarkupElement>>>();

		public void ResetCache()
		{
			Cache = new Dictionary<RelationType, Dictionary<MarkupElement, List<MarkupElement>>>();
		}

		public Dictionary<MarkupElement, List<MarkupElement>> BuildRelation(RelationType type, IEnumerable<MarkupElement> markup)
		{
			var res = new Dictionary<MarkupElement, List<MarkupElement>>();

			switch(Enum.Parse(typeof(RelationGroup), type.ToString().Split('_')[0]))
			{
				case RelationGroup.Spatial:
					var elements = GetLinearSequenceVisitor.GetElements(markup);
					res = elements.ToDictionary(e => e, e => new List<MarkupElement>());

					switch (type)
					{
						case RelationType.Spatial_Follows:
							break;
						case RelationType.Spatial_Preceeds:
							break;
						case RelationType.Spatial_IsSiblingOf:
							break;
						case RelationType.Spatial_Includes:
							break;
						case RelationType.Spatial_Intersects:
							break;
					}

					break;
				case RelationGroup.Set:
					switch (type)
					{
						case RelationType.Set_Includes:
							break;
						case RelationType.Set_Intersects:
							break;
					}
					break;
				case RelationGroup.Semantic:
					switch (type)
					{
						case RelationType.Semantic_Uses:
							break;
					}
					break;
			}

			return res;
		}
	}
}
