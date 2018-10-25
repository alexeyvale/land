%{
    public Parser(AbstractScanner<Land.Core.Builder.ValueType, SegmentLocation> scanner) : base(scanner) { }
    
    public Grammar ConstructedGrammar;
    public List<Message> Log = new List<Message>();
    
    private HashSet<string> Aliases = new HashSet<string>();
%}

%using System.Linq;
%using Land.Core;

%output = LandParser.cs

%YYLTYPE Land.Core.SegmentLocation

%namespace Land.Core.Builder

%union { 
	public int intVal; 
	public double doubleVal;
	public Quantifier quantVal;
	public bool boolVal;
	public string strVal;
	public Entry entryVal;
	public Alternative altVal;
	public ArgumentGroup argGroupVal;
	public dynamic dynamicVal;
	public OptionDeclaration optDeclVal;
	
	public List<dynamic> dynamicList;
	public List<Tuple<string, List<dynamic>>> optionParamsList;
	public List<OptionDeclaration> optionsList;
	public List<string> strList;	
	public List<Alternative> altList;
	
	public HashSet<string> strSet;
	
	// Информация о количестве повторений
	public Nullable<Quantifier> optQuantVal;
	public Nullable<double> optDoubleVal;
}

%start lp_description

%left OR

%token OPT_LROUND_BRACKET ELEM_LROUND_BRACKET LROUND_BRACKET RROUND_BRACKET LCURVE_BRACKET RCURVE_BRACKET
%token COLON COMMA PROC EQUALS MINUS PLUS EXCLAMATION DOT ARROW LEFT RIGHT LINESTART
%token <strVal> REGEX NAMED STRING ID ENTITY_NAME OPTION_NAME CATEGORY_NAME
%token <intVal> POSITION
%token <doubleVal> RNUM
%token <quantVal> OPTIONAL ZERO_OR_MORE ONE_OR_MORE
%token IS_LIST_NODE PREC_NONEMPTY

%type <optQuantVal> quantifier
%type <strVal> entry_core group optional_alias
%type <entryVal> entry
%type <strList> identifiers
%type <altList> body
%type <boolVal> prec_nonempty opt_linestart
%type <argGroupVal> argument_group
%type <dynamicVal> argument
%type <altVal> alternative
%type <optDeclVal> option

%type <dynamicList> opt_args args context_opt_args entry_args
%type <optionParamsList> context_options
%type <optionsList> option_or_block options

%type <strSet> pair_border_group_content pair_border

%%

lp_description 
	: structure PROC options_section 
		{ 
			ConstructedGrammar.PostProcessing();
			Log.AddRange(ConstructedGrammar.CheckValidity()); 
		}
	;

/***************************** STRUCTURE ******************************/
	
structure 
	: structure element
	| element
	;

element
	: terminal
	| nonterminal
	| pair
	;
	
terminal
	: ENTITY_NAME COLON opt_linestart REGEX 
		{ 
			SafeGrammarAction(() => { 
				ConstructedGrammar.DeclareTerminal($1, $4, $3);
				ConstructedGrammar.AddAnchor($1, @1.Start);
			}, @1.Start);
		}
	;
	
opt_linestart
	: LINESTART { $$ = true; }
	| { $$ = false; }
	;
	
/******** ID = %left ID1 %right (ID2 | ID3) ***************/
pair
	: ENTITY_NAME COLON LEFT pair_border RIGHT pair_border 
		{
			SafeGrammarAction(() => { 
				ConstructedGrammar.DeclarePair($1, $4, $6);
				ConstructedGrammar.AddAnchor($1, @1.Start);
			}, @1.Start);
		}
	;
	
pair_border
	: ID { $$ = new HashSet<string>() { $1 }; }
	| STRING 
		{ 	
			var generated = ConstructedGrammar.GenerateTerminal($1);
			ConstructedGrammar.AddAnchor(generated, @1.Start);
			$$ = new HashSet<string>() { generated };
		}
	| LROUND_BRACKET pair_border_group_content RROUND_BRACKET { $$ = $2; }
	;
	
pair_border_group_content
	: pair_border { $$ = $1; }
	| pair_border_group_content OR pair_border { $1.UnionWith($3); $$ = $1; }
	;	

