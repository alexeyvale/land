using Land.Markup.Binding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManualRemappingTool
{
	public class MappingHelper
	{
		private Dictionary<string, MappingRule> Rules { get; set; } =
			new Dictionary<string, MappingRule>
			{
				{
					"cs",
					new CSharpRule()
				}
			};

		private MappingRule DefaultRule { get; set; } = new DefaultRule();

		public MappingRule this[string ext] 
		{ 
			get
			{
				ext = ext.Trim('.');
				return Rules.ContainsKey(ext) ? Rules[ext] : DefaultRule;
			} 
		}
	}

	public abstract  class MappingRule
	{
		public abstract MappingElement GetSameElement(
			MappingElement sourceElement, 
			List<MappingElement> candidates);
	}

	public class DefaultRule: MappingRule
	{
		public override MappingElement GetSameElement(
			MappingElement sourceElement, 
			List<MappingElement> candidates) =>
			candidates.FirstOrDefault(c => c.Header.Sequence.SequenceEqual(sourceElement.Header.Sequence)
				&& c.Ancestors.SequenceEqual(sourceElement.Ancestors));
	}

	public class CSharpRule : MappingRule
	{
		private class CSharpAncestorsEqualityComparer : IEqualityComparer<AncestorsContextElement>
		{
			public static string GetName(List<HeaderContextElement> e) =>
				String.Join("", e.Where(he => he.Type == "name").SelectMany(he=>he.Value));

			public bool Equals(AncestorsContextElement x, AncestorsContextElement y)
			{
				return x.Type == y.Type 
					&& GetName(x.HeaderContext) == GetName(y.HeaderContext);
			}

			public int GetHashCode(AncestorsContextElement obj)
			{
				throw new NotImplementedException();
			}
		}

		public override MappingElement GetSameElement(MappingElement sourceElement, List<MappingElement> candidates)
		{
			var sourceElementName = sourceElement.Header.Core;
			var sameName = candidates.Where(c => c.Header.Core == sourceElementName
				&& c.Ancestors.SequenceEqual(sourceElement.Ancestors, new CSharpAncestorsEqualityComparer())).ToList();

			return sameName.Count == 1 
				? sameName[0] : (new DefaultRule()).GetSameElement(sourceElement, sameName);
		}
	}
}

