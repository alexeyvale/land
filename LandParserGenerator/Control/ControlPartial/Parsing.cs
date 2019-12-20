using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Land.Core;
using Land.Markup;
using Land.Markup.Binding;
using Land.Markup.CoreExtension;

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

		private ParsedFile GetParsed(string documentName)
		{
			return !String.IsNullOrEmpty(documentName)
				/// Если связанный с точкой файл разбирали и он не изменился с прошлого разбора,
				? ParsedFiles.ContainsKey(documentName) && ParsedFiles[documentName] != null
					/// возвращаем сохранённый ранее результат
					? ParsedFiles[documentName]
					/// иначе пытаемся переразобрать файл
					: ParsedFiles[documentName] = TryParse(documentName, null, out bool success)
				: null;
		}

		/// <summary>
		/// Попытка распарсить заданный файл
		/// </summary>
		/// <param name="fileName">Имя файла</param>
		/// <param name="text">Текст, если его не нужно брать из самого файла с именем <paramref name="fileName"/></param>
		/// <param name="success">Признак успешности выполнения операции</param>
		/// <param name="dryRun">Признак того, что нужно выполнить все пре- и пост- операции, кроме самого парсинга</param>
		/// <returns></returns>
		private ParsedFile TryParse(string fileName, string text, out bool success, bool dryRun = false)
		{
			if (!String.IsNullOrEmpty(fileName))
			{
				var extension = Path.GetExtension(fileName);

				if (Parsers[extension] != null)
				{
					if (String.IsNullOrEmpty(text))
						text = GetText(fileName);

					Core.Parsing.Tree.Node root = null;

					if(dryRun)
					{
						root = null;
						success = true;
					}
					else
					{
						root = Parsers[extension].Parse(text);
						success = Parsers[extension].Log.All(l => l.Type != MessageType.Error);

						if(root!=null)
						{
							var visitor = new NodeRetypingVisitor(null);
							root.Accept(visitor);
							root = visitor.Root;
						}

						Parsers[extension].Log.ForEach(l => l.FileName = fileName);
						Log.AddRange(Parsers[extension].Log);
					}

					return success ? new ParsedFile
					{
						Root = root,
						Text = text,
						MarkupSettings = Parsers.GetMarkupSettings(extension),
						BindingContext = PointContext.GetFileContext(fileName, text)
					} : null;
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