/******* ID = ID 'string' (group)[*|+|?]  ********/
nonterminal
	: ENTITY_NAME EQUALS body
		{ 
			var aliases = this.Aliases;
			this.Aliases = new HashSet<string>(); 
			
			SafeGrammarAction(() => { 
				ConstructedGrammar.DeclareNonterminal($1, $3);
				ConstructedGrammar.AddAnchor($1, @1.Start);
				
				if(aliases.Count > 0)
					ConstructedGrammar.AddAliases($1, aliases);
			}, @1.Start);
		}
	;
	
body
	: body OR alternative optional_alias 
		{ 
			$$ = $1; 
			$3.Alias = $4; 
			$$.Add($3); 
		}
	| alternative optional_alias 
		{ 
			$$ = new List<Alternative>(); 
			$1.Alias = $2; 
			$$.Add($1); 
		}
	;

alternative
	: alternative entry { $$ = $1; $$.Add($2); }
	| { $$ = new Alternative(); }
	;

optional_alias
	: ARROW ID { $$ = $2; this.Aliases.Add($2); }
	| { $$ = null; }
	;
	
entry
	: context_options entry_core entry_args quantifier prec_nonempty
		{ 		
			var opts = new LocalOptions();
			
			foreach(var opt in $1)
			{
				NodeOption nodeOpt;		
				if(!Enum.TryParse<NodeOption>(opt.Item1.ToUpper(), out nodeOpt))
				{
					MappingOption mapOpt;
					if(!Enum.TryParse<MappingOption>(opt.Item1.ToUpper(), out mapOpt))
					{
						Log.Add(Message.Error(
							"Неизвестная опция '" + opt.Item1 + "'",
							@1.Start,
							"LanD"
						));
					}
					else
						opts.Set(mapOpt, opt.Item2.ToArray());				
				}
				else
					opts.Set(nodeOpt);	
			}
			
			if($4.HasValue)
			{
				if($2.StartsWith(Grammar.ANY_TOKEN_NAME))
				{
					Log.Add(Message.Warning(
							"Использование квантификаторов с символом '" + Grammar.ANY_TOKEN_NAME + "' избыточно и не влияет на процесс разбора",
							@1.Start,
							"LanD"
						));
				}
				else
				{			
					var generated = ConstructedGrammar.GenerateNonterminal($2, $4.Value, $5);
					ConstructedGrammar.AddAnchor(generated, @$.Start);
					
					$$ = new Entry(generated, opts);
				}
			}
			
			if($$ == null)
			{
				if($2.StartsWith(Grammar.ANY_TOKEN_NAME))
				{
					AnyOption sugarOption;

					if(Enum.TryParse($2.Substring(Grammar.ANY_TOKEN_NAME.Length), out sugarOption))
						opts.AnyOptions[sugarOption] = new HashSet<string>($3.Select(e=>(string)e)); 
					else
					{
						foreach(var opt in $3)
						{
							var errorGroupName = String.Empty;
							
							if(opt is ArgumentGroup)
							{
								var group = (ArgumentGroup)opt;

								if(Enum.TryParse(group.Name, out sugarOption))
									opts.AnyOptions[sugarOption] = new HashSet<string>(group.Arguments.Select(e=>(string)e)); 
								else
									errorGroupName = group.Name;
							}
							else if(opt is String)
							{
								if(Enum.TryParse((string)opt, out sugarOption))
									opts.AnyOptions[sugarOption] = new HashSet<string>(); 
								else
									errorGroupName = (string)opt;
							}
							
							if(!String.IsNullOrEmpty(errorGroupName))
							{
								Log.Add(Message.Error(
									"При описании '" + Grammar.ANY_TOKEN_NAME + "' использовано неизвестное имя группы '" 
										+ errorGroupName + "', группа проигнорирована",
									@1.Start,
									"LanD"
								));
							}
						}
					}				
					
					$$ = new Entry(Grammar.ANY_TOKEN_NAME, opts);
				}
				else
				{
					$$ = new Entry($2, opts);
				}
			}
		}
	;
	
entry_args
	: ELEM_LROUND_BRACKET args RROUND_BRACKET { $$ = $2; }
	| { $$ = new List<dynamic>(); }
	;
	
context_options
	: context_options OPTION_NAME context_opt_args
		{ 
			$$ = $1; 
			$$.Add(new Tuple<string, List<dynamic>>($2, $3)); 
		}
	| { $$ = new List<Tuple<string, List<dynamic>>>(); }
	;
	
context_opt_args
	: OPT_LROUND_BRACKET args RROUND_BRACKET { $$ = $2; }
	| { $$ = new List<dynamic>(); }
	;
	
prec_nonempty
	: PREC_NONEMPTY { $$ = true; }
	| { $$ = false; }
	;
	
