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

		private string CopyLibraryToTmp(string path, HashSet<string> dependencies)
		{
			var tmpDirectory = Path.Combine(TMP_DIRECTORY, Path.GetRandomFileName());

			while (Directory.Exists(tmpDirectory))
				tmpDirectory = Path.Combine(TMP_DIRECTORY, Path.GetRandomFileName());

			Directory.CreateDirectory(tmpDirectory);

			var tmpLibraryFile = Path.Combine(tmpDirectory, Path.GetFileName(path));
			File.Copy(path, tmpLibraryFile);

			foreach (var dependency in dependencies)
			{
				if (File.Exists(dependency))
					File.Copy(dependency, Path.Combine(tmpDirectory, Path.GetFileName(dependency)));
			}

			return tmpLibraryFile;
		}

		private Dictionary<string, BaseParser> LoadParsers()
		{
			var parsers = new Dictionary<string, BaseParser>();

			/// Генерируем парсер и связываем его с каждым из расширений, 
			/// указанных для грамматики
			foreach (var item in SettingsObject.Parsers)
			{
				if(!File.Exists(item.ParserPath))
				{
					Log.Add(Message.Error(
						$"Файл {item.ParserPath} не существует, невозможно загрузить парсер для расширения {item.ExtensionsString}",
						null
					));

					continue;
				}

				var tmpParserFile = CopyLibraryToTmp(item.ParserPath, item.ParserDependencies);

				var parser = (BaseParser)Assembly.LoadFrom(tmpParserFile)
							.GetTypes().FirstOrDefault(t => t.Name == PARSER_PROVIDER_CLASS)
							?.GetMethod(GET_PARSER_METHOD)?.Invoke(null, null);

				if(parser == null)
				{
					Log.Add(Message.Error(
						$"Не удалось загрузить парсер для расширения {item.ExtensionsString}",
						null
					));

					continue;
				}

                foreach (var key in item.Extensions)
					parsers[key] = parser;

				if (!String.IsNullOrEmpty(item.PreprocessorPath))
				{
					if (!File.Exists(item.PreprocessorPath))
					{
						Log.Add(Message.Error(
							$"Файл {item.PreprocessorPath} не существует, невозможно загрузить препроцессор для расширения {item.ExtensionsString}",
							null
						));
					}
					else
					{
						var tmpPreprocFile = CopyLibraryToTmp(item.PreprocessorPath, item.PreprocessorDependencies);

						var preprocessor = (BasePreprocessor)Assembly.LoadFrom(tmpPreprocFile)
							.GetTypes().FirstOrDefault(t => t.BaseType.Equals(typeof(BasePreprocessor)))
							?.GetConstructor(Type.EmptyTypes).Invoke(null);

						if (preprocessor != null)
						{
							if (item.PreprocessorProperties != null
								&& item.PreprocessorProperties.Count > 0)
							{
								/// Получаем тип препроцессора из библиотеки
								var propertiesObjectType = Assembly.LoadFrom(item.PreprocessorPath)
									.GetTypes().FirstOrDefault(t => t.BaseType.Equals(typeof(PreprocessorSettings)));

								/// Для каждой настройки препроцессора
								foreach (var property in item.PreprocessorProperties)
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
												$"Не удаётся конвертировать строку '{property.ValueString}' в свойство '{property.DisplayedName}' препроцессора для расширения {item.ExtensionsString}",
												null
											));
										}
									}
								}
							}

							parser.SetPreprocessor(preprocessor);
						}
						else
						{
							Log.Add(Message.Error(
								$"Библиотека {item.PreprocessorPath} не содержит описание препроцессора для расширения {item.ExtensionsString}",
								null
							));
						}
					}
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