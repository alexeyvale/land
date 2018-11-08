using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace SameFilesExtractor
{
	class Program
	{
		/// Первые два параметра - пути к каталогам,
		/// третий - паттерн поиска файлов,
		/// последний - путь к папке, в которую надо сохранить результаты
		static void Main(string[] args)
		{
			if(args.Length == 4)
			{
				if (Directory.Exists(args[3]))
					Directory.Delete(args[3], true);
				Directory.CreateDirectory(args[3]);

				var first = Directory.GetFiles(args[0], args[2], SearchOption.AllDirectories)
					.Select(path => path.Substring(args[1].Length));

				var second = Directory.GetFiles(args[1], args[2], SearchOption.AllDirectories)
					.Select(path => path.Substring(args[1].Length));

				var common = first.Intersect(second)
					.Where(name => File.ReadAllText(args[0] + name) != File.ReadAllText(args[1] + name)).ToList();

				foreach (var name in common)
				{
					var counter = 0;
					var testFileName = $"{Path.GetFileNameWithoutExtension(name)}-first{{0}}{Path.GetExtension(name)}";

					while (File.Exists(Path.Combine(args[3], String.Format(testFileName, counter))))
						++counter;

					File.Copy(args[0] + name,
						Path.Combine(args[3], $"{Path.GetFileNameWithoutExtension(name)}-first{counter}{Path.GetExtension(name)}"), true);

					File.Copy(args[1] + name,
						Path.Combine(args[3], $"{Path.GetFileNameWithoutExtension(name)}-second{counter}{Path.GetExtension(name)}"), true);
				}
			}
		}
	}
}
