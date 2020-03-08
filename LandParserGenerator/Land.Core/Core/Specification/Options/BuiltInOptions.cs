using System;
using System.Collections.Generic;
using System.Linq;

namespace Land.Core.Specification
{
	/// <summary>
	/// Опции, касающиеся процесса разбора
	/// </summary>
	public static class ParsingOption
	{
		public const string GROUP_NAME = "parsing";

		public const string START = "start";
		public const string SKIP = "skip";
		public const string IGNORECASE = "ignorecase";
		public const string FRAGMENT = "fragment";
		public const string IGNOREUNDEFINED = "ignoreundefined";
		public const string RECOVERY = "recovery";
	}

	/// <summary>
	/// Опции, касающиеся построения дерева
	/// </summary>
	public static class NodeOption
	{
		public const string GROUP_NAME = "nodes";

		public const string GHOST = "ghost";
		public const string LIST = "list";
		public const string LEAF = "leaf";
	}

	/// <summary>
	/// Опции, касающиеся выделения псевдосущностей программы
	/// </summary>
	public static class CustomBlockOption
	{
		public const string GROUP_NAME = "customblock";

		public const string START = "start";
		public const string END = "end";
		public const string BASETOKEN = "basetoken";
	}

	public static class OptionsExtension
	{
		public static bool IsRecoveryEnabled(this OptionsManager manager) =>
			manager.GetSymbols().Any(s => manager.IsSet(ParsingOption.GROUP_NAME, ParsingOption.RECOVERY, s));
	}
}
