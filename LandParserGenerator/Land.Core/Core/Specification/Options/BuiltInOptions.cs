using System;
using System.Collections.Generic;
using System.Linq;

namespace Land.Core.Specification
{
	/// <summary>
	/// Встроенные в язык группы опций
	/// </summary>
	public enum OptionGroups { PARSING, NODES, CUSTOMBLOCK }

	/// <summary>
	/// Опции, касающиеся процесса разбора
	/// </summary>
	public enum ParsingOption { START, SKIP, IGNORECASE, FRAGMENT, IGNOREUNDEFINED, RECOVERY }

	/// <summary>
	/// Опции, касающиеся построения дерева
	/// </summary>
	public enum NodeOption { GHOST, LIST, LEAF }

	/// <summary>
	/// Опции, касающиеся выделения псевдосущностей программы
	/// </summary>
	public enum CustomBlockOption { START, END, BASETOKEN }

	public static class OptionsManagerExtension
	{
		#region Методы для встроенных групп опций

		public static void Set(this OptionsManager manager, 
			ParsingOption option, List<string> symbols, List<dynamic> @params) =>
			manager.Set(OptionGroups.PARSING.ToString(), option.ToString(), symbols, @params);

		public static void Set(this OptionsManager manager,
			NodeOption option, List<string> symbols, List<dynamic> @params) =>
			manager.Set(OptionGroups.NODES.ToString(), option.ToString(), symbols, @params);

		public static void Set(this OptionsManager manager,
			CustomBlockOption option, List<string> symbols, List<dynamic> @params) =>
			manager.Set(OptionGroups.CUSTOMBLOCK.ToString(), option.ToString(), symbols, @params);

		public static bool IsSet(this OptionsManager manager, 
			ParsingOption option, string symbol = null) =>
			manager.IsSet(OptionGroups.PARSING.ToString(), option.ToString(), symbol);

		public static bool IsSet(this OptionsManager manager, 
			NodeOption option, string symbol = null) =>
			manager.IsSet(OptionGroups.NODES.ToString(), option.ToString(), symbol);

		public static bool IsSet(this OptionsManager manager, 
			CustomBlockOption option, string symbol = null) =>
			manager.IsSet(OptionGroups.CUSTOMBLOCK.ToString(), option.ToString(), symbol);

		public static HashSet<string> GetSymbols(this OptionsManager manager, 
			ParsingOption option) =>
			manager.GetSymbols(OptionGroups.PARSING.ToString(), option.ToString());

		public static HashSet<string> GetSymbols(this OptionsManager manager, 
			NodeOption option) =>
			manager.GetSymbols(OptionGroups.NODES.ToString(), option.ToString());

		public static HashSet<string> GetSymbols(this OptionsManager manager, 
			CustomBlockOption option) =>
			manager.GetSymbols(OptionGroups.CUSTOMBLOCK.ToString(), option.ToString());

		public static List<dynamic> GetParams(this OptionsManager manager,
			ParsingOption option, string symbol = null) =>
			manager.GetParams(OptionGroups.PARSING.ToString(), option.ToString(), symbol);

		public static List<dynamic> GetParams(this OptionsManager manager,
			NodeOption option, string symbol = null) =>
			manager.GetParams(OptionGroups.NODES.ToString(), option.ToString(), symbol);

		public static List<dynamic> GetParams(this OptionsManager manager,
			CustomBlockOption option, string symbol = null) =>
			manager.GetParams(OptionGroups.CUSTOMBLOCK.ToString(), option.ToString(), symbol);

		public static void Clear(this OptionsManager manager, 
			ParsingOption option) =>
			manager.Clear(OptionGroups.PARSING.ToString(), option.ToString());

		public static void Clear(this OptionsManager manager, 
			NodeOption option) =>
			manager.Clear(OptionGroups.NODES.ToString(), option.ToString());

		public static void Clear(this OptionsManager manager, 
			CustomBlockOption option) =>
			manager.Clear(OptionGroups.CUSTOMBLOCK.ToString(), option.ToString());

		#endregion

		#region Кастомные методы для конкретных опций

		public static bool IsRecoveryEnabled(this OptionsManager manager) =>
			manager.GetSymbols().Any(s => manager.IsSet(ParsingOption.RECOVERY, s));

		#endregion
	}

	public static class SymbolOptionsManagerExtension
	{
		public static void Set(this SymbolOptionsManager manager,
			ParsingOption option, List<dynamic> @params = null) =>
			manager.Set(OptionGroups.PARSING.ToString(), option.ToString(), @params);

		public static void Set(this SymbolOptionsManager manager,
			NodeOption option, List<dynamic> @params = null) =>
			manager.Set(OptionGroups.NODES.ToString(), option.ToString(), @params);

		public static void Set(this SymbolOptionsManager manager,
			CustomBlockOption option, List<dynamic> @params = null) =>
			manager.Set(OptionGroups.CUSTOMBLOCK.ToString(), option.ToString(), @params);


		public static bool IsSet(this SymbolOptionsManager manager,
			ParsingOption option) =>
			manager.IsSet(OptionGroups.PARSING.ToString(), option.ToString());

		public static bool IsSet(this SymbolOptionsManager manager,
			NodeOption option) =>
			manager.IsSet(OptionGroups.NODES.ToString(), option.ToString());

		public static bool IsSet(this SymbolOptionsManager manager,
			CustomBlockOption option) =>
			manager.IsSet(OptionGroups.CUSTOMBLOCK.ToString(), option.ToString());


		public static List<dynamic> GetParams(this SymbolOptionsManager manager,
			ParsingOption option) =>
			manager.GetParams(OptionGroups.PARSING.ToString(), option.ToString());

		public static List<dynamic> GetParams(this SymbolOptionsManager manager,
			NodeOption option) =>
			manager.GetParams(OptionGroups.NODES.ToString(), option.ToString());

		public static List<dynamic> GetParams(this SymbolOptionsManager manager,
			CustomBlockOption option) =>
			manager.GetParams(OptionGroups.CUSTOMBLOCK.ToString(), option.ToString());


		public static void Clear(this SymbolOptionsManager manager,
			ParsingOption option) =>
			manager.Clear(OptionGroups.PARSING.ToString(), option.ToString());

		public static void Clear(this SymbolOptionsManager manager,
			NodeOption option) =>
			manager.Clear(OptionGroups.NODES.ToString(), option.ToString());

		public static void Clear(this SymbolOptionsManager manager,
			CustomBlockOption option) =>
			manager.Clear(OptionGroups.CUSTOMBLOCK.ToString(), option.ToString());

		#region Кастомные методы для конкретных опций

		public static List<NodeOption> GetNodeOptions(this SymbolOptionsManager manager) =>
			manager.GetOptions(OptionGroups.NODES.ToString()).Select(o => (NodeOption)Enum.Parse(typeof(NodeOption), o)).ToList();

		public static List<ParsingOption> GetParsingOptions(this SymbolOptionsManager manager) =>
			manager.GetOptions(OptionGroups.PARSING.ToString()).Select(o => (ParsingOption)Enum.Parse(typeof(ParsingOption), o)).ToList();

		#endregion
	}
}
