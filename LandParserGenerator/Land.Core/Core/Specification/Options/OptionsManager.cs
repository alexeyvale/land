using System;
using System.Collections.Generic;
using System.Linq;

namespace Land.Core.Specification
{
	public class OptionsManager: MarshalByRefObject
	{
		private const string LANGUAGE_PARAMETERS_SYMBOL = "";

		private Dictionary<string, SymbolOptionsManager> Options { get; set; } =
			new Dictionary<string, SymbolOptionsManager> {
				/// Сразу заводим элемент для глобальных опций
				{ LANGUAGE_PARAMETERS_SYMBOL, new SymbolOptionsManager() }
			};

		public OptionsManager() { }

		public OptionsManager(Dictionary<string, SymbolOptionsManager> options)
		{
			Options = options;
		}

		/// <summary>
		/// Установка опции для заданного набора символов и параметров
		/// </summary>
		public void Set(string group, string option, List<string> symbols, List<dynamic> @params)
		{
			if (symbols == null || symbols.Count == 0)
			{
				Options[LANGUAGE_PARAMETERS_SYMBOL].Set(group, option, @params);
			}
			else
			{
				foreach (var symbol in symbols)
				{
					if (!Options.ContainsKey(symbol))
						Options[symbol] = new SymbolOptionsManager();
					Options[symbol].Set(group, option, @params);
				}
			}
		}

		/// <summary>
		/// Проверка того, что опция установлена для заданного символа
		/// </summary>
		public bool IsSet(string group, string option, string symbol = null)
		{
			if (String.IsNullOrEmpty(symbol))
				symbol = LANGUAGE_PARAMETERS_SYMBOL;

			return Options[symbol].IsSet(group, option);
		}

		/// <summary>
		/// Получение символов, для которых установлена опция
		/// </summary>
		public HashSet<string> GetSymbols(string group, string option) =>
			new HashSet<string>(Options.Where(o => o.Value.IsSet(group, option)).Select(o => o.Key));

		public HashSet<string> GetSymbols() => new HashSet<string>(Options.Keys);

		/// <summary>
		/// Получение параметров опции для заданного символа
		/// </summary>
		public List<dynamic> GetParams(string group, string option, string symbol = null)
		{
			if (String.IsNullOrEmpty(symbol))
				symbol = LANGUAGE_PARAMETERS_SYMBOL;

			return IsSet(group, option, symbol) 
				? Options[symbol].GetParams(group, option) 
				: new List<dynamic>();
		}

		/// <summary>
		/// Получение опций для заданного символа
		/// </summary>
		public SymbolOptionsManager GetOptions(string symbol = null) =>
			Options.ContainsKey(symbol) 
				? Options[symbol] 
				: symbol == null ? Options[LANGUAGE_PARAMETERS_SYMBOL] : null;

		/// <summary>
		/// Слияние хранимых опций для символа и переданных на вход
		/// </summary>
		public SymbolOptionsManager MergeForSymbol(string symbol, SymbolOptionsManager local)
		{
			var global = Options[symbol];
			var merged = new SymbolOptionsManager();

			/// Если есть локальные опции, они все остаются в неизменном виде
			foreach (var group in local.GetGroups())
				foreach (var option in local.GetOptions(group))
					merged.Set(group, option, local.GetParams(group, option));
			/// Дополняем локальные опции глобальными
			foreach (var group in global.GetGroups())
				foreach (var option in global.GetOptions(group).Except(local.GetOptions(group)))
					merged.Set(group, option, local.GetParams(group, option));

			return merged;
		}

		public void Clear(string group, string option)
		{
			foreach (var opt in Options)
				opt.Value.Clear(group, option);
		}

		public OptionsManager Clone()
		{
			return new OptionsManager
			{
				Options = this.Options.ToDictionary(g => g.Key, g => g.Value.Clone())
			};
		}

		public override object InitializeLifetimeService() => null;
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
