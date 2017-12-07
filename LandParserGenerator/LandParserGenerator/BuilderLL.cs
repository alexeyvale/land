using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CSharp;

using LandParserGenerator.Parsing.LL;

namespace LandParserGenerator
{
	public static class BuilderLL
	{
		private static Type BuildLexer(Grammar grammar, string lexerName)
		{
			/// Генерируем по грамматике файл для ANTLR

			var grammarOutput = new StreamWriter($"{lexerName}.g4");

			grammarOutput.WriteLine($"lexer grammar {lexerName};");
			grammarOutput.WriteLine();
			grammarOutput.WriteLine(@"WS: [ \n\r\t]+ -> skip ;");

			foreach (var token in grammar.TokenOrder.Where(t=>!String.IsNullOrEmpty(grammar.Tokens[t].Pattern)))
			{
				/// На уровне лексера распознаём только лексемы для обычных токенов
				if (!grammar.SpecialTokens.Contains(token))
				{
                    /// Если токен служит только для описания других токенов - это fragment
                    var isFragment = "";// grammar.SkipTokens.Contains(token) 
                        //|| grammar.Rules.SelectMany(r => r.Value.Alternatives).Any(a=>a.Contains(token)) ?
						//"" : "fragment ";
					grammarOutput.WriteLine($"{isFragment}{token}: {grammar.Tokens[token].Pattern} ;");
                }
			}

			grammarOutput.WriteLine(@"UNDEFINED: . -> skip ;");

			grammarOutput.Close();

			/// Запускаем ANTLR и получаем файл лексера

			Process process = new Process();
			ProcessStartInfo startInfo = new ProcessStartInfo()
			{
				FileName = "cmd.exe",
				Arguments = $"/C java -jar \"../../../components/Antlr/antlr-4.7-complete.jar\" -Dlanguage=CSharp {lexerName}.g4",
				WindowStyle = ProcessWindowStyle.Hidden
			};
			process.StartInfo = startInfo;
			process.Start();

			while (!process.HasExited)
			{
				System.Threading.Thread.Sleep(0);
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

		public static Parser BuildYacc()
		{
			Grammar yaccGrammar = new Grammar();

			/// Пропускаемые сущности
			yaccGrammar.DeclareTerminal(new TerminalSymbol("COMMENT_L", @"'//' ~[\n\r]*"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("COMMENT_ML", "'/*' .*? '*/'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("COMMENT", "COMMENT_L|COMMENT_ML"));

            yaccGrammar.DeclareTerminal(new TerminalSymbol("STRING_SKIP", "'\\\\\"' | '\\\\\\\\'"));
            yaccGrammar.DeclareTerminal(new TerminalSymbol("STRING_STD", "'\"' (STRING_SKIP|.)*? '\"'"));
            yaccGrammar.DeclareTerminal(new TerminalSymbol("STRING_ESC", "'@\"' ~[\"]* '\"'"));
            yaccGrammar.DeclareTerminal(new TerminalSymbol("STRING", "STRING_STD|STRING_ESC"));

			yaccGrammar.DeclareTerminal(new TerminalSymbol("DECLARATION_CODE", "'%{' (STRING|COMMENT|.)*? '%}'"));

			yaccGrammar.SetSkipTokens("COMMENT", "STRING", "DECLARATION_CODE");

			/// Нужные штуки
			yaccGrammar.DeclareTerminal(new TerminalSymbol("BORDER", "'%%'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("DECLARATION_NAME", "'%' ID"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("CORNER_LEFT", "'<'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("ID", "[_a-zA-Z][_0-9a-zA-Z]*"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("CORNER_RIGHT", "'>'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("COLON", "':'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("SEMICOLON", "';'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("LITERAL", @"'\''(.|'\\\'')*?'\''"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("LBRACE", "'{'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("RBRACE", "'}'"));
			yaccGrammar.DeclareTerminal(new TerminalSymbol("PIPE", "'|'"));


			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("grammar", new string[][]
			{
				new string[]{ "declarations", "BORDER", "rules", "grammar_ending" }
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("grammar_ending", new string[][]
			{
				new string[]{ "BORDER", "TEXT" },
				new string[]{ }
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("declarations", new string[][]
			{
				new string[]{ "declaration", "declarations" },
				new string[]{ },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("declaration", new string[][]
			{
				new string[]{ "DECLARATION_NAME", "optional_type", "declaration_body" }
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("optional_type", new string[][]
			{
				new string[]{ "CORNER_LEFT", "ID", "CORNER_RIGHT" },
				new string[]{ }
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("declaration_body", new string[][]
			{
				new string[]{ "identifiers" },
				new string[]{ "TEXT" }
			}));


			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("identifiers", new string[][]
			{
				new string[]{ "ID", "identifiers_list" },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("identifiers_list", new string[][]
			{
				new string[]{ "ID", "identifiers_list" },
				new string[]{ }
			}));


			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("rules", new string[][]
			{
				new string[]{ "rule", "rules_list" },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("rules_list", new string[][]
			{
				new string[]{ "rule", "rules_list" },
				new string[]{ },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("rule", new string[][]
			{
				new string[]{ "ID", "COLON", "alternatives", "SEMICOLON" },
			}));


			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("alternatives", new string[][]
			{
				new string[]{ "alternative", "alternatives_list" },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("alternatives_list", new string[][]
			{
				new string[]{ "PIPE", "alternative", "alternatives_list" },
				new string[]{ },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("alternative", new string[][]
			{
				new string[]{ "alternative_component", "alternative" },
				new string[]{ }
			}));


			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("alternative_component", new string[][]
			{
				new string[]{ "ID" },
				new string[]{ "LBRACE", "code_content", "RBRACE" },
				new string[] {"LITERAL" }
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("code_content_element", new string[][]
			{
				new string[]{ "TEXT" },
				new string[]{ "LBRACE", "code_content", "RBRACE" },
			}));

			yaccGrammar.DeclareNonterminal(new NonterminalSymbol("code_content", new string[][]
			{
				new string[]{ "code_content_element", "code_content" },
				new string[] { }
			}));

			yaccGrammar.SetStartSymbol("grammar");

			yaccGrammar.SetListSymbols("alternatives_list", "alternative", "rules_list", "declarations", "identifiers_list", "code_content");

			yaccGrammar.SetGhostSymbols("alternatives_list", "rules_list", "code_content_element");


            var errors = yaccGrammar.CheckValidity();

            if (errors.Count() == 0)
            {
                TableLL1 table = new TableLL1(yaccGrammar);
                table.ExportToCsv("yacc_table.csv");

                var lexerType = BuildLexer(yaccGrammar, "YaccGrammarLexer");

                /// Создаём парсер
                var parser = new Parser(yaccGrammar,
                    new AntlrLexerAdapter(
                        (Antlr4.Runtime.ICharStream stream) => (Antlr4.Runtime.Lexer)Activator.CreateInstance(lexerType, stream)
                    )
                );

                return parser;
            }
            else
            {
                foreach (var error in errors)
                    Console.WriteLine(error);
                return null;
            }
		}

        public static Parser BuildSharp()
        {
            Grammar sharpGrammar = new Grammar();

            /// Пропускаемые сущности
            sharpGrammar.DeclareTerminal(new TerminalSymbol("DIRECTIVE", @"'#' ~[\n\r]*"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("DIRECTIVE_ELSE", @"'#else' (COMMENT|STRING|CHAR|DIRECTIVE|DIRECTIVE_ELSE|NAMESPACE|CLASS_STRUCT_INTERFACE|ENUM|USING|EXTERN|OPERATOR|KEYWORD|ID|SEMICOLON|DOT|DOUBLE_COLON|COMMA|COLON|LCBRACE|RCBRACE|LBRACE|RBRACE|LSBRACE|RSBRACE|LABRACE|RABRACE|EQUALS|ARROW|.)*? '#endif'"));

            sharpGrammar.DeclareTerminal(new TerminalSymbol("COMMENT", "COMMENT_L|COMMENT_ML"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("COMMENT_L", @"'//' ~[\n\r]*"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("COMMENT_ML", "'/*' .*? '*/'"));

            sharpGrammar.DeclareTerminal(new TerminalSymbol("STRING", "STRING_STD|STRING_ESC"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("STRING_SKIP", "'\\\\\"' | '\\\\\\\\'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("STRING_STD", "'\"' (STRING_SKIP|.)*? '\"'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("STRING_ESC", "'@\"' ~[\"]* '\"'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("CHAR", "'\\''('\\\\\\''|'\\\\\\\\'|.)*? '\\''"));

            sharpGrammar.SetSkipTokens("COMMENT", "STRING", "CHAR", "DIRECTIVE", "DIRECTIVE_ELSE");

            /// Нужные штуки
            sharpGrammar.DeclareTerminal(new TerminalSymbol("NAMESPACE", "'namespace'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("CLASS_STRUCT_INTERFACE", "'class' | 'struct' | 'interface'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("ENUM", "'enum'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("USING", "'using'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("EXTERN", "'extern'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("OPERATOR", "'operator'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("DELEGATE", "'delegate'"));

            sharpGrammar.DeclareTerminal(new TerminalSymbol("KEYWORD", "'public'|'private'|'internal'|'protected'|'partial'|'static'|'abstract'|'sealed'|'override'|'virtual'|'readonly'|'new'|'unsafe'|'volatile'"));

            sharpGrammar.DeclareTerminal(new TerminalSymbol("ID", "'@'?[_a-zA-Z][_0-9a-zA-Z]*"));

            sharpGrammar.DeclareTerminal(new TerminalSymbol("SEMICOLON", "';'"));

            sharpGrammar.DeclareTerminal(new TerminalSymbol("DOT", "'.'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("DOUBLE_COLON", "'::'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("COMMA", "'.'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("COLON", "':'"));

            sharpGrammar.DeclareTerminal(new TerminalSymbol("LCBRACE", "'{'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("RCBRACE", "'}'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("LBRACE", "'('"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("RBRACE", "')'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("LABRACE", "'<'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("RABRACE", "'>'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("LSBRACE", "'['"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("RSBRACE", "']'"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("EQUALS", "'='"));
            sharpGrammar.DeclareTerminal(new TerminalSymbol("ARROW", "'=>'"));


            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("namespace_content", new string[][]
            {
                new string[]{ "opening_directives", "namespace_entities" }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("opening_directive", new string[][]
            {
                new string[]{ "USING", "TEXT", "SEMICOLON" },
                new string[]{ "EXTERN", "TEXT", "SEMICOLON" }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("opening_directives", new string[][]
            {
                new string[]{ "opening_directive", "opening_directives" },
                new string[]{}
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("namespace_entities", new string[][]
            {
                new string[]{ "namespace_entity", "namespace_entities" },
                new string[]{ }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("namespace_entity", new string[][]
            {
                new string[]{ "attribute" },
                new string[]{ "namespace" },
                new string[]{ "class_enum" }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("attribute", new string[][]
            {
                 new string[]{ "LSBRACE", "attributes_code", "RSBRACE" },
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("attributes_code", new string[][]
            {
                new string[]{ "TEXT", "attributes_code" },
                new string[]{ "attribute", "attributes_code" },
                new string[]{ },
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("namespace", new string[][]
            {
                new string[]{ "NAMESPACE", "full_name", "LCBRACE", "namespace_content", "RCBRACE" }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("class_enum", new string[][]
            {
                new string[]{ "class_enum_keywords", "class_enum_tail" }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("class_enum_tail", new string[][]
            {
                new string[]{ "CLASS_STRUCT_INTERFACE", "full_name", "before_body", "LCBRACE", "class_entities", "RCBRACE", "opt_semicolon" },
                new string[] { "ENUM", "full_name", "TEXT", "block", "opt_semicolon" },
                new string[] { "DELEGATE", "full_name", "arguments", "SEMICOLON" }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("class_entities", new string[][]
            {
                new string[]{ "class_entity", "class_entities" },
                new string[]{ }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("class_entity", new string[][]
            {
                new string[]{ "attribute" },
                new string[]{ "class_member_keywords", "class_entity_tail" },
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("class_entity_tail", new string[][]
            {
                new string[]{ "full_name", "class_member_tail" },
                new string[]{ "class_enum_tail" }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("class_enum_keywords", new string[][]
           {
                 new string[]{ "KEYWORD", "class_enum_keywords" },
                 new string[]{ }
           }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("class_member_keywords", new string[][]
           {
                 new string[]{ "KEYWORD", "class_member_keywords" },
                 new string[]{ "EXTERN", "class_member_keywords" },
                 new string[]{ }
           }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("opt_semicolon", new string[][]
            {
                new string[]{ "SEMICOLON" },
                new string[]{ }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("class_member_tail", new string[][]
            {
                new string[] { "OPERATOR", "TEXT", "arguments", "before_body", "opt_method_body" },
                new string[] { "arguments", "before_body", "opt_method_body" },
                new string[] { "block", "opt_initializer_code" },
                new string[] { "initializer_code" }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("arguments", new string[][]
            {
                 new string[]{ "LBRACE", "arguments_code", "RBRACE" }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("arguments_code", new string[][]
            {
                new string[]{ "TEXT", "arguments_code" },
                new string[]{ "arguments", "arguments_code" },
                new string[]{ },
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("before_body", new string[][]
           {
                new string[]{ "TEXT", "COLON", "bracket_structure" },
                new string[]{ },
           }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("bracket_structure", new string[][]
            {
                new string[]{ "arguments", "bracket_structure" },
                new string[]{ "TEXT", "bracket_structure" },
                new string[]{ },
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("opt_method_body", new string[][]
            {
                new string[]{ "block" },
                new string[]{ "initializer_code" }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("full_name", new string[][]
            {
                new string[]{ "ID", "full_name_list" },
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("opt_name", new string[][]
            {
                new string[]{ "full_name" },
                new string[]{ }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("full_name_element", new string[][]
            {
                new string[]{ "ID" },
                new string[]{ "DOT" },
                new string[]{ "DOUBLE_COLON" },
                new string[]{ "COMMA" },
                new string[]{ "LABRACE" },
                new string[]{ "RABRACE" },
                new string[]{ "LSBRACE" },
                new string[]{ "RSBRACE" }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("full_name_list", new string[][]
            {
                new string[]{ "full_name_element", "full_name_list" },
                new string[]{ }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("opt_initializer_code", new string[][]
            {
                new string[]{ "initializer_code" },
                new string[]{ },
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("initializer_code", new string[][]
            {
                new string[]{ "EQUALS", "initializer_content", "SEMICOLON" },
                new string[]{ "ARROW", "initializer_content", "SEMICOLON" },
                new string[]{ "SEMICOLON" },
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("initializer_content", new string[][]
            {
                new string[]{ "TEXT", "initializer_content" },
                new string[]{ "block", "initializer_content" },
                new string[]{ }
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("block", new string[][]
            {
                new string[]{ "LCBRACE", "block_content", "RCBRACE" },
            }));

            sharpGrammar.DeclareNonterminal(new NonterminalSymbol("block_content", new string[][]
            {
                new string[]{ "TEXT", "block_content" },
                new string[]{ "block", "block_content" },
                new string[]{ }
            }));

            sharpGrammar.SetStartSymbol("namespace_content");
            /// Символы, которые не должны рекурсивно быть детьми самих себя
            sharpGrammar.SetListSymbols(
                "opening_directives",
                "namespace_entities",
                "class_entities",
                "full_name_list",
                "full_name",
                "block_content",
                "initializer_content"
            );
            /// Символы, которые не должны порождать узел дерева
            sharpGrammar.SetGhostSymbols(
                "namespace_entities",
                "class_entities",
                "namespace_entity",
                "class_entity",
                "opt_initializer_code",
                "full_name_list",
                "full_name_element",
                "class_member_tail",
                "class_enum_tail",
                "opt_name"
            );

            var errors = sharpGrammar.CheckValidity();

            if (errors.Count() == 0)
            {
                TableLL1 table = new TableLL1(sharpGrammar);
                table.ExportToCsv("sharp_table.csv");

                var lexerType = BuildLexer(sharpGrammar, "SharpGrammarLexer");
                /// Создаём парсер
                var parser = new Parser(sharpGrammar,
                    new AntlrLexerAdapter(
                        (Antlr4.Runtime.ICharStream stream) => (Antlr4.Runtime.Lexer)Activator.CreateInstance(lexerType, stream)
                    )
                );

                return parser;
            }
            else
            {
                foreach (var error in errors)
                    Console.WriteLine(error);

                return null;
            }
        }

        public static Parser BuildExpressionGrammar()
		{
			/// Формируем грамматику

			Grammar exprGrammar = new Grammar();

			exprGrammar.DeclareSpecialTokens("ERROR");

			exprGrammar.DeclareTerminal(new TerminalSymbol("PLUS", "'+'"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("MULT", "'*'"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("LPAR", "'('"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("RPAR", "')'"));
			exprGrammar.DeclareTerminal(new TerminalSymbol("ID", "[_a-zA-Z][_0-9a-zA-Z]*"));

			exprGrammar.DeclareNonterminal(new NonterminalSymbol("E", new string[][]
			{
				new string[]{ "T", "E'" }
			}));

			exprGrammar.DeclareNonterminal(new NonterminalSymbol("E'", new string[][]
			{
				new string[]{ "PLUS", "T","E'" },
				new string[]{ }
			}));

			exprGrammar.DeclareNonterminal(new NonterminalSymbol("T", new string[][]
			{
				new string[]{ "F", "T'" },
			}));

			exprGrammar.DeclareNonterminal(new NonterminalSymbol("T'", new string[][]
			{
				new string[]{ "MULT", "F","T'" },
				new string[]{ }
			}));

			exprGrammar.DeclareNonterminal(new NonterminalSymbol("F", new string[][]
			{
				new string[]{ "LPAR", "E","RPAR" },
				new string[]{ "ID" }
			}));

			exprGrammar.SetStartSymbol("E");

            var errors = exprGrammar.CheckValidity();

            if (errors.Count() == 0)
            {
                TableLL1 table = new TableLL1(exprGrammar);
                table.ExportToCsv("expr_table.csv");

                var lexerType = BuildLexer(exprGrammar, "ExprGrammarLexer");

                /// Создаём парсер
                var parser = new Parser(exprGrammar,
                    new AntlrLexerAdapter(
                        (Antlr4.Runtime.ICharStream stream) => (Antlr4.Runtime.Lexer)Activator.CreateInstance(lexerType, stream)
                    )
                );

                return parser;
            }
            else
            {
                foreach (var error in errors)
                    Console.WriteLine(error);
                return null;
            }
        }

        public static void BuildTestCases()
        {
            Grammar testGrammar;

            /// 1
            
            testGrammar = new Grammar();

            testGrammar.DeclareTerminal(new TerminalSymbol("A", "'a'"));
            testGrammar.DeclareTerminal(new TerminalSymbol("B", "'b'"));

            testGrammar.DeclareNonterminal(new NonterminalSymbol("a", new string[][]
            {
                new string[]{ "A", "TEXT", "B" }
            }));

            testGrammar.SetStartSymbol("a");

            var errors = testGrammar.CheckValidity();

            if (errors.Count() == 0)
            {
                TableLL1 table = new TableLL1(testGrammar);
                table.ExportToCsv("1.csv");
            }

            /// 2

            testGrammar = new Grammar();

            testGrammar.DeclareTerminal(new TerminalSymbol("A", "'a'"));
            testGrammar.DeclareTerminal(new TerminalSymbol("B", "'b'"));

            testGrammar.DeclareNonterminal(new NonterminalSymbol("a", new string[][]
            {
                new string[]{ "A", "TEXT" }
            }));

            testGrammar.SetStartSymbol("a");

            errors = testGrammar.CheckValidity();

            if (errors.Count() == 0)
            {
                TableLL1 table = new TableLL1(testGrammar);
                table.ExportToCsv("2.csv");
            }

            /// 3

            testGrammar = new Grammar();

            testGrammar.DeclareTerminal(new TerminalSymbol("A", "'a'"));
            testGrammar.DeclareTerminal(new TerminalSymbol("B", "'b'"));

            testGrammar.DeclareNonterminal(new NonterminalSymbol("a", new string[][]
            {
                new string[]{ "TEXT", "B" }
            }));

            testGrammar.SetStartSymbol("a");

            errors = testGrammar.CheckValidity();

            if (errors.Count() == 0)
            {
                TableLL1 table = new TableLL1(testGrammar);
                table.ExportToCsv("3.csv");
            }

            /// 4

            testGrammar = new Grammar();

            testGrammar.DeclareTerminal(new TerminalSymbol("A", "'a'"));
            testGrammar.DeclareTerminal(new TerminalSymbol("B", "'b'"));
            testGrammar.DeclareTerminal(new TerminalSymbol("C", "'c'"));

            testGrammar.DeclareNonterminal(new NonterminalSymbol("a", new string[][]
            {
                new string[]{ "A", "TEXT", "b" }
            }));

            testGrammar.DeclareNonterminal(new NonterminalSymbol("b", new string[][]
            {
                 new string[]{ "B", "C" }
            }));

            testGrammar.SetStartSymbol("a");

            errors = testGrammar.CheckValidity();

            if (errors.Count() == 0)
            {
                TableLL1 table = new TableLL1(testGrammar);
                table.ExportToCsv("4.csv");
            }

            /// 5

            testGrammar = new Grammar();

            testGrammar.DeclareTerminal(new TerminalSymbol("A", "'a'"));
            testGrammar.DeclareTerminal(new TerminalSymbol("B", "'b'"));
            testGrammar.DeclareTerminal(new TerminalSymbol("C", "'c'"));

            testGrammar.DeclareNonterminal(new NonterminalSymbol("a", new string[][]
            {
                new string[]{ "A", "TEXT", "b" }
            }));

            testGrammar.DeclareNonterminal(new NonterminalSymbol("b", new string[][]
            {
                 new string[]{ "B", "C" },
                 new string[]{ },
            }));

            testGrammar.SetStartSymbol("a");

            errors = testGrammar.CheckValidity();

            if (errors.Count() == 0)
            {
                TableLL1 table = new TableLL1(testGrammar);
                table.ExportToCsv("5.csv");
            }

            /// 5

            testGrammar = new Grammar();

            testGrammar.DeclareTerminal(new TerminalSymbol("A", "'a'"));
            testGrammar.DeclareTerminal(new TerminalSymbol("B", "'b'"));
            testGrammar.DeclareTerminal(new TerminalSymbol("C", "'c'"));

            testGrammar.DeclareNonterminal(new NonterminalSymbol("a", new string[][]
            {
                new string[]{ "b", "TEXT", "C" }
            }));

            testGrammar.DeclareNonterminal(new NonterminalSymbol("b", new string[][]
            {
                 new string[]{ "A", "B" },
                 new string[]{ },
            }));

            testGrammar.SetStartSymbol("a");

            errors = testGrammar.CheckValidity();

            if (errors.Count() == 0)
            {
                TableLL1 table = new TableLL1(testGrammar);
                table.ExportToCsv("6.csv");
            }
        }

        public static Parser BuildTestCase()
		{
			/// Формируем грамматику

			Grammar testGrammar = new Grammar();

			testGrammar.DeclareTerminal(new TerminalSymbol("A", "'a'"));
			testGrammar.DeclareTerminal(new TerminalSymbol("B", "'b'"));
			testGrammar.DeclareTerminal(new TerminalSymbol("C", "'c'"));
			testGrammar.DeclareTerminal(new TerminalSymbol("D", "'d'"));
			testGrammar.DeclareTerminal(new TerminalSymbol("E", "'e'"));

			testGrammar.DeclareNonterminal(new NonterminalSymbol("b", new string[][]
			{
				new string[]{ "a", "TEXT", "B" },
				new string[]{ "A" },
				new string[]{ "C" }
			}));

			testGrammar.DeclareNonterminal(new NonterminalSymbol("a", new string[][]
			{
				new string[]{ "D", "E" },
				new string[] { }
			}));

			testGrammar.SetStartSymbol("b");

            var errors = testGrammar.CheckValidity();

            if (errors.Count() == 0)
            {
                TableLL1 table = new TableLL1(testGrammar);
                table.ExportToCsv("test_table.csv");

                var lexerType = BuildLexer(testGrammar, "TestGrammarLexer");

                /// Создаём парсер
                var parser = new Parser(testGrammar,
                    new AntlrLexerAdapter(
                        (Antlr4.Runtime.ICharStream stream) => (Antlr4.Runtime.Lexer)Activator.CreateInstance(lexerType, stream)
                    )
                );

                return parser;
            }
            else
            {
                foreach (var error in errors)
                    Console.WriteLine(error);
                return null;
            }
        }
	}
}
