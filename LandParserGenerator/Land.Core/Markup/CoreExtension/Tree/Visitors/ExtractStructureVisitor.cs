using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Parsing.Tree;
using Land.Core.Specification;

namespace Land.Markup.CoreExtension
{
	public class ExtractStructureVisitor : BaseTreeVisitor
	{
		public List<ushort> Sequence { get; set; } = new List<ushort>();
		public Dictionary<string, List<ushort>> Codes { get; set; }

		public ExtractStructureVisitor(Dictionary<string, List<ushort>> codes)
		{
			Codes = codes;
		}

		public override void Visit(Node node)
		{
			if (Codes.ContainsKey(node.Type))
			{
				Sequence.Add(Codes[node.Type][0]);

				if (Codes[node.Type].Count > 1)
				{
					base.Visit(node);

					Sequence.Add(Codes[node.Type][1]);
				}
			}
		}
	}
}
