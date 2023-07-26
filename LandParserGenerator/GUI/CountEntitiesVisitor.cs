using Land.Core.Parsing.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.GUI
{
	public class CountEntitiesVisitor : BaseTreeVisitor
	{
		public class TypeValuePair
		{
			public string Type { get; set; }
			public string Value { get; set; }
		}

		public Dictionary<string, int> Counts
		{
			get
			{
				return Land.Where(e => !WithValueOnly || !String.IsNullOrEmpty(e.Value))
					.GroupBy(e => e.Type).ToDictionary(e => e.Key, e => e.Count());
			}
		}

		public List<TypeValuePair> Land { get; set; } = new List<TypeValuePair>();

		public HashSet<string> TargetEntityTypes { get; set; }
		public HashSet<string> ChildrenWithValues { get; set; }
		public bool WithValueOnly { get; set; }

		public CountEntitiesVisitor(
			bool withValueOnly, 
			IEnumerable<string> targetEntityTypes,
			IEnumerable<string> childrenWithValues)
		{
			TargetEntityTypes = new HashSet<string>(targetEntityTypes);
			ChildrenWithValues = new HashSet<string>(childrenWithValues);
			WithValueOnly = withValueOnly;
		}

		public override void Visit(Node node)
		{
			if (TargetEntityTypes.Contains(node.Alias) || TargetEntityTypes.Contains(node.Symbol))
			{
				Land.Add(new TypeValuePair()
				{
					Type = node.Type,
					Value = String.Join(" ", node.Children.Where(c => ChildrenWithValues.Contains(c.Type))
						.Select(c => String.Join("", c.Value.Select(e => System.Text.RegularExpressions.Regex.Replace(e, "[\n\r\t\f]+", " ")))))
				});
			}

			base.Visit(node);
		}
	}
}
