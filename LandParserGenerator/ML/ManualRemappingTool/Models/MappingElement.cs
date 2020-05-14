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
	}
}
