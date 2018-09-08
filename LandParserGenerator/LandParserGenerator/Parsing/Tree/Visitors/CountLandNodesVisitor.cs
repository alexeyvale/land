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
		public Dictionary<string, int> Counted { get; set; } = new Dictionary<string, int>(); 

		public override void Visit(Node node)
		{
			if(node.Options.IsLand)
			{
				if (!Counted.ContainsKey(node.Type))
					Counted[node.Type] = 0;

				Counted[node.Type] += 1;
			}

			base.Visit(node);
		}

		public static Dictionary<string, int> Merge(Dictionary<string, int> a, Dictionary<string, int> b)
		{
			var result = new Dictionary<string, int>(a);

			foreach(var pair in b)
			{
				if (!result.ContainsKey(pair.Key))
					result[pair.Key] = 0;

				result[pair.Key] += pair.Value;
			}

			return result;
		}
	}
}
