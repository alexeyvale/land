using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.Core
{
	/// <summary>
	/// Возможные категории опций
	/// </summary>
	public enum OptionCategory { PARSING, NODES, MAPPING, RECOVERY }

	/// <summary>
	/// Опции, касающиеся построения дерева
	/// </summary>
	public enum NodeOption { GHOST, LIST, LEAF }

	/// <summary>
	/// Опции, касающиеся процесса разбора
	/// </summary>
	public enum ParsingOption { START, SKIP, IGNORECASE, FRAGMENT, IGNOREUNDEFINED }

	/// <summary>
	/// Опции, касающиеся восстановления от ошибок
	/// </summary>
	public enum RecoveryOption { ANYBASED, TRIGGER, INSERT }

	/// <summary>
	/// Опции, касающиеся отображения старого дерева в новое (изменённое) дерево
	/// </summary>
	public enum MappingOption { PRIORITY, LAND, EXACTMATCH }


	public class OptionsManager
	{
		public const string GLOBAL_PARAMETERS_SYMBOL = "";

		private Dictionary<NodeOption, Dictionary<string, List<dynamic>>> NodeOptions { get; set; } = 
			new Dictionary<NodeOption, Dictionary<string, List<dynamic>>>();
		private Dictionary<MappingOption, Dictionary<string, List<dynamic>>> MappingOptions { get; set; } =
			new Dictionary<MappingOption, Dictionary<string, List<dynamic>>>();
		private Dictionary<ParsingOption, Dictionary<string, List<dynamic>>> ParsingOptions { get; set; } = 
			new Dictionary<ParsingOption, Dictionary<string, List<dynamic>>>();
		private Dictionary<RecoveryOption, Dictionary<string, List<dynamic>>> RecoveryOptions { get; set; } =
			new Dictionary<RecoveryOption, Dictionary<string, List<dynamic>>>();

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

		public void Set(RecoveryOption opt, params string[] symbols)
		{
			if (!RecoveryOptions.ContainsKey(opt))
				RecoveryOptions[opt] = new Dictionary<string, List<dynamic>>();
			foreach (var smb in symbols)
			{
				if (!RecoveryOptions[opt].ContainsKey(smb))
					RecoveryOptions[opt].Add(smb, null);
			}
		}

		public void Set(RecoveryOption opt, string[] symbols, params dynamic[] @params)
		{
			if (!RecoveryOptions.ContainsKey(opt))
				RecoveryOptions[opt] = new Dictionary<string, List<dynamic>>();
			foreach (var smb in symbols)
			{
				if (!RecoveryOptions[opt].ContainsKey(smb))
					RecoveryOptions[opt].Add(smb, @params.ToList());
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

		public bool IsSet(RecoveryOption opt, string symbol = null)
		{
			return RecoveryOptions.ContainsKey(opt) && (symbol == null || RecoveryOptions[opt].ContainsKey(symbol));
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

		public HashSet<string> GetSymbols(RecoveryOption opt)
		{
			return IsSet(opt) ? new HashSet<string>(RecoveryOptions[opt].Keys) : new HashSet<string>();
		}

		public List<dynamic> GetParams(MappingOption opt, string symbol)
		{
			return IsSet(opt, symbol) ? MappingOptions[opt][symbol] : new List<dynamic>();
		}

		public List<dynamic> GetParams(RecoveryOption opt, string symbol)
		{
			return IsSet(opt, symbol) ? RecoveryOptions[opt][symbol] : new List<dynamic>();
		}

		public void Clear(NodeOption opt)
		{
			NodeOptions.Remove(opt);
		}

		public void Clear(ParsingOption opt)
		{
			ParsingOptions.Remove(opt);
		}

		public void Clear(MappingOption opt)
		{
			MappingOptions.Remove(opt);
		}

		public void Clear(RecoveryOption opt)
		{
			RecoveryOptions.Remove(opt);
		}
	}

	public class LocalOptions
	{
		public const double BASE_PRIORITY = 1;

		public NodeOption? NodeOption { get; set; } = null;

		public double? Priority { get; set; } = null;
		public bool IsLand { get; set; } = false;
		public bool ExactMatch { get; set; } = false;

		public Dictionary<AnyOption, HashSet<string>> AnyOptions { get; set; } 
			= new Dictionary<AnyOption, HashSet<string>>();

		public void Set(NodeOption opt)
		{
			NodeOption = opt;
		}

		public void Set(MappingOption opt, params dynamic[] @params)
		{
			switch(opt)
			{
				case MappingOption.LAND:
					IsLand = true;
					break;
				case MappingOption.PRIORITY:
					Priority = (double)@params[0];
					break;
				case MappingOption.EXACTMATCH:
					ExactMatch = true;
					break;
				default:
					break;
			}
		}

		public bool Contains(AnyOption anyOption, string smb)
		{
			return AnyOptions.ContainsKey(anyOption) && AnyOptions[anyOption].Contains(smb);
		}

		public LocalOptions Clone()
		{
			return new LocalOptions()
			{
				IsLand = IsLand,
				NodeOption = NodeOption,
				Priority = Priority,
				AnyOptions = new Dictionary<AnyOption, HashSet<string>>(AnyOptions)
			};
		}
	}

	public class ArgumentGroup
	{
		public string Name { get; set; }
		public List<dynamic> Arguments { get; set; }
	}

	public class OptionDeclaration
	{
		public string Name { get; set; }
		public List<dynamic> Arguments { get; set; }
		public List<string> Symbols { get; set; }
	}
}
