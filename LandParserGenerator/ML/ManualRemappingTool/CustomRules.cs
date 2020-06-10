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
					new ProgrammingLanguageMappingRule()
				},
				{
					"java",
					new ProgrammingLanguageMappingRule()
				},
				{
					"pas",
					new ProgrammingLanguageMappingRule()
				},
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
			List<MappingElement> candidates);
	}

	public class DefaultRule: MappingRule
	{
		public override MappingElement GetSameElement(
			MappingElement sourceElement,
			List<MappingElement> allSourceElements,
			List<MappingElement> candidates)
		{
			var correctCandidates = candidates.Where(c => c.Header.Sequence.SequenceEqual(sourceElement.Header.Sequence)
				&& c.Ancestors.SequenceEqual(sourceElement.Ancestors)).ToList();

			return correctCandidates.Count == 1 ? correctCandidates[0] : null;
		}
	}

	public class ProgrammingLanguageMappingRule : MappingRule
	{
		public override MappingElement GetSameElement(
			MappingElement sourceElement,
			List<MappingElement> allSourceElements,
			List<MappingElement> candidates)
		{
			var heuristic = new ProgrammingLanguageHeuristic();
			var element = heuristic.GetSameElement(
				new PointContext
				{
					HeaderContext = sourceElement.Header,
					AncestorsContext = sourceElement.Ancestors,
					InnerContext = new InnerContext(),
					ClosestContext = allSourceElements
						.Where(e => e != sourceElement).Select(e => (PointContext)e).ToList()
				},
				candidates.Select(e => (RemapCandidateInfo)e).ToList()
			) ;

			return element != null 
				? candidates.FirstOrDefault(C=>C.Node == element.Node) : null;
		}
	}
}

