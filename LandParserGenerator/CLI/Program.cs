using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

using Land.Core;
using Land.Core.Parsing.LL;

namespace Land.CLI
{
	class Program
	{
		private const string GenerateSyntax = "--generate <имя_файла_грамматики> [<имя_результирующего_файла>]";
		private const string ParseSyntax = "--parse <имя_текстового_или_бинарного_файла_грамматики> <имя_разбираемого_файла> [<имя_файла_для_сохранения_дерева>]";

		static void Main(string[] args)
		{
			if (args != null && args.Length > 0)
			{
				switch (args[0])
				{
					case "--generate":
						var grammarFileName = args.Length > 1 ? args[1] : String.Empty;
						var compiledFileName = args.Length > 2 ? args[2] : grammarFileName + ".bin";
						Generate(grammarFileName, compiledFileName);
						break;
					case "--parse":
						var grammarOrParserFileName = args.Length > 1 ? args[1] : String.Empty;
						var targetFileName = args.Length > 2 ? args[2] : String.Empty;
						var treeFileName = args.Length > 3 ? args[3] : targetFileName + ".land.tree.bin";
						Parse(grammarOrParserFileName, targetFileName, treeFileName);
						break;
					default:
						Console.WriteLine($"Неизвестная команда {args[0]}");
						return;
				}
			}
			else
			{
				Console.WriteLine(GenerateSyntax);
				Console.WriteLine(ParseSyntax);
			}
		}

		#region Commands

		private static Parser Generate(string grammarFileName, string compiledFileName = null)
		{
			/// Проверяем, есть ли файл грамматики
			if (String.IsNullOrEmpty(grammarFileName))
			{
				Console.WriteLine($"Отсутствует обязательный аргумент команды. Корректный синтаксис: {GenerateSyntax}");
				return null;
			}
			if (!File.Exists(grammarFileName))
			{
				Console.WriteLine($"Файла с именем {grammarFileName} не существует");
				return null;
			}

			/// Генерируем парсер
			var log = new List<Message>();
			var parser = BuilderLL.BuildParser(File.ReadAllText(grammarFileName), log);

			/// Проверяем, были ли ошибки при генерации
			var hasErrors = log.Any(l => l.Type == MessageType.Error);

			foreach (var l in log.Where(l => l.Type == MessageType.Error || l.Type == MessageType.Warning))
				Console.WriteLine($"{l.Type} {l.ToString()}");

			if (hasErrors)
				return null;

			/// Если передано имя файла, в который нужно сериализовать сгенерированный парсер
			if (!String.IsNullOrEmpty(compiledFileName))
				Serialize(compiledFileName, parser);

			return parser;
		}

		private static void Parse(string grammarOrParserFileName, string targetFileName, string treeFileName)
		{
			if(String.IsNullOrEmpty(grammarOrParserFileName) || String.IsNullOrEmpty(targetFileName))
			{
				Console.WriteLine($"Отсутствует обязательный аргумент команды. Корректный синтаксис:{Environment.NewLine}{ParseSyntax}");
				return;
			}

			var parser = grammarOrParserFileName.EndsWith(".land") 
				? Generate(grammarOrParserFileName) 
				: Deserialize<Parser>(grammarOrParserFileName);

			var root = parser.Parse(File.ReadAllText(targetFileName));

			foreach (var l in parser.Log.Where(l => l.Type == MessageType.Error || l.Type == MessageType.Warning))
				Console.WriteLine($"{l.Type} {l.ToString()}");

			if (root != null && !String.IsNullOrEmpty(treeFileName))
				Serialize(treeFileName, root);
		}

		#endregion Commands

		#region Methods

		private static void Serialize<T>(string fileName, T obj)
		{
			var fs = new FileStream(fileName, FileMode.Create);
			var formatter = new BinaryFormatter();

			try
			{
				formatter.Serialize(fs, obj);
			}
			finally
			{
				fs.Close();
			}
		}

		private static T Deserialize<T>(string fileName)
		{
			var fs = new FileStream(fileName, FileMode.Open);
			var formatter = new BinaryFormatter();

			try
			{
				return (T)formatter.Deserialize(fs);
			}
			finally
			{
				fs.Close();
			}
		}

		#endregion Methods
	}
}