quantifier
	: OPTIONAL { $$ = $1; }
	| ZERO_OR_MORE { $$ = $1; }
	| ONE_OR_MORE { $$ = $1; }
	| { $$ = null; }
	;
	
entry_core
	: STRING
		{ 
			$$ = ConstructedGrammar.GenerateTerminal($1);
			ConstructedGrammar.AddAnchor($$, @$.Start);
		}
	| ID 
		{ $$ = $1; }
	| group 
		{ $$ = $1; }
	;
	
group
	: LROUND_BRACKET body RROUND_BRACKET
		{ 
			$$ = ConstructedGrammar.GenerateNonterminal($2);
			ConstructedGrammar.AddAnchor($$, @$.Start);
		}
	;

/***************************** OPTIONS ******************************/

options_section
	:
	| category_block options_section
	;
	
category_block
	: CATEGORY_NAME option_or_block
		{
			OptionCategory optCategory;
			if(!Enum.TryParse($1.ToUpper(), out optCategory))
			{
				Log.Add(Message.Error(
					"Неизвестная категория опций '" + $1 + "'",
					@1.Start,
					"LanD"
				));
			}
			
			foreach(var option in $2)
			{
				bool goodOption = true;
				switch (optCategory)
				{
					case OptionCategory.PARSING:
						ParsingOption parsingOpt;
						goodOption = Enum.TryParse(option.Name.ToUpper(), out parsingOpt);
						if(goodOption) 
							SafeGrammarAction(() => { 
						 		ConstructedGrammar.SetOption(parsingOpt, option.Symbols.ToArray());
						 	}, @1.Start);
						break;
					case OptionCategory.NODES:
						NodeOption nodeOpt;
						goodOption = Enum.TryParse(option.Name.ToUpper(), out nodeOpt);
						if(goodOption)
							SafeGrammarAction(() => { 					
								ConstructedGrammar.SetOption(nodeOpt, option.Symbols.ToArray());
							}, @1.Start);
						break;
					case OptionCategory.MAPPING:
						MappingOption mappingOpt;
						goodOption = Enum.TryParse(option.Name.ToUpper(), out mappingOpt);
						if(goodOption)
							SafeGrammarAction(() => { 			
								ConstructedGrammar.SetOption(mappingOpt, option.Symbols.ToArray(), option.Arguments.ToArray());
							}, @1.Start);
						break;
					default:
						break;
				}
				
				if(!goodOption)
				{
					Log.Add(Message.Error(
						"Опция '" + option.Name + "' не определена для категории '" + $1 + "'",
						@2.Start,
						"LanD"
					));
				}
			}
		}
	;

option_or_block
	: option { $$ = new List<OptionDeclaration>(){ $1 }; }
	| LCURVE_BRACKET options RCURVE_BRACKET { $$ = $2; }
	;
	
options
	: { $$ = new List<OptionDeclaration>(); }
	| options option { $$ = $1; $1.Add($2);  }
	;
	
option
	: OPTION_NAME opt_args identifiers
		{
			$$ = new OptionDeclaration()
			{
				Name = $1,
				Arguments = $2,
				Symbols = $3
			};
		}
	;
	
opt_args
	: LROUND_BRACKET args RROUND_BRACKET { $$ = $2; }
	| { $$ = new List<dynamic>(); }
	;
	
args
	: args COMMA argument 
		{ 
			$$ = $1; 
			$$.Add($3); 
		}
	| argument { $$ = new List<dynamic>(){ $1 }; }
	;
	
	
argument
	: RNUM { $$ = $1; }
	| STRING 
		{
			var generated = ConstructedGrammar.GenerateTerminal((string)$1);
			ConstructedGrammar.AddAnchor(generated, @1.Start);		
			$$ = generated;
		}
	| ID { $$ = $1; }
	| argument_group { $$ = $1; }
	;

argument_group
	: ID ELEM_LROUND_BRACKET args RROUND_BRACKET 
		{ 
			$$ = new ArgumentGroup()
			{
				Name = $1,
				Arguments = $3
			};
		}
	;

identifiers
	: identifiers ID { $$ = $1; $$.Add($2); }
	| { $$ = new List<string>(); }
	;
	
%%

private void SafeGrammarAction(Action action, PointLocation loc)
{
	try
	{
		action();
	}
	catch(IncorrectGrammarException ex)
	{
		Log.Add(Message.Error(
			ex.Message,
			loc,
			"LanD"
		));
	}
}

