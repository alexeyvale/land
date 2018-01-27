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
	public bool boolVal;
	public string strVal;
	public List<string> strList;
	
	public List<Alternative> altList;
	// Информация о количестве повторений
	public Nullable<Quantifier> quantVal;
}

%start lp_description

%left OR
%token COLON LPAR RPAR COMMA PROC EQUALS MINUS PLUS EXCLAMATION ADD_CHILD DOT
%token <strVal> REGEX NAMED STRING ID ENTITY_NAME OPTION_NAME
%token <intVal> POSITION
%token <quantVal> OPTIONAL ZERO_OR_MORE ONE_OR_MORE
%token IS_LIST_NODE PREC_NONEMPTY

%type <quantVal> quantifier
%type <strVal> body_element_core body_element_atom group body_element context_option
%type <strList> identifiers
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
	: context_option body_element_core quantifier prec_nonempty
		{ 		
			NodeOption option;
			
			if(!Enum.TryParse<NodeOption>($1.ToUpper(), out option))
			{
					Errors.Add(Message.Error(
						"Неизвестная опция '" + $1 + "'",
						@1.StartLine, @1.StartColumn,
						"LanD"
					));
					option = NodeOption.NONE;
			}
			
			if($3.HasValue)
			{
				var generated = ConstructedGrammar.GenerateNonterminal($2, $3.Value, $4);
				ConstructedGrammar.AddAnchor(generated, @$);
				
				$$ = new Entry(generated, option);
			}
			else
			{
				$$ = new Entry($2, option);
			}
		}
	;
	
context_option
	: OPTION_NAME { $$ = $1; }
	| { $$ = NodeOption.NONE.ToString(); }
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
	: OPTION_NAME ID identifiers
		{
			switch($2)
			{
				case "skip":
					SafeGrammarAction(() => { ConstructedGrammar.SetSkipTokens($3.ToArray()); }, @1);
					break;
				case "start":
					SafeGrammarAction(() => { ConstructedGrammar.SetStartSymbol($3[0]); }, @1);
					break;
				case "ignorecase":
					ConstructedGrammar.IsCaseSensitive = false;
					break;
				default:
					NodeOption option;	
					if(!Enum.TryParse<NodeOption>($2.ToUpper(), out option))
					{
						Errors.Add(Message.Error(
							"Неизвестная опция '" + $2 + "'",
							@1.StartLine, @1.StartColumn,
							"LanD"
						));
					}
					else
					{
						SafeGrammarAction(() => { ConstructedGrammar.SetSymbolsForOption(option, $3.ToArray()); }, @1);
					}				
					break;
			}
		}
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

