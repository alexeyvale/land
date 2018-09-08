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
		public Dictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
		public Dictionary<string, List<string>> Values { get; set; } = new Dictionary<string, List<string>>();

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
					Values[node.Type] = new List<string>();
				}

				Counts[node.Type] += 1;
				Values[node.Type].Add(String.Join(" ", node.Children.Where(c=> ChildrenWithValues.Contains(c.Type)).Select(c=>String.Join("", c.Value))));
			}

			base.Visit(node);
		}

		public void MergeIn(Dictionary<string, int> targetCounts, Dictionary<string, List<string>> targetValues)
		{
			foreach(var pair in Counts)
			{
				if (!targetCounts.ContainsKey(pair.Key))
				{
					targetCounts[pair.Key] = 0;
					targetValues[pair.Key] = new List<string>();
				}

				targetCounts[pair.Key] += pair.Value;
				targetValues[pair.Key].AddRange(Values[pair.Key]);
			}
		}
	}
}
