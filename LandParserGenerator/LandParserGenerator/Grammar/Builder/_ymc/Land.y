%{
    public Parser(AbstractScanner<LandParserGenerator.Builder.ValueType, LexLocation> scanner) : base(scanner) { }
    
    public Grammar ConstructedGrammar;
    public List<Message> Errors = new List<Message>();
%}

%using System.Linq;
%using LandParserGenerator;

%output = LandParser.cs

%namespace LandParserGenerator.Builder

%union { 
	public int intVal; 
	public double doubleVal;
	public Quantifier quantVal;
	public bool boolVal;
	public string strVal;
	
	public List<string> strList;	
	public List<Alternative> altList;
	
	// Информация о количестве повторений
	public Nullable<Quantifier> optQuantVal;
	public Nullable<double> optDoubleVal;
}

%start lp_description

%left OR
%token COLON LPAR RPAR COMMA PROC EQUALS MINUS PLUS EXCLAMATION ADD_CHILD DOT
%token <strVal> REGEX NAMED STRING ID ENTITY_NAME OPTION_NAME CATEGORY_NAME
%token <intVal> POSITION
%token <doubleVal> RNUM
%token <quantVal> OPTIONAL ZERO_OR_MORE ONE_OR_MORE
%token IS_LIST_NODE PREC_NONEMPTY

%type <optQuantVal> quantifier
%type <optDoubleVal> opt_arg
%type <strVal> body_element_core body_element_atom group body_element
%type <strList> identifiers context_options
%type <altList> body
%type <boolVal> prec_nonempty

%%

lp_description 
	: structure PROC options { Errors.AddRange(ConstructedGrammar.CheckValidity()); }
	;

/***************************** STRUCTURE ******************************/
	
structure 
	: structure element
	| element
	;

element
	: terminal
	| nonterminal
	;
	
terminal
	: ENTITY_NAME COLON REGEX 
		{ 
			SafeGrammarAction(() => { 
				ConstructedGrammar.DeclareTerminal($1, $3);
				ConstructedGrammar.AddAnchor($1, @1);
			}, @1);
		}
	;

/******* ID = ID 'string' (group)[*|+|?]  ********/
nonterminal
	: ENTITY_NAME EQUALS body 
		{ 
			SafeGrammarAction(() => { 
				ConstructedGrammar.DeclareNonterminal($1, $3);
				ConstructedGrammar.AddAnchor($1, @1);
			}, @1);
		}
	;
	
body
	: body body_element 
		{ 
			$$ = $1; 
			$$[$$.Count-1].Add($2); 	
		}
	| body OR 
		{ 
			$$ = $1;
			$$.Add(new Alternative());		
		}
	|  
		{ 
			$$ = new List<Alternative>(); 
			$$.Add(new Alternative()); 
		}
	;
	
body_element
	: context_options body_element_core quantifier prec_nonempty
		{ 		
			var opts = new LocalOptions();
			
			foreach(var opt in $1)
			{
				NodeOption nodeOpt;		
				if(!Enum.TryParse<NodeOption>(opt.ToUpper(), out nodeOpt))
				{
					MappingOption mapOpt;
					if(!Enum.TryParse<MappingOption>(opt.ToUpper(), out mapOpt))
					{
						Errors.Add(Message.Error(
							"Неизвестная опция '" + opt + "'",
							@1.StartLine, @1.StartColumn,
							"LanD"
						));
					}
					else
						opts.Set(mapOpt);				
				}
				else
					opts.Set(nodeOpt);	
			}
			
			if($3.HasValue)
			{
				var generated = ConstructedGrammar.GenerateNonterminal($2, $3.Value, $4);
				ConstructedGrammar.AddAnchor(generated, @$);
				
				$$ = new Entry(generated, opts);
			}
			else
			{
				$$ = new Entry($2, opts);
			}
		}
	;
	
context_options
	: context_options OPTION_NAME opt_arg { $$ = $1; $$.Add($2); }
	| { $$ = new List<string>(); }
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
	
body_element_core
	: body_element_atom
		{ $$ = $1; }
	| group 
		{ $$ = $1; }
	;
	
body_element_atom
	: STRING
		{ 
			$$ = ConstructedGrammar.GenerateTerminal($1);
			ConstructedGrammar.AddAnchor($$, @$);
		}
	| ID 
		{ $$ = $1; }
	;
	
group
	: LPAR body RPAR 
		{ 
			$$ = ConstructedGrammar.GenerateNonterminal($2);
			ConstructedGrammar.AddAnchor($$, @$);
		}
	;

/***************************** OPTIONS ******************************/

options
	:
	| options option
	;
	
option
	: CATEGORY_NAME ID opt_arg identifiers
		{
			OptionCategory optCategory;
			if(!Enum.TryParse($1.ToUpper(), out optCategory))
			{
				Errors.Add(Message.Error(
					"Неизвестная категория опций '" + $1 + "'",
					@1.StartLine, @1.StartColumn,
					"LanD"
				));
			}

			bool goodOption = true;
			switch (optCategory)
			{
				case OptionCategory.PARSING:
					ParsingOption parsingOpt;
					goodOption = Enum.TryParse($2.ToUpper(), out parsingOpt);
					if(goodOption) ConstructedGrammar.SetOption(parsingOpt, $4.ToArray());
					break;
				case OptionCategory.NODES:
					NodeOption nodeOpt;
					goodOption = Enum.TryParse($2.ToUpper(), out nodeOpt);
					if(goodOption) ConstructedGrammar.SetOption(nodeOpt, $4.ToArray());
					break;
				case OptionCategory.MAPPING:
					MappingOption mappingOpt;
					goodOption = Enum.TryParse($2.ToUpper(), out mappingOpt);
					if(goodOption) ConstructedGrammar.SetOption(mappingOpt, $4.ToArray(), $3);
					break;
				default:
					break;
			}
			
			if(!goodOption)
			{
				Errors.Add(Message.Error(
					"Опция '" + $2 + "' не определена для категории '" + $1 + "'",
					@2.StartLine, @2.StartColumn,
					"LanD"
				));
			}
		}
	;
	
opt_arg
	: RNUM RPAR { $$ = $1; }
	| { $$ = null; }
	;
	
identifiers
	: identifiers ID 
		{ $$ = $1; $$.Add($2); }
	| ID 
		{ $$ = new List<string>(); $$.Add($1); }
	;
	
%%

private void SafeGrammarAction(Action action, LexLocation loc)
{
	try
	{
		action();
	}
	catch(IncorrectGrammarException ex)
	{
		Errors.Add(Message.Error(
			ex.Message,
			loc.StartLine, loc.StartColumn,
			"LanD"
		));
	}
}

