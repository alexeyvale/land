using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LandParserGenerator
{
	/// <summary>
	/// Возможные категории опций
	/// </summary>
	public enum OptionCategory { PARSING, NODES, MAPPING }

	/// <summary>
	/// Опции, касающиеся построения дерева
	/// </summary>
	public enum NodeOption { GHOST, LIST, LEAF }

	/// <summary>
	/// Опции, касающиеся процесса разбора
	/// </summary>
	public enum ParsingOption { START, SKIP, IGNORECASE }

	/// <summary>
	/// Опции, касающиеся отображения старого дерева в новое (изменённое) дерево
	/// </summary>
	public enum MappingOption { PRIORITY, LAND, BASEPRIORITY }

	public class OptionsManager
	{
		private Dictionary<NodeOption, HashSet<string>> NodeOptions { get; set; } = new Dictionary<NodeOption, HashSet<string>>();
		private Dictionary<MappingOption, HashSet<string>> MappingOptions { get; set; } = new Dictionary<MappingOption, HashSet<string>>();
		private Dictionary<ParsingOption, HashSet<string>> ParsingOptions { get; set; } = new Dictionary<ParsingOption, HashSet<string>>();

		public void Set(NodeOption opt, params string[] symbols)
		{
			if (!NodeOptions.ContainsKey(opt))
				NodeOptions[opt] = new HashSet<string>();
			NodeOptions[opt].UnionWith(symbols);
		}

		public void Set(ParsingOption opt, params string[] symbols)
		{
			if (!ParsingOptions.ContainsKey(opt))
				ParsingOptions[opt] = new HashSet<string>();
			ParsingOptions[opt].UnionWith(symbols);
		}

		public void Set(MappingOption opt, params string[] symbols)
		{
			if (!MappingOptions.ContainsKey(opt))
				MappingOptions[opt] = new HashSet<string>();
			MappingOptions[opt].UnionWith(symbols);
		}

		public bool IsSet(NodeOption opt, string symbol = null)
		{
			return NodeOptions.ContainsKey(opt) && (symbol == null || NodeOptions[opt].Contains(symbol));
		}

		public bool IsSet(ParsingOption opt, string symbol = null)
		{
			return ParsingOptions.ContainsKey(opt) && (symbol == null || ParsingOptions[opt].Contains(symbol));
		}

		public bool IsSet(MappingOption opt, string symbol = null)
		{
			return MappingOptions.ContainsKey(opt) && (symbol == null || MappingOptions[opt].Contains(symbol));
		}

		public HashSet<string> GetSymbols(NodeOption opt)
		{
			return IsSet(opt) ? NodeOptions[opt] : new HashSet<string>();
		}

		public HashSet<string> GetSymbols(ParsingOption opt)
		{
			return IsSet(opt) ? ParsingOptions[opt] : new HashSet<string>();
		}

		public HashSet<string> GetSymbols(MappingOption opt)
		{
			return IsSet(opt) ? MappingOptions[opt] : new HashSet<string>();
		}
	}

	public class LocalOptions
	{
		public NodeOption? NodeOption;
		public double? Priority;

		public void Set(NodeOption opt)
		{
			NodeOption = opt;
		}

		public void Set(MappingOption opt)
		{ }
	}
}
