using Land.Core.Parsing.Tree;
using Land.Markup.Binding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManualRemappingTool
{
	public class MappingElement
	{
		public Node Node { get; set; }
		public HeaderContext Header { get; set; }
		public List<AncestorsContextElement> Ancestors { get; set; }

		public static explicit operator RemapCandidateInfo(MappingElement element) =>
			new RemapCandidateInfo
			{
				Node = element.Node,
				Context = new PointContext
				{
					Type = element.Node.Alias ?? element.Node.Symbol,
					AncestorsContext = element.Ancestors,
					HeaderContext = element.Header,
					InnerContext = new InnerContext()
				}
			};

		public static explicit operator PointContext(MappingElement element) =>
			element != null ? new PointContext
			{
				Type = element.Node.Alias ?? element.Node.Symbol,
				AncestorsContext = element.Ancestors,
				HeaderContext = element.Header,
				InnerContext = new InnerContext()
			} : null;

		public static explicit operator MappingElement(RemapCandidateInfo element) =>
			element != null ? new MappingElement
			{
				Node = element.Node,
				Header = element.Context.HeaderContext,
				Ancestors = element.Context.AncestorsContext
			} : null;
	}
}
