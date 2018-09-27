using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Text;

using Microsoft.CSharp;

using SpecParsing = Land.Core.Builder;

using Land.Core.Parsing;
using Land.Core.Parsing.LL;
using Land.Core.Parsing.LR;

namespace Land.Core
{
	public static class BuilderBase
	{
		public static Type BuildLexer(Grammar grammar, string lexerName, List<Message> errors = null)
		{
			/// Генерируем по грамматике файл для ANTLR
			var grammarOutput = new StreamWriter($"{lexerName}.g4");

			grammarOutput.WriteLine($"lexer grammar {lexerName};");
			grammarOutput.WriteLine();
			grammarOutput.WriteLine(@"WS: [ \n\r\t]+ -> skip ;");

			/// Запоминаем соответствия между строчкой в генерируемом файле 
			/// и тем терминалом, который оказывается на этой строчке
			var linesCounter = 3;
			var tokensForLines = new Dictionary<int, string>();

			foreach (var token in grammar.Tokens.Values.Where(t => t.Name.StartsWith(Grammar.AUTO_TOKEN_PREFIX)))
			{
				grammarOutput.WriteLine($"{token.Name}: {token.Pattern} ;");
				tokensForLines[++linesCounter] = token.Name.StartsWith(Grammar.AUTO_TOKEN_PREFIX) ? token.Pattern : token.Name;
			}

			foreach (var token in grammar.TokenOrder.Where(t=>!String.IsNullOrEmpty(grammar.Tokens[t].Pattern)))
			{
				var fragment = grammar.Options.GetSymbols(ParsingOption.FRAGMENT).Contains(token) ? "fragment " : String.Empty;
				grammarOutput.WriteLine($"{fragment}{token}: {grammar.Tokens[token].Pattern} ;");
				tokensForLines[++linesCounter] = grammar.Userify(token);
			}

			if(grammar.Options.IsSet(ParsingOption.IGNOREUNDEFINED))
				grammarOutput.WriteLine(@"UNDEFINED: . -> skip ;");
			else
				grammarOutput.WriteLine(@"UNDEFINED: . ;");

			grammarOutput.Close();

			/// Запускаем ANTLR и получаем файл лексера

			Process process = new Process();
			ProcessStartInfo startInfo = new ProcessStartInfo()
			{
				FileName = "cmd.exe",
				Arguments = $"/C java -jar \"../../../components/Antlr/antlr-4.7-complete.jar\" -Dlanguage=CSharp {lexerName}.g4",
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false
			};
			process.StartInfo = startInfo;
			process.Start();

			var antlrOutput = process.StandardOutput.ReadToEndAsync();
			var antlrErrors = process.StandardError.ReadToEndAsync();

			process.WaitForExit();

			/// Если есть ошибки, приводим их к виду, больше соответствующему .land-файлу
			if(!String.IsNullOrEmpty(antlrErrors.Result))
			{
				var errorsList = antlrErrors.Result.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
				foreach(var error in errorsList)
				{
					try
					{ 
						/// 1 - имя .g4 файла, 2 - номер строки, 3 - номер столбца, 4 - соо об ошибке
						var parts = error.Split(new char[] { ':' }, 5);

						/// проверяем, не упоминаются ли в соо об ошибке автотерминалы
						var autoNames = System.Text.RegularExpressions.Regex.Matches(parts[4], $"{Grammar.AUTO_TOKEN_PREFIX}[0-9]+");
						foreach(System.Text.RegularExpressions.Match name in autoNames)
						{
							parts[4] = parts[4].Replace(name.Value, grammar.Userify(name.Value));
						}

						var errorToken = tokensForLines[int.Parse(parts[2])];
						var possibleAnchor = grammar.GetAnchor(errorToken);

						errors.Add(Message.Error($"Token {errorToken}: {parts[4]}", possibleAnchor, "ANTLR Scanner Generator"));
					}
					catch
					{
						errors.Add(Message.Error(error, null, "ANTLR Scanner Generator"));
					}
				}
			}

			/// Компилируем .cs-файл лексера

			var codeProvider = new CSharpCodeProvider();

			var compilerParams = new System.CodeDom.Compiler.CompilerParameters();
			compilerParams.GenerateInMemory = true;
			compilerParams.ReferencedAssemblies.Add("Antlr4.Runtime.Standard.dll");
			compilerParams.ReferencedAssemblies.Add("System.dll");

			var compilationResult = codeProvider.CompileAssemblyFromFile(compilerParams, $"{lexerName}.cs");
			return compilationResult.CompiledAssembly.GetType(lexerName);
		}

