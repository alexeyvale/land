using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using Land.Core;
using Land.Core.Parsing;
using Land.Core.Parsing.Preprocessing;
using Land.Control.Helpers;
using Land.Markup.CoreExtension;

namespace Land.Control
{
	public class ParserLoader : MarshalByRefObject
	{
		private const string PARSER_PROVIDER_CLASS = "ParserProvider";
		private const string GET_PARSER_METHOD = "GetParser";

		public BaseParser Parser { get; set; }

		public Message Load(string parserCachedPath, string preprocessorCachedPath, 
			ParserSettingsItem settings)
		{
			var log = new List<Message>();

			#region Загрузка парсера

			try
			{
				Parser = (BaseParser)Assembly.LoadFrom(parserCachedPath)
					.GetTypes().FirstOrDefault(t => t.Name == PARSER_PROVIDER_CLASS)
					?.GetMethod(GET_PARSER_METHOD)?.Invoke(null, null);
			}
			catch (Exception e)
			{
				return Message.Error(
					$"При загрузке библиотеки парсера {settings.ParserPath} " +
						$"для расширения {settings.ExtensionsString} произошла ошибка:{Environment.NewLine}{e.ToString()}",
					null
				);
			}

			if (Parser == null)
			{
				return Message.Error(
					$"Не удалось загрузить парсер для расширения {settings.ExtensionsString}",
					null
				);
			}

			/// Добавляем в коллекцию визиторов парсера визитор для обработки опций разметки
			Parser.SetVisitor(g => new MarkupOptionsProcessingVisitor(g));

			#endregion

			#region Загрузка препроцессора

			if (!String.IsNullOrEmpty(settings.PreprocessorPath))
			{
				BasePreprocessor preprocessor = null;

				try
				{
					preprocessor = (BasePreprocessor)Assembly.LoadFrom(preprocessorCachedPath)
						.GetTypes().FirstOrDefault(t => t.BaseType.Equals(typeof(BasePreprocessor)))
						?.GetConstructor(Type.EmptyTypes).Invoke(null);
				}
				catch (Exception e)
				{
					return Message.Error(
						$"При загрузке библиотеки препроцессора {settings.PreprocessorPath} " +
							$"для расширения {settings.ExtensionsString} произошла ошибка:{Environment.NewLine}{e.ToString()}",
						null
					);
				}

				if (preprocessor == null)
				{
					return Message.Error(
						$"Библиотека {settings.PreprocessorPath} не содержит описание препроцессора " +
							$"для расширения {settings.ExtensionsString}",
						null
					);
				}

				if (settings.PreprocessorProperties != null
					&& settings.PreprocessorProperties.Count > 0)
				{
					ConfigurePreprocessor(preprocessor, preprocessorCachedPath, settings, log);
				}

				Parser.SetPreprocessor(preprocessor);
			}

			#endregion

			return null;
		}

		private void ConfigurePreprocessor(BasePreprocessor preprocessor, string preprocessorLibraryPath, 
			ParserSettingsItem settings, List<Message> log)
		{
			/// Получаем тип препроцессора из библиотеки
			var propertiesObjectType = Assembly.LoadFrom(preprocessorLibraryPath)
				.GetTypes().FirstOrDefault(t => t.BaseType.Equals(typeof(PreprocessorSettings)));

			/// Для каждой настройки препроцессора
			foreach (var property in settings.PreprocessorProperties)
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
						log.Add(Message.Error(
							$"Не удаётся конвертировать строку '{property.ValueString}' в свойство " +
								$"'{property.DisplayedName}' препроцессора для расширения {settings.ExtensionsString}",
							null
						));
					}
				}
			}
		}

		public override object InitializeLifetimeService() => null;
	}
}
