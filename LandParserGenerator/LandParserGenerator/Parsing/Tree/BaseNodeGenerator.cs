using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Land.Core.Parsing.Tree
{
	public class BaseNodeGenerator
	{
		public const string BASE_NODE_TYPE = "Node";

		protected Dictionary<string, ConstructorInfo> Cache { get; set; } = new Dictionary<string, ConstructorInfo>()
		{
			[BASE_NODE_TYPE] = Assembly.GetExecutingAssembly().GetType($"Land.Core.Parsing.Tree.{BASE_NODE_TYPE}")
				.GetConstructor(new Type[] { typeof(string), typeof(LocalOptions) })
		};

		public BaseNodeGenerator(Grammar g) { }

		public virtual Node Generate(string symbol, LocalOptions opts = null)
		{
			return (Node)Cache[Cache.ContainsKey(symbol) ? symbol : BASE_NODE_TYPE]
				.Invoke(new object[] { symbol, opts });
		}
	}
}
