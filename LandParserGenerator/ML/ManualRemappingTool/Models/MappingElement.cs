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
					Type = element.Node.Type,
					AncestorsContext = element.Ancestors,
					HeaderContext = element.Header
				}
			};

		public static explicit operator PointContext(MappingElement element) =>
			new PointContext
			{
				Type = element.Node.Type,
				AncestorsContext = element.Ancestors,
				HeaderContext = element.Header
			};
	}
}
