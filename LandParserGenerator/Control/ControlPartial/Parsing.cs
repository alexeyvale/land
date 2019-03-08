using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using Microsoft.Win32;

using Land.Core;
using Land.Core.Parsing;
using Land.Core.Parsing.Tree;
using Land.Core.Parsing.Preprocessing;
using Land.Core.Markup;
using Land.Control.Helpers;

namespace Land.Control
{
	public partial class LandExplorerControl : UserControl
	{
		public ParserManager Parsers { get; private set; } = new ParserManager();

		private void ReloadParsers()
		{
			Log.Clear();

			foreach (var ext in Parsers.Load(SettingsObject, CACHE_DIRECTORY, Log))
				foreach(var file in ParsedFiles.Keys.Where(f=>Path.GetExtension(f) == ext).ToList())
					DocumentChangedHandler(file);
		}

		private Tuple<Node, string> GetRoot(string documentName)
		{
			return !String.IsNullOrEmpty(documentName)
				/// Если связанный с точкой файл разбирали и он не изменился с прошлого разбора,
				? ParsedFiles.ContainsKey(documentName) && ParsedFiles[documentName] != null
					/// возвращаем сохранённый ранее результат
					? ParsedFiles[documentName]
					/// иначе пытаемся переразобрать файл
					: ParsedFiles[documentName] = TryParse(documentName, out bool success)
				: null;
		}

		private Tuple<Node, string> TryParse(string fileName, out bool success, string text = null)
		{
			if (!String.IsNullOrEmpty(fileName))
			{
				var extension = Path.GetExtension(fileName);

				if (Parsers[extension] != null)
				{
					if (String.IsNullOrEmpty(text))
						text = GetText(fileName);

					var root = Parsers[extension].Parse(text);
					success = Parsers[extension].Log.All(l => l.Type != MessageType.Error);

					Parsers[extension].Log.ForEach(l => l.FileName = fileName);
					Log.AddRange(Parsers[extension].Log);

					return success ? new Tuple<Node, string>(root, text) : null;
				}
				else
				{
					Log.Add(Message.Error($"Отсутствует парсер для файлов с расширением '{extension}'", null));
				}
			}

			success = false;
			return null;
		}
	}
}