		public static Grammar BuildGrammar(GrammarType type, string text, List<Message> log)
		{
			var scanner = new SpecParsing.Scanner();
			scanner.SetSource(text, 0);

			var specParser = new SpecParsing.Parser(scanner);
			specParser.ConstructedGrammar = new Grammar(type);

			var success = specParser.Parse();

			log.AddRange(specParser.Log);
			log.AddRange(scanner.Log);

			if (!success)
			{
				return null;
			}

			return specParser.ConstructedGrammar;
		}

		public static BaseParser BuildParser(GrammarType type, string text, List<Message> messages)
		{
			var builtGrammar = BuildGrammar(type, text, messages);

			if (messages.Count(m=>m.Type == MessageType.Error) == 0)
			{
				builtGrammar.RebuildUserificationCache();

				BaseTable table = null;

				switch(type)
				{
					case GrammarType.LL:
						table = new TableLL1(builtGrammar);
						break;
					case GrammarType.LR:
						table = new TableLR1(builtGrammar);
						break;
				}

				messages.AddRange(table.CheckValidity());

				table.ExportToCsv("current_table.csv");

				var lexerType = BuildLexer(builtGrammar, "CurrentLexer", messages);

				/// Создаём парсер
				BaseParser parser = null;
				switch (type)
				{
					case GrammarType.LL:
						parser = new Parsing.LL.Parser(builtGrammar,
							new AntlrLexerAdapter(
								(Antlr4.Runtime.ICharStream stream) => (Antlr4.Runtime.Lexer)Activator.CreateInstance(lexerType, stream)
							)
						);
						break;
					case GrammarType.LR:
						parser = new Parsing.LR.Parser(builtGrammar,
							new AntlrLexerAdapter(
								(Antlr4.Runtime.ICharStream stream) => (Antlr4.Runtime.Lexer)Activator.CreateInstance(lexerType, stream)
							)
						);
						break;
				}

				return parser;
			}
			else
			{
				return null;
			}
		}

