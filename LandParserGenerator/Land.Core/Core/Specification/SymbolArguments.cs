using System;
using System.Collections.Generic;
using System.Linq;

namespace Land.Core.Specification
{
	public enum AnyArgument { Include, Except, Avoid, IgnorePairs, Error }

	[Serializable]
	public class SymbolArguments
	{
		#region Any

		public Dictionary<AnyArgument, HashSet<string>> AnyArguments { get; set; } = 
			new Dictionary<AnyArgument, HashSet<string>>();

		public void Set(AnyArgument anyArg, IEnumerable<string> symbols) =>
			AnyArguments[anyArg] = new HashSet<string>(symbols);

		public bool Contains(AnyArgument anyArg) =>
			AnyArguments.ContainsKey(anyArg);

		public bool Contains(AnyArgument anyArg, string token) =>
			AnyArguments.ContainsKey(anyArg) && AnyArguments[anyArg].Contains(token);

		#endregion

		public SymbolArguments Clone() =>
			new SymbolArguments()
			{
				AnyArguments = AnyArguments.ToDictionary(e => e.Key, e => new HashSet<string>(e.Value))
			};
	}
}
