using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Land.Control.Helpers
{
	public static class SettingsSerializer
	{
		public static LandExplorerSettings Deserialize(string fileName, bool generateNewGuids = false)
		{
			var settingsObject = (LandExplorerSettings)null;

			if (File.Exists(fileName))
			{
				try
				{
					var serializer = new DataContractSerializer(
						typeof(LandExplorerSettings),
						new Type[] { typeof(ParserSettingsItem) }
					);

					using (FileStream fs = new FileStream(fileName, FileMode.Open))
					{
						settingsObject = (LandExplorerSettings)serializer.ReadObject(fs);
					}

					if (generateNewGuids)
					{
						settingsObject.Id = new Guid();

						foreach (var parser in settingsObject.Parsers)
						{
							parser.Id = new Guid();
						}
					}

					/// Если в файле прописаны относительные пути, 
					/// разворачиваем их в абсолютные относительно пути к файлу настроек
					Func<string, string> getRootedPath = p =>
						!String.IsNullOrEmpty(p)
							? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fileName), p))
							: p;

					foreach (var parser in settingsObject.Parsers)
					{
						if (!Path.IsPathRooted(parser.ParserPath))
						{
							parser.ParserPath = getRootedPath(parser.ParserPath);
						}

						if (!Path.IsPathRooted(parser.PreprocessorPath))
						{
							parser.PreprocessorPath = getRootedPath(parser.PreprocessorPath);
						}

						foreach (var dep in parser.ParserDependencies.ToList())
						{
							if (!Path.IsPathRooted(dep))
							{
								parser.ParserDependencies.Remove(dep);
								parser.ParserDependencies.Add(getRootedPath(dep));
							}
						}

						foreach (var dep in parser.PreprocessorDependencies.ToList())
						{
							if (!Path.IsPathRooted(dep))
							{
								parser.PreprocessorDependencies.Remove(dep);
								parser.PreprocessorDependencies.Add(getRootedPath(dep));
							}
						}
					}
				}
				catch
				{ }
			}

			return settingsObject;
		}

		public static string Serialize(LandExplorerSettings settings)
		{
			var serializer = new DataContractSerializer(
					typeof(LandExplorerSettings),
					new Type[] { typeof(ParserSettingsItem) }
				);

			using (var memStm = new MemoryStream())
			{
				serializer.WriteObject(memStm, settings);
				memStm.Seek(0, SeekOrigin.Begin);

				using (var streamReader = new StreamReader(memStm))
				{
					return streamReader.ReadToEnd();
				}
			}
		}
	}
}
