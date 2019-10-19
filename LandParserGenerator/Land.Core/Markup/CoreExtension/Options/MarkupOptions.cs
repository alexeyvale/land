using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Specification;

namespace Land.Markup.CoreExtension
{
	/// <summary>
	/// Опции, касающиеся отображения старого дерева в новое (изменённое) дерево
	/// </summary>
	public enum MarkupOption {
		PRIORITY,
		LAND,
		EXACTMATCH,
		USEHORIZONTAAL
	}

	public static class OptionsExtension
	{
		public const string GROUP_NAME = "MARKUP";
		public const int DEFAULT_PRIORITY = 1;

		#region Options

		public static void Set(this OptionsManager manager, 
			MarkupOption option, List<string> symbols, List<dynamic> @params) =>
			manager.Set(GROUP_NAME, option.ToString(), symbols, @params);

		public static bool IsSet(this OptionsManager manager, 
			MarkupOption option, string symbol = null) =>
			manager.IsSet(GROUP_NAME, option.ToString(), symbol);

		public static HashSet<string> GetSymbols(this OptionsManager manager, 
			MarkupOption option) =>
			manager.GetSymbols(GROUP_NAME, option.ToString());

		public static List<dynamic> GetParams(this OptionsManager manager,
			MarkupOption option, string symbol = null) =>
			manager.GetParams(GROUP_NAME, option.ToString(), symbol);

		public static void Clear(this OptionsManager manager, 
			MarkupOption option) =>
			manager.Clear(GROUP_NAME, option.ToString());

		#endregion

		#region SymbolOptions

		public static void Set(this SymbolOptionsManager manager,
			MarkupOption option, List<dynamic> @params = null) =>
			manager.Set(GROUP_NAME, option.ToString(), @params);

		public static bool IsSet(this SymbolOptionsManager manager,
			MarkupOption option) =>
			manager.IsSet(GROUP_NAME, option.ToString());

		public static List<dynamic> GetParams(this SymbolOptionsManager manager,
			MarkupOption option) =>
			manager.GetParams(GROUP_NAME, option.ToString());

		public static void Clear(this SymbolOptionsManager manager,
			MarkupOption option) =>
			manager.Clear(GROUP_NAME, option.ToString());

		#endregion

		#region Кастомные методы для отдельных опций

		public static double? GetPriority(this SymbolOptionsManager manager)
		{
			var parameters = manager.GetParams(MarkupOption.PRIORITY);
			return parameters?.Count > 0 ? parameters[0] : null;
		}

		public static void SetPriority(this SymbolOptionsManager manager, double value)
		{
			manager.Set(MarkupOption.PRIORITY, new List<dynamic>() { value });
		}

		#endregion
	}
}
