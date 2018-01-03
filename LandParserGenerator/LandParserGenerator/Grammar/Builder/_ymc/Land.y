%{
    public Parser(AbstractScanner<LandParserGenerator.Builder.ValueType, LexLocation> scanner) : base(scanner) { }
    
    public Grammar ConstructedGrammar = new Grammar();
%}

%using System.Linq;
%using LandParserGenerator;

%output = LandParser.cs

%namespace LandParserGenerator.Builder

%union { 
	public int intVal; 
	public string strVal;
	public List<string> strList;
	
	public List<Alternative> altList;
	// Информация о количестве повторений
	public Quantifier quantVal;
}

%start lp_description

%left OR
%token COLON LPAR RPAR COMMA PROC EQUALS MINUS PLUS EXCLAMATION ADD_CHILD DOT
%token <strVal> REGEX NAMED STRING ID ENTITY_NAME
%token <intVal> POSITION
%token <quantVal> OPTIONAL ZERO_OR_MORE ONE_OR_MORE
%token OPTION_ITEMSLIST OPTION_SKIP OPTION_NOITEMS

%type <quantVal> quantifier
%type <strVal> body_element_core body_element_atom group body_element
%type <strList> identifiers
%type <altList> body

%%

lp_description 
	: structure PROC options
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
	: ENTITY_NAME REGEX { ConstructedGrammar.DeclareTerminal($1, $2); }
	;

/******* ID ::= 'regex' ID "string" (group)[*|+]  ********/
nonterminal
	: ENTITY_NAME EQUALS body 
		{ ConstructedGrammar.DeclareNonterminal($1, $3); }
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
	: body_element_core quantifier 
		{ 
			$$ = new Entry($1, $2);
		}
	;
	
quantifier
	: OPTIONAL { $$ = $1; }
	| ZERO_OR_MORE { $$ = $1; }
	| ONE_OR_MORE { $$ = $1; }
	| { $$ = Quantifier.ONE; }
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
	: itemslist_option
	| skip_option
	| noitems_option
	;
	
itemslist_option
	: OPTION_ITEMSLIST identifiers
		{ ConstructedGrammar.SetListSymbols($2.ToArray()); }
	;
	
skip_option
	: OPTION_SKIP identifiers
		{ ConstructedGrammar.SetSkipTokens($2.ToArray()); }
	;	

noitems_option
	: OPTION_NOITEMS identifiers
	;
	
identifiers
	: identifiers ID 
		{ $$ = $1; $$.Add($2); }
	| ID 
		{ $$ = new List<string>(); $$.Add($1); }
	;

