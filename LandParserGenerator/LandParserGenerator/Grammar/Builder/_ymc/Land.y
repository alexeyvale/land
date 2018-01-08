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
%token <strVal> REGEX NAMED STRING ID ENTITY_NAME
%token <intVal> POSITION
%token <quantVal> OPTIONAL ZERO_OR_MORE ONE_OR_MORE
%token OPTION_SKIP OPTION_GHOST OPTION_START IS_LIST_NODE

%type <quantVal> quantifier
%type <strVal> body_element_core body_element_atom group body_element
%type <strList> identifiers
%type <altList> body
%type <boolVal> is_list_node

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
	: ENTITY_NAME COLON REGEX { SafeGrammarAction(() => { ConstructedGrammar.DeclareTerminal($1, $3); }, @1); }
	;

/******* ID = ID 'string' (group)[*|+|?]  ********/
nonterminal
	: ENTITY_NAME EQUALS body 
		{ SafeGrammarAction(() => { ConstructedGrammar.DeclareNonterminal($1, $3); }, @1); }
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
	: is_list_node body_element_core quantifier 
		{ 
			if($3.HasValue)
			{
				var generated = ConstructedGrammar.GenerateNonterminal($2, $3.Value);
				$$ = new Entry(generated);
				
				if($1) { SafeGrammarAction(() => { ConstructedGrammar.SetListSymbol($$); }, @2); }
			}
			else
			{
				$$ = new Entry($2);
			}
		}
	;
	
is_list_node
	: IS_LIST_NODE { $$ = true; }
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
		}
	| ID 
		{ $$ = $1; }
	;
	
group
	: LPAR body RPAR { $$ = ConstructedGrammar.GenerateNonterminal($2); }
	;

/***************************** OPTIONS ******************************/

options
	:
	| options option
	;
	
option
	: ghost_option
	| skip_option
	| start_option
	;
	
skip_option
	: OPTION_SKIP identifiers
		{ SafeGrammarAction(() => { ConstructedGrammar.SetSkipTokens($2.ToArray()); }, @1); }
	;	

ghost_option
	: OPTION_GHOST identifiers
		{ SafeGrammarAction(() => { ConstructedGrammar.SetGhostSymbols($2.ToArray()); }, @1); }
	;
	
start_option
	: OPTION_START ID
		{ SafeGrammarAction(() => { ConstructedGrammar.SetStartSymbol($2); }, @1); }
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

