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
		private Dictionary<NodeOption, Dictionary<string, List<dynamic>>> NodeOptions { get; set; } = 
			new Dictionary<NodeOption, Dictionary<string, List<dynamic>>>();
		private Dictionary<MappingOption, Dictionary<string, List<dynamic>>> MappingOptions { get; set; } =
			new Dictionary<MappingOption, Dictionary<string, List<dynamic>>>();
		private Dictionary<ParsingOption, Dictionary<string, List<dynamic>>> ParsingOptions { get; set; } = 
			new Dictionary<ParsingOption, Dictionary<string, List<dynamic>>>();

		public void Set(NodeOption opt, params string[] symbols)
		{
			if (!NodeOptions.ContainsKey(opt))
				NodeOptions[opt] = new Dictionary<string, List<dynamic>>();
			foreach (var smb in symbols)
			{
				if (!NodeOptions[opt].ContainsKey(smb))
					NodeOptions[opt].Add(smb, null);
			}
		}

		public void Set(ParsingOption opt, params string[] symbols)
		{
			if (!ParsingOptions.ContainsKey(opt))
				ParsingOptions[opt] = new Dictionary<string, List<dynamic>>();
			foreach (var smb in symbols)
			{
				if (!ParsingOptions[opt].ContainsKey(smb))
					ParsingOptions[opt].Add(smb, null);
			}
		}

		public void Set(MappingOption opt, params string[] symbols)
		{
			if (!MappingOptions.ContainsKey(opt))
				MappingOptions[opt] = new Dictionary<string, List<dynamic>>();
			foreach(var smb in symbols)
			{
				if (!MappingOptions[opt].ContainsKey(smb))
					MappingOptions[opt].Add(smb, null);
			}
		}

		public void Set(MappingOption opt, string[] symbols, params dynamic[] @params)
		{
			if (!MappingOptions.ContainsKey(opt))
				MappingOptions[opt] = new Dictionary<string, List<dynamic>>();
			foreach (var smb in symbols)
			{
				if (!MappingOptions[opt].ContainsKey(smb))
					MappingOptions[opt].Add(smb, @params.ToList());
			}
		}

		public bool IsSet(NodeOption opt, string symbol = null)
		{
			return NodeOptions.ContainsKey(opt) && (symbol == null || NodeOptions[opt].ContainsKey(symbol));
		}

		public bool IsSet(ParsingOption opt, string symbol = null)
		{
			return ParsingOptions.ContainsKey(opt) && (symbol == null || ParsingOptions[opt].ContainsKey(symbol));
		}

		public bool IsSet(MappingOption opt, string symbol = null)
		{
			return MappingOptions.ContainsKey(opt) && (symbol == null || MappingOptions[opt].ContainsKey(symbol));
		}

		public HashSet<string> GetSymbols(NodeOption opt)
		{
			return IsSet(opt) ? new HashSet<string>(NodeOptions[opt].Keys) : new HashSet<string>();
		}

		public HashSet<string> GetSymbols(ParsingOption opt)
		{
			return IsSet(opt) ? new HashSet<string>(ParsingOptions[opt].Keys) : new HashSet<string>();
		}

		public HashSet<string> GetSymbols(MappingOption opt)
		{
			return IsSet(opt) ? new HashSet<string>(MappingOptions[opt].Keys) : new HashSet<string>();
		}

		public List<dynamic> GetParams(MappingOption opt, string symbol)
		{
			return IsSet(opt, symbol) ? MappingOptions[opt][symbol] : new List<dynamic>();
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
