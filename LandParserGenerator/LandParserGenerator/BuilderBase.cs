using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;

using Microsoft.CSharp;

using SpecParsing = LandParserGenerator.Builder;

using LandParserGenerator.Parsing;
using LandParserGenerator.Parsing.LL;
using LandParserGenerator.Parsing.LR;

namespace LandParserGenerator
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
				WindowStyle = ProcessWindowStyle.Hidden,
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

		public static Grammar BuildGrammar(GrammarType type, string text, List<Message> errors)
		{
			var scanner = new SpecParsing.Scanner();
			scanner.SetSource(text, 0);

			var specParser = new SpecParsing.Parser(scanner);
			specParser.ConstructedGrammar = new Grammar(type);

			var success = specParser.Parse();

			errors.AddRange(specParser.Errors);
			errors.AddRange(scanner.Log);

			if (!success)
			{
				//errors.Add(Message.Error(
				//	$"При генерации парсера произошла ошибка: встречена неожиданная лексема {scanner.yytext}",
				//	scanner.yylloc.StartLine,
				//	scanner.yylloc.StartColumn,
				//	"LanD"
				//));

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
	}
}
