using System;
using System.Collections.Generic;
using System.Linq;

namespace Land.Core.Specification
{
	[Serializable]
	public class SymbolOptionsManager
	{
		/// <summary>
		/// Первый ключ - имя группы, второй - имя опции, для каждой опции хранится список параметров
		/// </summary>
		private Dictionary<string, Dictionary<string, List<dynamic>>> Options { get; set; } =
			new Dictionary<string, Dictionary<string, List<dynamic>>>();

		public SymbolOptionsManager(Dictionary<string, Dictionary<string, List<dynamic>>> options)
		{
			Options = options;
		}

		public SymbolOptionsManager() {}

		/// <summary>
		/// Установка опции с заданными параметрами для текущего символа
		/// </summary>
		public void Set(string group, string option, List<dynamic> @params)
		{
			group = group.ToLower();
			option = option.ToLower();

			EnsureGroupExists(group);

			Options[group][option] = @params;
		}

		/// <summary>
		/// Проверка того, что опция установлена для текущего символа
		/// </summary>
		public bool IsSet(string group, string option = null)
		{
			group = group.ToLower();
			option = option?.ToLower();

			return Options.ContainsKey(group) && (option == null || Options[group].ContainsKey(option));
		}

		/// <summary>
		/// Получение параметров опции для текущего символа
		/// </summary>
		public List<dynamic> GetParams(string group, string option)
		{
			group = group.ToLower();
			option = option.ToLower();

			return IsSet(group, option) ? Options[group][option] : new List<dynamic>();
		}

		/// <summary>
		/// Сброс заданной опции для текущего символа
		/// </summary>
		public void Clear(string group, string option)
		{
			group = group.ToLower();
			option = option.ToLower();

			if (Options.ContainsKey(group))
			{
				Options[group].Remove(option);

				if (Options[group].Count == 0)
					Options.Remove(group);
			}
		}

		/// <summary>
		/// Получение категорий опций, установленных для текущего символа
		/// </summary>
		public List<string> GetGroups() =>
			Options.Select(o => o.Key).ToList();

		/// <summary>
		/// Получение опций из заданной категории, установленных для текущего символа
		/// </summary>
		public List<string> GetOptions(string group)
		{
			group = group.ToLower();

			return Options.ContainsKey(group)
				? Options[group].Select(o => o.Key).ToList() : new List<string>();
		}

		/// <summary>
		/// Клонирование менеджера опций
		/// </summary>
		public SymbolOptionsManager Clone() =>
			 new SymbolOptionsManager
			 {
				 Options = CloneRaw()
			 };

		/// <summary>
		/// Клонирование словаря опций
		/// </summary>
		public Dictionary<string, Dictionary<string, List<dynamic>>> CloneRaw() =>
			this.Options.ToDictionary(
				g => g.Key, g => g.Value.ToDictionary(
					o => o.Key, o => o.Value.ToList())
			);

		private void EnsureGroupExists(string group)
		{
			if (!Options.ContainsKey(group))
				Options[group] = new Dictionary<string, List<dynamic>>();
		}
	}
}
