using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using Land.Core.Parsing;
using Land.Core;
using Land.Markup;

namespace Land.Control
{
	public class ParserManager
	{
		private Dictionary<string, ParserInfo> Parsers { get; set; } = 
			new Dictionary<string, ParserInfo> ();

		public BaseParser this[string ext] => Parsers.ContainsKey(ext) ? Parsers[ext].Parser : null;

		public LanguageMarkupSettings GetMarkupSettings(string ext) => Parsers.ContainsKey(ext) ? Parsers[ext].MarkupSettings : null;

		public HashSet<string> Load(LandExplorerSettings settingsObject, string cacheDirectoryPath, List<Message> log)
		{
			/// Получаем имя каталога, соответствующего текущим настройкам, и создаём его, если он не существует
			var settingsObjectDirectory = Path.Combine(cacheDirectoryPath, settingsObject.Id.ToString());
			EnsureDirectoryExists(settingsObjectDirectory);

			/// Запоминаем расширения, для которых надо переразобрать файлы и перепривязать точки
			var invalidatedExtensions = new HashSet<string>();

			/// Запоминаем ранее загруженные парсеры
			var oldParsers = Parsers.ToDictionary(kvp=>kvp.Key, kvp => new {
				ParserId = kvp.Value.Domain.FriendlyName,
				Unloaded = false,
				DomainParserPair = kvp.Value
			});
			Parsers = new Dictionary<string, ParserInfo>();

			foreach (var parserItem in settingsObject.Parsers)
			{
				/// Ищем существующий домен, в который загружена старая версия парсера
				var existingDomain = oldParsers.Values
					.FirstOrDefault(i => !i.Unloaded && i.ParserId == parserItem.Id.ToString())
					?.DomainParserPair.Domain;

				/// Проверяем, нужно ли перезагрузить текущий парсер
				var response = RefreshParserCache(settingsObjectDirectory, parserItem, existingDomain, log);
				/// Если при проверке и обновлении кеша не произошло критических ошибок
				if (response?.NoErrors ?? false)
				{
					/// Если кеш обновился или парсер до этого не загружали
					if (response.CacheRefreshed || existingDomain == null)
					{
						var libraryDomain = AppDomain.CreateDomain(parserItem.Id.ToString());
						var loader = (ParserLoader)libraryDomain.CreateInstanceAndUnwrap(
							typeof(ParserLoader).Assembly.FullName,
							typeof(ParserLoader).FullName
						);

						var loadingResult = loader.Load(response.ParserCachedPath, response.PreprocessorCachedPath, parserItem);
						if (loadingResult != null)
							log.Add(loadingResult);

						if (loadingResult?.Type != MessageType.Error)
							foreach (var ext in parserItem.Extensions)
								Parsers[ext] = new ParserInfo
								{
									Domain = libraryDomain,
									Parser = loader.Parser,
									MarkupSettings = new LanguageMarkupSettings(
										loader.Parser.GrammarObject.Options.GetOptions())
								};

						invalidatedExtensions.UnionWith(parserItem.Extensions);
					}
					/// Иначе можно использовать уже подгруженные библиотеки
					else
					{
						foreach (var ext in parserItem.Extensions)
							Parsers[ext] = oldParsers.FirstOrDefault(p => p.Value.ParserId == parserItem.Id.ToString())
								.Value.DomainParserPair;
					}
				}
			}

			/// Удаляем неиспользуемые закешированные парсеры для данного файла настроек
			var directoriesInUse = new HashSet<string>(Parsers.Values.Select(v => v.Domain.FriendlyName));

			foreach(var directory in Directory.GetDirectories(settingsObjectDirectory))
			{
				if (!directoriesInUse.Contains(Path.GetFileName(directory)))
				{
					try { Directory.Delete(directory, true); } catch { }
				}
			}

			invalidatedExtensions.UnionWith(oldParsers.Where(kvp => !Parsers.ContainsKey(kvp.Key)
					|| kvp.Value.ParserId != Parsers[kvp.Key].Domain.FriendlyName).Select(kvp => kvp.Key));

			return invalidatedExtensions;
		}

		private bool EnsureDirectoryExists(string path)
		{
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);

			return Directory.Exists(path);
		}

