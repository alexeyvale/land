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
		private const string PARSER_PROVIDER_CLASS = "ParserProvider";
		private const string GET_PARSER_METHOD = "GetParser";

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

		private string CopyLibraryToTmp(string path, HashSet<string> dependencies, string tmpDirectory, out bool success)
		{
			/// Проверяем существование файла, который нужно скопировать
			success = File.Exists(path);

			if (!success)
				return null;

			/// Если в целевом месте точно такой же файл, ничего не делаем
			var tmpLibraryFile = Path.Combine(tmpDirectory, Path.GetFileName(path));

			if (!File.Exists(tmpLibraryFile) 
				|| File.GetLastWriteTimeUtc(path) != File.GetLastWriteTimeUtc(tmpLibraryFile))
			{
				try {
					File.Copy(path, tmpLibraryFile, true);
				}
				catch
				{
					success = false;
					return null;
				}
			}

			/// Аналогично проверяем и копируем зависимости
			foreach (var dependency in dependencies)
			{
				var tmpDependencyFile = Path.Combine(tmpDirectory, Path.GetFileName(dependency));

				if (File.Exists(dependency))
				{
					if (!File.Exists(tmpDependencyFile) 
						|| File.GetLastWriteTimeUtc(dependency) != File.GetLastWriteTimeUtc(tmpDependencyFile))
					{
						try
						{
							File.Copy(dependency, tmpDependencyFile);
						}
						catch
						{
							success = false;
						}
					}
				}
				else
					success = false;
			}

			return tmpLibraryFile;
		}

		private bool EnsureDirectoryExists(string path)
		{
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);

			return Directory.Exists(path);
		}

		private Dictionary<string, BaseParser> LoadParsers()
		{
			var parsers = new Dictionary<string, BaseParser>();

			#region Обеспечиваем существование временных каталогов

			/// Получаем имя каталога, соответствующего текущим настройкам, и создаём его,
			/// если он не существует
			var settingsObjectDirectory = Path.Combine(CACHE_DIRECTORY, SettingsObject.Id.ToString());

			if (!Directory.Exists(settingsObjectDirectory))
				Directory.CreateDirectory(settingsObjectDirectory);

			var hasLockedFiles = false;

			/// Для каждой записи в наборе парсеров создаём свой каталог, если его не существует
			var itemsPaths = SettingsObject.Parsers.Select(item =>
			{
				if (!File.Exists(item.ParserPath))
				{
					Log.Add(Message.Error(
						$"Файл {item.ParserPath} не существует, невозможно загрузить парсер для расширения {item.ExtensionsString}",
						null
					));
					return null;
				}

				if (!String.IsNullOrEmpty(item.PreprocessorPath))
				{
					if (!File.Exists(item.PreprocessorPath))
					{
						Log.Add(Message.Error(
							$"Файл {item.PreprocessorPath} не существует, невозможно загрузить препроцессор для расширения {item.ExtensionsString}",
							null
						));
						return null;
					}
				}

				for (var i = 0; i < 2; ++i)
				{
					var itemDirectory = Path.Combine(settingsObjectDirectory, item.Id.ToString());
					EnsureDirectoryExists(itemDirectory);

					var parserDirectory = Path.Combine(itemDirectory, "parser");
					EnsureDirectoryExists(parserDirectory);

					var preprocDirectory = Path.Combine(itemDirectory, "preprocessor");
					EnsureDirectoryExists(preprocDirectory);

					var tmpParserFile = CopyLibraryToTmp(item.ParserPath, item.ParserDependencies, 
						parserDirectory, out bool parserCopySuccess);

					var tmpPreprocFile = CopyLibraryToTmp(item.PreprocessorPath, item.PreprocessorDependencies, 
						preprocDirectory, out bool preprocCopySuccess);

					/// Если не удалось скопировать более новую версию какой-либо библиотеки на место более старой,
					/// предполагаем, что библиотеки в каталоге уже используются и нужно создать для парсера новый каталог
					if (!parserCopySuccess || (!String.IsNullOrEmpty(item.PreprocessorPath) && !preprocCopySuccess))
					{
						item.Id = Guid.NewGuid();
						hasLockedFiles = true;				
					}
					else
						return new { item, parserPath = tmpParserFile, preprocPath = tmpPreprocFile };
				}

				Log.Add(Message.Error(
					$"Не удалось скопировать в рабочий каталог библиотеки парсера или препроцессора для {item.ExtensionsString}",
					null
				));

				return null;
			}).Where(i => i != null).ToList();

			if (hasLockedFiles)
			{
				Editor.SaveSettings(
					SettingsObject, SETTINGS_DEFAULT_PATH
				);
			}

			#endregion

			/// Генерируем парсер и связываем его с каждым из расширений, указанных для грамматики
			foreach (var itemPaths in itemsPaths)
			{
				BaseParser parser = null;

				try
				{
					parser = (BaseParser)Assembly.LoadFrom(itemPaths.parserPath)
						.GetTypes().FirstOrDefault(t => t.Name == PARSER_PROVIDER_CLASS)
						?.GetMethod(GET_PARSER_METHOD)?.Invoke(null, null);
				}
				catch (Exception e)
				{
					Log.Add(Message.Error(
						$"При загрузке библиотеки парсера {itemPaths.item.ParserPath} " +
							$"для расширения {itemPaths.item.ExtensionsString} произошла ошибка:{Environment.NewLine}{e.ToString()}",
						null
					));
					continue;
				}

				if (parser == null)
				{
					Log.Add(Message.Error(
						$"Не удалось загрузить парсер для расширения {itemPaths.item.ExtensionsString}",
						null
					));
					continue;
				}

                foreach (var key in itemPaths.item.Extensions)
					parsers[key] = parser;

				if (!String.IsNullOrEmpty(itemPaths.item.PreprocessorPath))
				{
					BasePreprocessor preprocessor = null;

					try
					{
						preprocessor = (BasePreprocessor)Assembly.LoadFrom(itemPaths.preprocPath)
							.GetTypes().FirstOrDefault(t => t.BaseType.Equals(typeof(BasePreprocessor)))
							?.GetConstructor(Type.EmptyTypes).Invoke(null);
					}
					catch(Exception e)
					{
						Log.Add(Message.Error(
							$"При загрузке библиотеки препроцессора {itemPaths.item.PreprocessorPath} " +
								$"для расширения {itemPaths.item.ExtensionsString} произошла ошибка:{Environment.NewLine}{e.ToString()}",
							null
						));
						continue;
					}

					if(preprocessor == null)
					{
						Log.Add(Message.Error(
							$"Библиотека {itemPaths.item.PreprocessorPath} не содержит описание препроцессора " +
								$"для расширения {itemPaths.item.ExtensionsString}",
							null
						));
						continue;
					}

					if (itemPaths.item.PreprocessorProperties != null
						&& itemPaths.item.PreprocessorProperties.Count > 0)
					{
						/// Получаем тип препроцессора из библиотеки
						var propertiesObjectType = Assembly.LoadFrom(itemPaths.preprocPath)
							.GetTypes().FirstOrDefault(t => t.BaseType.Equals(typeof(PreprocessorSettings)));

						/// Для каждой настройки препроцессора
						foreach (var property in itemPaths.item.PreprocessorProperties)
						{
							/// проверяем, есть ли такое свойство у объекта
							var propertyInfo = propertiesObjectType.GetProperty(property.PropertyName);

							if (propertyInfo != null)
							{
								var converter = (PropertyConverter)(((ConverterAttribute)propertyInfo
									.GetCustomAttribute(typeof(ConverterAttribute))).ConverterType)
									.GetConstructor(Type.EmptyTypes).Invoke(null);

								try
								{
									propertyInfo.SetValue(preprocessor.Properties, converter.ToValue(property.ValueString));
								}
								catch
								{
									Log.Add(Message.Error(
										$"Не удаётся конвертировать строку '{property.ValueString}' в свойство " +
											$"'{property.DisplayedName}' препроцессора для расширения {itemPaths.item.ExtensionsString}",
										null
									));
								}
							}
						}
					}

					parser.SetPreprocessor(preprocessor);
				}
			}

			return parsers;
		}

		private Tuple<Node, string> TryParse(string fileName, out bool success, string text = null)
		{
			if (!String.IsNullOrEmpty(fileName))
			{
				var extension = Path.GetExtension(fileName);

				if (Parsers.ContainsKey(extension) && Parsers[extension] != null)
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