using Land.Core.Parsing.Tree;
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
			List<MappingElement> allSourceElements,
			List<MappingElement> candidates,
			out bool hasDoubts);
	}

	public class DefaultRule: MappingRule
	{
		public override MappingElement GetSameElement(
			MappingElement sourceElement,
			List<MappingElement> allSourceElements,
			List<MappingElement> candidates,
			out bool hasDoubts)
		{
			hasDoubts = false;

			return candidates.FirstOrDefault(c => c.Header.Sequence.SequenceEqual(sourceElement.Header.Sequence)
				&& c.Ancestors.SequenceEqual(sourceElement.Ancestors));
		}
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

		public override MappingElement GetSameElement(
			MappingElement sourceElement, 
			List<MappingElement> allSourceElements,
			List<MappingElement> candidates,
			out bool hasDoubts)
		{
			/// Если анализируемый элемент не единственный с таким же именем, есть сомнения
			hasDoubts = allSourceElements.Where(c => sourceElement.Node.Type == c.Node.Type 
				&& c.Header.Core.SequenceEqual(sourceElement.Header.Core)
				&& c.Ancestors.SequenceEqual(sourceElement.Ancestors, new CSharpAncestorsEqualityComparer())).Count() > 1;

			/// Ищем кандидатов с таким же именем, как у сопоставляемого элемента
			var sameNameCandidates = candidates.Where(c => sourceElement.Node.Type == c.Node.Type 
				&& c.Header.Core.SequenceEqual(sourceElement.Header.Core)
				&& c.Ancestors.SequenceEqual(sourceElement.Ancestors, new CSharpAncestorsEqualityComparer())).ToList();

			if(sameNameCandidates.Count > 1)
			{
				var defaultMatch = (new DefaultRule())
					.GetSameElement(sourceElement, allSourceElements, sameNameCandidates, out bool defaultHasDoubts);

				hasDoubts &= defaultHasDoubts;
				return defaultMatch ?? sameNameCandidates[0];
			}
			else
			{
				return sameNameCandidates.FirstOrDefault();
			}
		}
	}
}

