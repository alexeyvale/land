using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace LandParserGenerator.Builder
{
	public struct Error
	{
		public string Info;
	}

	/// <summary>
	/// Информация для поиска составляющей имени узла
	/// </summary>
	public class NameComponentLocation
	{
		public int? Position { get; set; }
		public string UserDefinedName { get; set; }
	}

    public class YaccLexBuilder
    {
        public List<Error> Errors { get; set; } = new List<Error>();
		public Dictionary<string, string> UserDefinedNamesTable { get; set; } = new Dictionary<string, string>();
		public string Namespace { get; set; } = "Generated";


		public void AddUserDefinedName(string name, string symbol)
		{
			if (UserDefinedNamesTable.ContainsKey(name))
				Errors.Add(new Error()
				{
					Info = $"Имя {name} связано с несколькими символами грамматики"
				});
			UserDefinedNamesTable[name] = symbol;
		}

        //Перечень определённых пользователем терминалов
		public Dictionary<string, string> Terminals { get; set; } = new Dictionary<string, string>();
		/// <summary>
		/// Терминалы, которые определяются не одной регуляркой, а набором, и подбираются в отдельном состоянии лексера
		/// </summary>
		public Dictionary<string, List<string>> StateProvidedTerminals { get; set; } = new Dictionary<string, List<string>>();
		/// <summary>
		/// Терминальные символы, которые нужно пропускать при разборе, если они не указаны в правиле
		/// </summary>
		public List<string> SkipTerminals { get; set; } = new List<string>(); 
		private const string AutoTerminalPrefix = "_TERMINAL";
        private int AutoTerminalCounter = 0;

        

        public string StartSymbol { get; set; } = null;
        public Dictionary<string, RuleNode> Nonterminals { get; set; } = new Dictionary<string, RuleNode>();
		/// <summary>
		/// Пользовательские нетерминалы, для которых не надо создавать узлы дерева
		/// </summary>
		public List<string> NodeListNonterminals { get; set; } = new List<string>();
		/// <summary>
		/// Пользовательские нетерминалы, у которых не должно быть потомков
		/// </summary>
		public List<string> LeafNonterminals { get; set; } = new List<string>();

		private const string AutoNonterminalPrefix = "_NONTERMINAL";
        private int AutoNonterminalCounter = 0;

        public bool AddUserNonterminal(string name, List<string> elements, 
			List<NameComponentLocation> nodeNameComponents)
        {
			//Если это первое пользовательское правило, это стартовый символ
            if (String.IsNullOrEmpty(StartSymbol))
                StartSymbol = name;

			//Если символ с таким именем уже определён - ошибка
            if (Nonterminals.ContainsKey(name))
            {
                Errors.Add(new Error() { Info = $"Повторное определение символа {name}" });
                return false;
            }

			//Создаём правило
			var newRule = new RuleNode(new AlternativeNode(elements.Select(e => new EntityNode(e)).ToArray()));
			if (nodeNameComponents != null)
				newRule.NameComponents.AddRange(nodeNameComponents);
			Nonterminals.Add(name, newRule);

            return true;
        }

        //Формируем правило для списка элементов (если указан элемент и при нём - квантификатор)
        public string AddAutoNonterminal(string name, Quantifier quantifier)
        {
            var newName = AutoNonterminalPrefix + AutoNonterminalCounter++;
            var singleElem = new EntityNode(name);
            var pluralElem = new EntityNode(newName);
            RuleNode rule = null;
            switch (quantifier)
            {
                case Quantifier.ONE_OR_MORE:
                    var alternativeOne = new AlternativeNode(singleElem);
                    var alternativeMany = new AlternativeNode(pluralElem, singleElem);
                    rule = new RuleNode(alternativeOne, alternativeMany);
                    break;
                case Quantifier.ZERO_OR_MORE:
                    var alternativeZero = new AlternativeNode();
                    alternativeMany = new AlternativeNode(pluralElem, singleElem);
                    rule = new RuleNode(alternativeZero, alternativeMany);
                    break;
                case Quantifier.ZERO_OR_ONE:
                    alternativeZero = new AlternativeNode();
                    alternativeOne = new AlternativeNode(singleElem);
                    rule = new RuleNode(alternativeZero, alternativeOne);
                    break;
            }

			return AddOrUseExistingAuto(rule, newName);
		}

        //Добавление нетерминала для группы
        public string AddAutoNonterminal(List<string> elements)
        {
            if (elements.Count == 1)
                return elements.First();
            var newName = AutoNonterminalPrefix + AutoNonterminalCounter++;
            var alternative = new AlternativeNode();
            foreach (string elem in elements)
                alternative.Children.Add(new EntityNode(elem));
			var rule = new RuleNode(alternative);

			return AddOrUseExistingAuto(rule, newName);
		}

        //Добавление нетерминал для ветвления
        public string AddAutoNonterminal(string alt1, string alt2)
        {
            var newName = AutoNonterminalPrefix + AutoNonterminalCounter++;
            var alt1Node = new AlternativeNode(new EntityNode(alt1));
            var alt2Node = new AlternativeNode(new EntityNode(alt2));
			var newRule = new RuleNode(alt1Node, alt2Node);

			return AddOrUseExistingAuto(newRule, newName);
        }

		/// <summary>
		/// Добавление нового автоматического правила в грамматику
		/// </summary>
		/// <param name="newRule">Новое правило</param>
		/// <param name="newName">Определяемый новым правилом нетерминал</param>
		/// <returns>Имя определяемого нетерминала или имя существующего, 
		/// правило для которого совпало с новым</returns>
		private string AddOrUseExistingAuto(RuleNode newRule, string newName)
		{
			//Если новый нетерминал выражается через существующий и только через него
			if (newRule.Children.Count == 1 &&
				newRule.Children[0].Children.Count == 1 &&
				Nonterminals.ContainsKey(newRule.Children[0].Children[0].Value))
				return newRule.Children[0].Children[0].Value;

			//Если уже есть точно такое же правило, возвращаем соответствующий ему символ
			string equalRule = GetEqualRule(newRule, newName);
			if (!String.IsNullOrEmpty(equalRule))
				return equalRule;

			Nonterminals.Add(newName, newRule);
			return newName;
		}

		/// <summary>
		/// Проверка наличия правила, совпадающего с переданным
		/// </summary>
		/// <param name="newRule">Проверяемое правило</param>
		/// <param name="newName">Имя символа, которому это правило соответствует</param>
		/// <returns>Имя символа с таким же правилом</returns>
		private string GetEqualRule(RuleNode newRule, string newName)
		{
			//Если встречаем существующее правило, дублирующее наше новое
			foreach (var nonterminal in Nonterminals)
				if (newRule.Equals(nonterminal.Value, nonterminal.Key, newName))
					//Возвращаем соответствующее имя нетерминала
					return nonterminal.Key;
			return null;
		}

		/// <summary>
		/// Замена символа во всех правилах
		/// </summary>
		/// <param name="from">Заменяемый символ</param>
		/// <param name="to">Символ, на который заменяем</param>
		private void ChangeSymbol(string from, string to)
		{
			foreach (var rule in Nonterminals.Values)
				foreach (var alt in rule.Children)
					foreach (var elem in alt.Children)
						if (elem.Value == from)
							elem.Value = to;

			if(Nonterminals.ContainsKey(from))
			{
				var body = Nonterminals[from];
				Nonterminals.Remove(from);
				Nonterminals.Add(to, body);
			}
			else if(Terminals.ContainsKey(from))
			{
				var body = Terminals[from];
				Terminals.Remove(from);
				Terminals.Add(to, body);
			}
			
		}

		public void RemoveIdenticalRules()
		{
			bool found;
			//Пока находим идентичные правила, продолжаем итеративный процесс
			do
			{
				found = false;
				//Изначально в списке некоторый текущий нетерминал
				List<string> equalNonterminals = null;
				foreach (var nt1 in Nonterminals)
				{
					//Ищем нетерминалы, правила для которых идентичны текущему
					equalNonterminals = new List<string>() { nt1.Key };
					foreach (var nt2 in Nonterminals)
						if (nt1.Key != nt2.Key &&
							nt1.Value.Equals(nt2.Value, nt2.Key, nt1.Key))
							equalNonterminals.Add(nt2.Key);
					if (equalNonterminals.Count > 1)
					{
						found = true;
						break;
					}
				}
				//Если нашли идентичные правила
				if (found)
				{
					//При совпадении пользовательского правила с каким-то автоматическим, оставляем пользовательское
					string symbolThatWillSurvive = equalNonterminals.Where(nt => !nt.StartsWith(AutoNonterminalPrefix)).FirstOrDefault();
					//Иначе берём любое автоматическое
					if (String.IsNullOrEmpty(symbolThatWillSurvive))
						symbolThatWillSurvive = equalNonterminals[0];

					foreach (var symbol in equalNonterminals)
						if (symbol != symbolThatWillSurvive)
							ChangeSymbol(symbol, symbolThatWillSurvive);
				}
			}
			while (found);
		}

		/// <summary>
		/// Поиск альтернатив, которые входят в другие альтернативы, 
		/// и оформление их как самостоятельных нетерминалов
		/// </summary>
		public void ExtractAlternativesFromBiggerOnes()
		{
			//Проходим по всем правилам
			for (var r1 = 0; r1 < Nonterminals.Keys.Count; ++r1)
			{
				var key1 = Nonterminals.Keys.ElementAt(r1);
				var rule1 = Nonterminals[key1];

				for (var a1 = 0; a1 < rule1.Children.Count; ++a1)
				{
					var alternative1 = rule1.Children[a1];

					for (var r2 = 0; r2 < Nonterminals.Keys.Count; ++r2)
					{
						var key2 = Nonterminals.Keys.ElementAt(r2);
						var rule2 = Nonterminals[key2];

						for (var a2 = 0; a2 < rule2.Children.Count; ++a2)
						{
							var alternative2 = rule2.Children[a2];

							if (alternative1 != alternative2)
							{
								var nestingIndices = alternative1.IsNested(alternative2);
								for (var i = nestingIndices.Count() - 1; i >= 0; --i)
								{
									if (rule1.Children.Count == 1)
									{
										alternative2.Children.RemoveRange(nestingIndices[i], alternative1.Children.Count);
										alternative2.Children.Insert(nestingIndices[i], new EntityNode(key1));
									}
									else
									{
										var newRule = new RuleNode(alternative1);
										var newName = AutoNonterminalPrefix + AutoNonterminalCounter++;
										Nonterminals.Add(newName, newRule);

										rule1.Children.Remove(alternative1);
										rule1.Children.Insert(a1, new AlternativeNode(new EntityNode(newName)));

										alternative2.Children.RemoveRange(nestingIndices[i], alternative1.Children.Count);
										alternative2.Children.Insert(nestingIndices[i], new EntityNode(newName));
									}
								}
							}
						}
					}
				}
			}
		}

        //Создание yacc и lex-файлов
        public void WriteGrammar(string lexFile, string yaccFile, string sharpFile)
        {
			//Множество использованных терминальных символов (их должен распознавать лексер)
			var terminalsUsed = new HashSet<string>(SkipTerminals);

			///////////////////////////////////////////////////////////////// Вывод yacc-файла
			StreamWriter yaccOut = new StreamWriter(yaccFile, false);

            var yaccPattern =
@"%{{
	public Parser(AbstractScanner<ValueType, LexLocation> scanner) : base(scanner) {{ }}

	public Node Root {{ get; set; }}

	public List<string> Log {{ get; set; }}

	protected override List<string> SkipTokens {{ get; set; }} = new List<string>(){{{5}}};
%}}

%union {{ 
	public string strVal;
	public Node nodeVal;
	public NodeList nodeList;
	public TokenInfo tokenVal;
}}

%using Land.Generated;
%namespace {4}
%output = GeneratedParser.cs

%token<tokenVal> TEXT {0}
%type<nodeVal> {1}
%type<nodeList> {2}

%start {3}

%%

";
            yaccOut.WriteLine(String.Format(yaccPattern,
                String.Join(" ", this.Terminals.Keys),
				String.Join(" ", this.Nonterminals.Keys.Where(nt=>!nt.StartsWith(AutoNonterminalPrefix))),
				String.Join(" ", this.Nonterminals.Keys.Where(nt => nt.StartsWith(AutoNonterminalPrefix))),
				this.StartSymbol, this.Namespace,
				"\"" + String.Join("\",\"", this.SkipTerminals) + "\""));

			foreach (var rule in this.Nonterminals)
			{
				//Выводим имя нетерминала
				yaccOut.WriteLine(rule.Key);
				//Является ли выводимая альтернатива первой
				bool firstAlternative = true;

				foreach (var alternative in rule.Value.Children)
				{
					if (firstAlternative)
					{
						yaccOut.Write("\t:");
						firstAlternative = false;
					}
					else
						yaccOut.Write("\t|");

					foreach (var entity in alternative.Children)
						yaccOut.Write($" {entity.Value}");

					//Является ли нетерминал автоматически введённым или вспомогательным
					var isAutoOrAuxiliary = rule.Key.StartsWith(AutoNonterminalPrefix) ||
						NodeListNonterminals.Contains(rule.Key);
					//Если да - рассматриваем его как способ скрыть большой список элементов
					var className = isAutoOrAuxiliary ? "NodeList" : Char.ToUpper(rule.Key[0]) + rule.Key.Substring(1);

					yaccOut.WriteLine(" {");

					yaccOut.WriteLine($"\t\t\t$$ = new {className}();");

					if (rule.Key == StartSymbol)
						yaccOut.WriteLine($"\t\t\tRoot = $$;");

					//Проходим по всем дочерним элементам
					for (int i = 0; i < alternative.Children.Count; ++i)
					{
						var child = alternative.Children[i];
						//Если у нас нетерминал и у текущего узла могут быть дети
						if (Nonterminals.ContainsKey(child.Value) && !LeafNonterminals.Contains(rule.Key))
						{
							//и он автоматически введённый или вспомогательный пользовательский
							if (child.Value.StartsWith(AutoNonterminalPrefix) ||
								NodeListNonterminals.Contains(child.Value))
							{
								yaccOut.WriteLine($"\t\t\tforeach(var item in ${i + 1}.Items)");
								yaccOut.WriteLine($"\t\t\t\titem.Parent = $$;");
								yaccOut.WriteLine($"\t\t\t$$.Items.AddRange(${i + 1}.Items);");
							}
							else
							{
								yaccOut.WriteLine($"\t\t\t\t${i + 1}.Parent = $$;");
								yaccOut.WriteLine($"\t\t\t$$.Items.Add(${i + 1});");
							}
						}
						else if (Terminals.ContainsKey(child.Value))
						{
							terminalsUsed.Add(child.Value);
						}
						//else if (child.Value == "TEXT")
						//{
						//	yaccOut.WriteLine($"\t\t\t\tvar text = new TokenNode();");
						//	yaccOut.WriteLine($"\t\t\t$$.Items.Add(${i + 1});");
						//}
						else
						{
							Errors.Add(new Error()
							{
								Info = String.Format("В правиле для нетерминала {0} используется не определённый в грамматике символ {1}",
									rule.Key, child.Value)
							});
						}

						yaccOut.WriteLine($"\t\t\t$$.MergeLocation(${i + 1}.Location);");
					}

					//Для автонетерминала именем считаем имена всех составляющих
					if (rule.Key.StartsWith(AutoNonterminalPrefix))
					{
						for (int i = 0; i < alternative.Children.Count; ++i)
						{
							var child = alternative.Children[i];
							if (Nonterminals.ContainsKey(child.Value))
							{
								yaccOut.WriteLine($"\t\t\t$$.Name.AddRange(${i + 1}.Name);");
							}
							else
							{
								yaccOut.WriteLine($"\t\t\t$$.Name.Add(${i + 1}.Text);");
							}
						}
					}
					else
					{
						var nameLoc = rule.Value.NameComponents;
						for (var j = 0; j < nameLoc.Count; ++j)
						{
							if (!nameLoc[j].Position.HasValue)
							{
								var key = UserDefinedNamesTable[nameLoc[j].UserDefinedName];
								//По введённому пользователем имени для элемента правила определяем индекс этого элемента
								nameLoc[j].Position = alternative.Children.Select((ch, idx) => new { value = ch.Value, index = idx })
									.First(e => e.value == key).index + 1;
							}

							if (Terminals.ContainsKey(alternative.Children[nameLoc[j].Position.Value - 1].Value))
								yaccOut.WriteLine($"\t\t\t$$.Name.Add(${nameLoc[j].Position}.Text);");
							else
								yaccOut.WriteLine($"\t\t\t$$.Name.AddRange(${nameLoc[j].Position}.Name);");
						}
					}

					yaccOut.WriteLine("\t\t}");

				}

				yaccOut.WriteLine("\t;" + Environment.NewLine);
			}

			yaccOut.Close();

			///////////////////////////////////////////////////////////////// Вывод lex-файла
			StreamWriter lexOut = new StreamWriter(lexFile, false);

			var lexPattern =
@"%namespace {0}
%using QUT.Gppg;
%using Land.Generated;
%option stack

	public List<string> Log {{ get; set; }}

	private string AccumulatedText {{ get; set; }}
	private LexLocation AccumulatedStartLocation {{ get; set; }}

{1}

{2}

%%

/*
%{{
  yylloc = new LexLocation(tokLin, tokCol, tokELin, tokECol);
%}}
*/

";
			lexOut.WriteLine(String.Format(lexPattern, Namespace,
				String.Join(Environment.NewLine, this.StateProvidedTerminals
					.Select(t => "%x " + t.Key + "_state")),
				String.Join(Environment.NewLine, this.Terminals
					.Select(terminal => $"{terminal.Key} {terminal.Value}"))));

			//Создаём правила только для используемых в грамматике или пропускаемых терминалов
			//Генерируем правила для автотерминалов
			foreach (var terminal in terminalsUsed.Where(t => t.StartsWith(AutoTerminalPrefix)))
			{
				lexOut.WriteLine(
$@"
{{{terminal}}} {{
	Log.Add(yytext);

	yylval.tokenVal = new TokenInfo()
	{{
		Text = yytext,
		Location = new LexLocation(tokLin, tokCol, tokELin,tokECol)
	}}; 
	return (int)Tokens.{terminal};
}}");
			}

			lexOut.WriteLine();

			//Генерируем правила для пользовательских терминалов без доп. состояния
			foreach (var terminal in terminalsUsed.Where(
				t => !t.StartsWith(AutoTerminalPrefix) && Terminals.ContainsKey(t)))
			{
				lexOut.WriteLine(
$@"
{{{terminal}}} {{
	Log.Add(yytext);

	yylval.tokenVal = new TokenInfo()
	{{
		Text = yytext,
		Location = new LexLocation(tokLin, tokCol, tokELin,tokECol)
	}}; 
	return (int)Tokens.{terminal};
}}");
			}

			//Генерируем правила для пользовательских терминалов с доп. состоянием
			foreach (var terminal in terminalsUsed.Where(
				t => !t.StartsWith(AutoTerminalPrefix) && StateProvidedTerminals.ContainsKey(t)))
			{
				//Правило для левой границы, на которой уходим в новое состояние
				lexOut.WriteLine(
$@"
{StateProvidedTerminals[terminal][0]} {{
	AccumulatedText = yytext;
	AccumulatedStartLocation = new LexLocation(tokLin, tokCol, tokELin,tokECol);
	yy_push_state({terminal + "_state"});
}}

<{terminal + "_state"}> {{");

				for (var i = 1; i < StateProvidedTerminals[terminal].Count - 1; ++i)
				{
					if(StateProvidedTerminals[terminal][i] != "{" + terminal + "}")
						lexOut.WriteLine(
$@"
	{StateProvidedTerminals[terminal][i]} {{
		AccumulatedText += yytext;
	}}");
				}

				//Правило для левой границы, на которой кладём на стек такое же состояние
				//обеспечивая поддержку вложенности
				lexOut.WriteLine(
$@"
	{StateProvidedTerminals[terminal][0]} {{
		AccumulatedText += yytext;
		yy_push_state({terminal + "_state"});
	}}");


				//Правило для правой границы, на которой возвращаем всё подобранное и переходим в исходное состояние
				lexOut.WriteLine(
$@"
	{StateProvidedTerminals[terminal].Last()} {{
		AccumulatedText += yytext;
		yy_pop_state();

		if(currentScOrd == 0)
		{{
			yylval.tokenVal = new TokenInfo()
			{{
				Text = AccumulatedText,
				Location = AccumulatedStartLocation.Merge(new LexLocation(tokLin, tokCol, tokELin,tokECol));
			}};
			return (int)Tokens.{terminal};
		}}
	}}");
				//Правило для подбора и добавления к тексту всех прочих символов
				lexOut.WriteLine(
$@"
	.|\n {{
		AccumulatedText += yytext;
	}}
}}");
			}

			lexOut.Close();

			///////////////////////////////////////////////////////////////// Вывод cs-файла с классами для узлов дерева
			StreamWriter csOut = new StreamWriter(sharpFile, false);

            csOut.WriteLine(
$@"using Land.Generated;

namespace {this.Namespace}
{{");

            //Для каждого описанного в land-файле правила, не помеченного как itemslist, создаём узел дерева
            foreach (var rule in this.Nonterminals
				.Where(nt=>!nt.Key.StartsWith(AutoNonterminalPrefix) && !this.NodeListNonterminals.Contains(nt.Key)))
                csOut.WriteLine(String.Format(@"	public class {0}: Node {{}}", 
					Char.ToUpper(rule.Key[0]) + rule.Key.Substring(1)));

            //Закрыли пространство имён
            csOut.WriteLine(Environment.NewLine + "}");

            csOut.Close();
        }

    }
}
