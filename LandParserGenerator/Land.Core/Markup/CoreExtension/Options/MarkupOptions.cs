using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Specification;

namespace Land.Markup.CoreExtension
{
	/// <summary>
	/// Опции, касающиеся отображения старого дерева в новое (изменённое) дерево
	/// </summary>
	public static class MarkupOption
	{
		public const string GROUP_NAME = "markup";

		public const string PRIORITY = "priority";
		public const string LAND = "land";
		public const string EXACTMATCH = "exactmatch";
		public const string USESIBLINGS = "usesiblings";
	}

	public static class OptionsExtension
	{	
		public const int DEFAULT_PRIORITY = 1;

		public static double? GetPriority(this SymbolOptionsManager manager)
		{
			var parameters = manager.GetParams(MarkupOption.GROUP_NAME, MarkupOption.PRIORITY);
			return parameters?.Count > 0 ? parameters[0] : null;
		}

		public static void SetPriority(this SymbolOptionsManager manager, double value)
		{
			manager.Set(MarkupOption.GROUP_NAME, MarkupOption.PRIORITY, new List<dynamic>() { value });
		}
	}
}