		/// <summary>
		/// Генерация библиотеки с парсером
		/// </summary>
		/// <param name="type">Тип парсера (LL или LR)</param>
		/// <param name="text">Грамматика разбираемого формата файлов</param>
		/// <param name="namespace">Пространство имён для сгенерированного парсера</param>
		/// <param name="path">Путь к файлу генерируемой библиотеки</param>
		/// <param name="messages">Лог генерации парсера</param>
		/// <returns>Признак успешности выполнения операции</returns>
		public static bool GenerateLibrary(GrammarType type, string text, string @namespace, string path, string keyPath, List<Message> messages)
		{
			/// Строим объект грамматики и проверяем, корректно ли прошло построение
			var builtGrammar = BuildGrammar(type, text, messages);

			if (messages.Count(m => m.Type == MessageType.Error) == 0)
			{
				builtGrammar.RebuildUserificationCache();

				/// Строим таблицу и проверяем, соответствует ли она указанному типу грамматики
				BaseTable table = null;

				switch (type)
				{
					case GrammarType.LL:
						table = new TableLL1(builtGrammar);
						break;
					case GrammarType.LR:
						table = new TableLR1(builtGrammar);
						break;
				}

				messages.AddRange(table.CheckValidity());

				if (messages.Count(m => m.Type == MessageType.Error) == 0)
				{
					var lexerFileName = $"{@namespace.Replace('.', '_')}_Lexer.cs";
					var parserFileName = $"{@namespace.Replace('.', '_')}_Parser.cs";
					var grammarFileName = $"{@namespace.Replace('.', '_')}_Grammar.cs";

					BuildLexer(builtGrammar, Path.GetFileNameWithoutExtension(lexerFileName) , messages);
					File.WriteAllText(grammarFileName, GetGrammarProviderText(builtGrammar, @namespace));
					File.WriteAllText(parserFileName, GetParserProviderText(@namespace));

					if (!String.IsNullOrEmpty(keyPath) && !File.Exists(keyPath))
					{
						/// Создаём файл ключа
						Process process = new Process();
						ProcessStartInfo startInfo = new ProcessStartInfo()
						{
							FileName = "cmd.exe",
							Arguments = $"/C \"../../../components/Microsoft SDK/sn.exe\" -k \"{keyPath}\"",
							CreateNoWindow = true,
							RedirectStandardOutput = true,
							UseShellExecute = false
						};
						process.StartInfo = startInfo;
						process.Start();

						process.WaitForExit();
					}

					/// Компилируем библиотеку
					var codeProvider = new CSharpCodeProvider(); ;
					var compilerParams = new System.CodeDom.Compiler.CompilerParameters();

					compilerParams.GenerateInMemory = false;
					compilerParams.OutputAssembly = Path.Combine(path, $"{@namespace}.dll");
					compilerParams.ReferencedAssemblies.Add("Antlr4.Runtime.Standard.dll");
					compilerParams.ReferencedAssemblies.Add("Land.Core.dll");
					compilerParams.ReferencedAssemblies.Add("System.dll");
					compilerParams.ReferencedAssemblies.Add("System.Core.dll");
					compilerParams.ReferencedAssemblies.Add("mscorlib.dll");

					if(!String.IsNullOrEmpty(keyPath))
						compilerParams.CompilerOptions = $"/keyfile:\"{keyPath}\"";

					var compilationResult = codeProvider.CompileAssemblyFromFile(compilerParams, lexerFileName, grammarFileName, parserFileName);

					if (compilationResult.Errors.Count == 0)
					{
						return true;
					}
					else
					{
						foreach(System.CodeDom.Compiler.CompilerError error in compilationResult.Errors)
						{
							if (error.IsWarning)
							{
								messages.Add(Message.Warning(
									$"Предупреждение: {error.FileName}; ({error.Line}, {error.Column}); {error.ErrorText}",
									null,
									"C# Compiler"
								));
							}
							else
							{
								messages.Add(Message.Error(
									$"Ошибка: {error.FileName}; ({error.Line}, {error.Column}); {error.ErrorText}",
									null,
									"C# Compiler"
								));
							}
						}

						return messages.All(m => m.Type != MessageType.Error);
					}
				}
			}

			return false;
		}

		private static string GetGrammarProviderText(Grammar grammar, string @namespace)
		{
			return
@"
using Land.Core;
using System.Collections.Generic;

namespace " + @namespace + @"
{
	public static class GrammarProvider
	{
		public static Grammar GetGrammar()
		{
" + String.Join(Environment.NewLine, grammar.ConstructionLog.Select(rec => $"\t\t\t{rec}")) + @"
			grammar.ForceValid();
			return grammar;
		}
	}
}";
		}

		private static string GetParserProviderText(string @namespace)
		{
			return
@"
using System;
using System.Reflection;
using Land.Core;
using Land.Core.Parsing;

namespace " + @namespace + @"
{
	public static class ParserProvider
	{
		public static BaseParser GetParser()
		{
			var grammar = GrammarProvider.GetGrammar();
			var lexerType = Assembly.GetExecutingAssembly().GetType(""" + @namespace.Replace('.', '_') + @"_Lexer"");

			BaseParser parser = null;

			switch (grammar.Type)
			{
				case GrammarType.LL:
					parser = new Land.Core.Parsing.LL.Parser(grammar,
						new AntlrLexerAdapter(
							(Antlr4.Runtime.ICharStream stream) => (Antlr4.Runtime.Lexer)Activator.CreateInstance(lexerType, stream)
						)
					);
					break;
				case GrammarType.LR:
					parser = new Land.Core.Parsing.LR.Parser(grammar,
						new AntlrLexerAdapter(
							(Antlr4.Runtime.ICharStream stream) => (Antlr4.Runtime.Lexer)Activator.CreateInstance(lexerType, stream)
						)
					);
					break;
			}

			return parser;
		}
	}
}";
		}
	}
}