		private RefreshParserCacheResponse RefreshParserCache(string settingsObjectDirectory, ParserSettingsItem item, 
			AppDomain existingDomain, List<Message> log)
		{
			var response = new List<RefreshParserCacheResponse>();
			var localLog = new List<Message>();

			for (var i = 0; i < 2; ++i)
			{
				localLog.Clear();

				/// Убеждаемся в существовании директорий для кеша библиотек парсера и препроцессора
				var itemDirectory = Path.Combine(settingsObjectDirectory, item.Id.ToString());
				EnsureDirectoryExists(itemDirectory);

				var parserDirectory = Path.Combine(itemDirectory, "parser");
				EnsureDirectoryExists(parserDirectory);

				var preprocDirectory = Path.Combine(itemDirectory, "preprocessor");
				EnsureDirectoryExists(preprocDirectory);

				/// Получаем набор действий, которые необходимо произвести при кешировании
				var parserCacheActions = GetCacheActions(item.ParserPath, item.ParserDependencies,
					parserDirectory, out string parserCachedPath);
				var preprocessorCacheActions = GetCacheActions(item.PreprocessorPath, item.PreprocessorDependencies, 
					preprocDirectory, out string preprocessorCachedPath);
				var cacheActions = parserCacheActions.Concat(preprocessorCacheActions).ToList();

				/// Если какие-то действия нужны, выгружаем домен текущего парсера
				if (existingDomain != null && cacheActions.Count > 0)
				{
					AppDomain.Unload(existingDomain);
					existingDomain = null;
				}

				foreach (var action in parserCacheActions)
					DoCacheActions(action, CacheActionLibraryType.Parser, 
						item.ExtensionsString, localLog);

				foreach (var action in preprocessorCacheActions)
					DoCacheActions(action, CacheActionLibraryType.Preprocessor,
						item.ExtensionsString, localLog);

				if (cacheActions.Any(a => !a.Success ?? true))
					item.Id = Guid.NewGuid();
				else
				{
					return new RefreshParserCacheResponse()
					{
						NoErrors = true,
						CacheRefreshed = cacheActions.Count > 0,
						ParserCachedPath = parserCachedPath,
						PreprocessorCachedPath = preprocessorCachedPath
					};
				}
			}

			log.AddRange(localLog);

			return new RefreshParserCacheResponse()
			{
				NoErrors = false
			};
		}

		private void DoCacheActions(CacheAction action, CacheActionLibraryType libraryType, 
			string extensionsString, List<Message> log)
		{
			switch (action.ActionType)
			{
				case CacheActionType.Copy:
					if (!String.IsNullOrEmpty(action.SourcePath) && !File.Exists(action.SourcePath))
					{
						log.Add(Message.Error(
							$"Файл {action.SourcePath} не существует, ошибка при загрузке " +
								$"{(libraryType == CacheActionLibraryType.Parser ? "парсера" : "препроцессора")} " +
								$"для расширения {extensionsString}",
							null
						));

						action.Success = false;
						return;
					}

					try
					{
						File.Copy(action.SourcePath, action.TargetPath);
						action.Success = true;
					}
					catch (Exception e)
					{
						log.Add(Message.Error(
							$"Не удалось скопировать в кеш библиотек {(libraryType == CacheActionLibraryType.Parser ? "парсера" : "препроцессора")}" +
								$" для расширения {extensionsString}" +
								$" файл {action.SourcePath}, произошло исключение:{Environment.NewLine}{e.ToString()}",
							null
						));
						action.Success = false;
					}

					break;
				case CacheActionType.Delete:
					try
					{
						File.Delete(action.TargetPath);
						action.Success = true;
					}
					catch (Exception e)
					{
						log.Add(Message.Warning(
							$"Не удалось удалить из кеша библиотек {(libraryType == CacheActionLibraryType.Parser ? "парсера" : "препроцессора")}" +
								$" для расширения {extensionsString}" +
								$" файл {Path.GetFileName(action.TargetPath)}, произошло исключение:{Environment.NewLine}{e.ToString()}",
							null
						));
						action.Success = false;
					}

					break;
			}
		}

		private List<CacheAction> GetCacheActions(string sourceLibraryPath, HashSet<string> sourceDependencyPaths, 
			string targetDirectory, out string targetLibraryPath)
		{
			if(String.IsNullOrWhiteSpace(sourceLibraryPath))
			{
				targetLibraryPath = null;
				return new List<CacheAction>();
			}

			var relevantTargetFiles = new HashSet<string>();
			targetLibraryPath = Path.Combine(targetDirectory, Path.GetFileName(sourceLibraryPath));
			var sourceFiles = new HashSet<string>(sourceDependencyPaths) { sourceLibraryPath };

			var response = new List<CacheAction>();

			foreach (var sourceFile in sourceFiles)
			{
				var targetFile = Path.Combine(targetDirectory, Path.GetFileName(sourceFile));
				relevantTargetFiles.Add(targetFile);

				if (!File.Exists(targetFile)
					|| File.GetLastWriteTimeUtc(sourceFile) != File.GetLastWriteTimeUtc(targetFile))
				{
					response.Add(new CacheAction()
					{
						SourcePath = sourceFile,
						TargetPath = targetFile,
						ActionType = CacheActionType.Copy
					});
				}
			}

			foreach(var targetFile in Directory.GetFiles(targetDirectory).Except(relevantTargetFiles))
				response.Add(new CacheAction()
				{
					TargetPath = targetFile,
					ActionType = CacheActionType.Delete
				});

			return response;
		}
	}
}
