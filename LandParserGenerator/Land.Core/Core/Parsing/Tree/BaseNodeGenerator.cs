using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Land.Core.Specification;

namespace Land.Core.Parsing.Tree
{
	public class BaseNodeGenerator
	{
		public const string BASE_NODE_TYPE = "Node";

		protected Dictionary<string, ConstructorInfo> Cache { get; set; } = new Dictionary<string, ConstructorInfo>()
		{
			[BASE_NODE_TYPE] = Assembly.GetExecutingAssembly().GetType($"Land.Core.Parsing.Tree.{BASE_NODE_TYPE}")
				.GetConstructor(new Type[] { typeof(string), typeof(SymbolOptionsManager), typeof(SymbolArguments) })
		};

		public BaseNodeGenerator(Grammar g) { }

		public virtual Node Generate(string symbol, 
			SymbolOptionsManager opts = null, 
			SymbolArguments args = null)
		{
			return (Node)Cache[Cache.ContainsKey(symbol) ? symbol : BASE_NODE_TYPE]
				.Invoke(new object[] { symbol, opts, args });
		}

		public virtual Node Generate(Node node)
		{
			return (Node)Cache[Cache.ContainsKey(node.Symbol) ? node.Symbol : BASE_NODE_TYPE]
				.Invoke(new object[] { node.Symbol, node.Options, node.Arguments });
		}
	}
}
