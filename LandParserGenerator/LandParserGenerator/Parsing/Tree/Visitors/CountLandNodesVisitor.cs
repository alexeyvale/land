using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;

namespace Land.Core.Parsing.Tree
{
	public class CountLandNodesVisitor: BaseTreeVisitor
	{
		public class TypeValuePair
		{
			public string Type { get; set; }
			public string Value { get; set; }
		}

		public Dictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
		public List<TypeValuePair> Land { get; set; } = new List<TypeValuePair>();

		public HashSet<string> ChildrenWithValues { get; set; }

		public CountLandNodesVisitor(params string[] childrenWithValues)
		{
			ChildrenWithValues = new HashSet<string>(childrenWithValues);
		}

		public override void Visit(Node node)
		{
			if(node.Options.IsLand)
			{
				if (!Counts.ContainsKey(node.Type))
				{
					Counts[node.Type] = 0;
				}

				Counts[node.Type] += 1;
				Land.Add(new TypeValuePair()
				{
					Type = node.Type,
					Value = String.Join(" ", node.Children.Where(c => ChildrenWithValues.Contains(c.Type))
						.Select(c => String.Join("", c.Value.Select(e=>System.Text.RegularExpressions.Regex.Replace(e, "[\n\r\t\f]+", " ")))))
				});
			}

			base.Visit(node);
		}
	}
}
