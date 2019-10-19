%{
    public Parser(AbstractScanner<ValueType, SegmentLocation> scanner) : base(scanner) { }
    
    public Grammar ConstructedGrammar;
    public List<Message> Log = new List<Message>();
    
    private HashSet<string> Aliases = new HashSet<string>();
%}

%using System.Linq;
%using Land.Core;

%output = LandParser.cs

%YYLTYPE Land.Core.SegmentLocation

%namespace Land.Core.Specification.Parsing

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
	public List<OptionDeclaration> optionsList;
	public List<string> strList;	
	public List<Alternative> altList;
	
	public HashSet<string> strSet;
	
	public Dictionary<string, Dictionary<string, List<dynamic>>> optionGroupsDict; 
	public Dictionary<string, List<dynamic>> optionsDict;
	public Tuple<string, List<dynamic>> optionTuple;
	
	// Информация о количестве повторений
	public Nullable<Quantifier> optQuantVal;
	public Nullable<double> optDoubleVal;
}

%start lp_description

%left OR

%token ARGS_LROUND_BRACKET LROUND_BRACKET RROUND_BRACKET LCURVE_BRACKET RCURVE_BRACKET
%token COLON COMMA PROC EQUALS MINUS PLUS EXCLAMATION DOT ARROW LEFT RIGHT LINESTART
%token <strVal> REGEX NAMED STRING ID ENTITY_NAME OPTION_NAME CATEGORY_NAME
%token <intVal> POSITION
%token <doubleVal> RNUM
%token <quantVal> OPTIONAL ZERO_OR_MORE ONE_OR_MORE
%token IS_LIST_NODE PREC_NONEMPTY

%type <optQuantVal> quantifier
%type <strVal> entry_core group optional_alias grammar_entity
%type <entryVal> entry
%type <strList> grammar_entities_list
%type <altList> body
%type <boolVal> prec_nonempty opt_linestart
%type <argGroupVal> argument_group
%type <dynamicVal> argument
%type <altVal> alternative
%type <optDeclVal> option

%type <dynamicList> opt_args args context_opt_args entry_args
%type <optionGroupsDict> context_option_groups
%type <optionsDict> context_options
%type <optionsList> option_or_block options
%type <optionTuple> context_option

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
				ConstructedGrammar.AddLocation($1, @1.Start);
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
				ConstructedGrammar.AddLocation($1, @1.Start);
			}, @1.Start);
		}
	;
	
pair_border
	: ID { $$ = new HashSet<string>() { $1 }; }
	| REGEX 
		{ 	
			var generated = ConstructedGrammar.GenerateTerminal($1);
			ConstructedGrammar.AddLocation(generated, @1.Start);
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
				ConstructedGrammar.AddLocation($1, @1.Start);
				
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
	: context_option_groups entry_core entry_args quantifier prec_nonempty
		{ 		
			var opts = new SymbolOptionsManager();
			
			foreach(var group in $1)
			{
				foreach(var option in group.Value)
				{
					opts.Set(group.Key, option.Key, option.Value);
				}
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
					ConstructedGrammar.AddLocation(generated, @$.Start);
					
					$$ = new Entry(generated, opts);
				}
			}
			
			if($$ == null)
			{
				if($2.StartsWith(Grammar.ANY_TOKEN_NAME))
				{
					var args = new SymbolArguments();
					AnyArgument sugarOption;

					if(Enum.TryParse($2.Substring(Grammar.ANY_TOKEN_NAME.Length), out sugarOption))
						args.Set(sugarOption, $3.Select(e=>(string)e)); 
					else
					{
						foreach(var opt in $3)
						{
							var errorGroupName = String.Empty;
							
							if(opt is ArgumentGroup)
							{
								var group = (ArgumentGroup)opt;

								if(Enum.TryParse(group.Name, out sugarOption))
									args.Set(sugarOption, group.Arguments.Select(e=>(string)e)); 
								else
									errorGroupName = group.Name;
							}
							else if(opt is String)
							{
								if(Enum.TryParse((string)opt, out sugarOption))
									args.Set(sugarOption, new List<string>()); 
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
					
					$$ = new Entry(Grammar.ANY_TOKEN_NAME, opts, args);
				}
				else
				{
					$$ = new Entry($2, opts);
				}
			}
		}
	;
	
entry_args
	: ARGS_LROUND_BRACKET args RROUND_BRACKET { $$ = $2; }
	| { $$ = new List<dynamic>(); }
	;
	
context_option_groups
	: context_option_groups CATEGORY_NAME ARGS_LROUND_BRACKET context_options RROUND_BRACKET
		{ 
			$$ = $1; 
			$$[$2] = $4;
		}
	| { $$ = new Dictionary<string, Dictionary<string, List<dynamic>>>(); }
	;
	
context_options
	: context_options COMMA context_option
		{
			$$ = $1;
			$$[$3.Item1] = $3.Item2;
		}
	| context_option
		{
			$$ = new Dictionary<string, List<dynamic>>();
			$$[$1.Item1] = $1.Item2;
		}
	;
	
context_option
	: ID context_opt_args { $$ = new Tuple<string, List<dynamic>>($1, $2); }
	;

context_opt_args
	: ARGS_LROUND_BRACKET args RROUND_BRACKET { $$ = $2; }
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
	: REGEX
		{ 
			$$ = ConstructedGrammar.GenerateTerminal($1);
			ConstructedGrammar.AddLocation($$, @$.Start);
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
			ConstructedGrammar.AddLocation($$, @$.Start);
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
			foreach(var option in $2)
			{
				SafeGrammarAction(() => {
					ConstructedGrammar.SetOption($1, option.Name, option.Symbols, option.Arguments);
				}, @1.Start);
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
	: OPTION_NAME opt_args grammar_entities_list
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
	| REGEX 
		{
			var generated = ConstructedGrammar.GenerateTerminal((string)$1);
			ConstructedGrammar.AddLocation(generated, @1.Start);		
			$$ = generated;
		}
	| STRING { $$ = $1.Substring(1, $1.Length - 2); }
	| ID { $$ = $1; }
	| argument_group { $$ = $1; }
	;

argument_group
	: ID LROUND_BRACKET args RROUND_BRACKET 
		{ 
			$$ = new ArgumentGroup()
			{
				Name = $1,
				Arguments = $3
			};
		}
	;

grammar_entities_list
	: grammar_entities_list grammar_entity 
		{ 
			$$ = $1;	
			if(!String.IsNullOrEmpty($2))
				$$.Add($2);
		}
	| { $$ = new List<string>(); }
	;
	
grammar_entity
	: ID { $$ = $1; }
	| REGEX 
		{ 
			$$ = ConstructedGrammar.GetTerminal($1);
			
			if(String.IsNullOrEmpty($$))
			{
				Log.Add(Message.Error(
					"Не найден токен, определяемый регулярным выражением " + $1,
					@1.Start,
					"LanD"
				));
			}
		}
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
