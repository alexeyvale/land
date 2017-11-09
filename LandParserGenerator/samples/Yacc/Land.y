%{
    public Parser(AbstractScanner<Land.ValueType, LexLocation> scanner) : base(scanner) { }
    
    public YaccLexBuilder Builder = new YaccLexBuilder();
%}

%using System.Linq;

%output = LandParser.cs

%namespace Land

%union { 
	public int intVal; 
	public string strVal;
	public List<string> strList;
	
	// Информация для поиска составляющей имени элемента
	public NameComponentLocation nameCompVal;
	public List<NameComponentLocation> nameCompList;
	// Информация о количестве повторений
	public Quantifier quantVal;
}

%start lp_description

%left OR
%token COLON LPAR RPAR COMMA PROC EQUALS MINUS PLUS EXCLAMATION ADD_CHILD DOT
%token <strVal> REGEX NAMED STRING ID ENTITY_NAME
%token <intVal> POSITION
%token <quantVal> OPTIONAL ZERO_OR_MORE ONE_OR_MORE
%token OPTION_ITEMSLIST OPTION_NAMESPACE OPTION_SKIP OPTION_NOITEMS

%type <quantVal> quantifier
%type <strVal> body_element_core body_element_atom group body_element name
%type <strList> body target_language_entity_path identifiers regexps
%type <nameCompVal> name_arg
%type <nameCompList> name_args_list name_args

%%

lp_description 
	: structure PROC options 
		{ 
			Builder.RemoveIdenticalRules(); 
			//Builder.ExtractAlternativesFromBiggerOnes(); 
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
	;
	
terminal
	: ENTITY_NAME regexps { Builder.AddUserTerminal($1, $2); }
	;
	
regexps
	: REGEX 
		{ $$ = new List<string>(); $$.Add($1); }
	| regexps REGEX
		{ $$ = $1; $$.Add($2); }
	;


/******* ID ($0, $ID) ::= r(regex) ID string (group)[*|+]  ********/
nonterminal
	: ENTITY_NAME name_args EQUALS body 
		{ Builder.AddUserNonterminal($1, $4, $2); }
	;
	
name_args
	: { $$ = null; }
	| LPAR name_args_list RPAR { $$ = $2; }
	;
	
name_args_list
	: name_args_list COMMA name_arg { $$ = $1; $$.Add($3); }
	| name_arg { $$ = new List<NameComponentLocation>(); $$.Add($1); }
	;
	
name_arg
	: POSITION 
		{ 
			$$ = new NameComponentLocation();
			$$.Position = $1;			
		}
	| NAMED 
		{ 
			$$ = new NameComponentLocation();
			$$.UserDefinedName = $1;			
		}
	;
	
body
	: body body_element 
		{ 
			$1.Add($2); 
			$$ = $1; 
		}
	| body_element 
		{ 
			$$ = new List<string>(); 
			$$.Add($1); 
		}
	;
	
body_element
	: modifiers name body_element_core quantifier 
		{ 
			$$ = Builder.AddAutoNonterminal($3, $4); 
			if(!String.IsNullOrEmpty($2))
				Builder.AddUserDefinedName($2, $$);
		}
	| modifiers name body_element_core 
		{ 		
			$$ = $3; 
			if(!String.IsNullOrEmpty($2))
				Builder.AddUserDefinedName($2, $$);
		} 
	| body_element OR body_element 
		{ 
			$$ = Builder.AddAutoNonterminal($1, $3); 			
		}
	;
	
modifiers
	:
	| modifiers modifier
	;
	
modifier
	: ADD_CHILD
	;
	
name
	: { $$ = null; }
	| NAMED EQUALS { $$ = $1; }
	;
	
quantifier
	: OPTIONAL { $$ = $1; }
	| ZERO_OR_MORE { $$ = $1; }
	| ONE_OR_MORE { $$ = $1; }
	;
	
body_element_core
	: body_element_atom
		{ $$ = $1; }
	| group 
		{ $$ = $1; }
	//| macros
	;
	
body_element_atom
	: REGEX 
		{ 
			$$ = Builder.AddAutoTerminal($1); 
		}
	| STRING
		{ 
			$$ = Builder.AddAutoTerminal($1);
		}
	| ID 
		{ $$ = $1; }
	;
	
group
	: LPAR body RPAR { $$ = Builder.AddAutoNonterminal($2); }
	;
	
/***************************** RELATIONS *****************************

relations
	:
	;
	
*/

/***************************** OPTIONS ******************************/

options
	:
	| options option
	;
	
option
	: namespace_option
	| itemslist_option
	| skip_option
	| noitems_option
	;
	
namespace_option
	: OPTION_NAMESPACE target_language_entity_path
		{ Builder.Namespace = String.Join(".", $2); }
	;
	
target_language_entity_path
	: ID 
		{ $$ = new List<string>(); $$.Add($1); }
	| target_language_entity_path DOT ID 
		{ $$ = $1; $$.Add($3); }
	;
	
itemslist_option
	: OPTION_ITEMSLIST identifiers
		{ Builder.NodeListNonterminals = $2; }
	;
	
skip_option
	: OPTION_SKIP identifiers
		{ Builder.SkipTerminals = $2; }
	;	

noitems_option
	: OPTION_NOITEMS identifiers
		{ Builder.LeafNonterminals = $2; }
	;
	
identifiers
	: identifiers ID 
		{ $$ = $1; $$.Add($2); }
	| ID 
		{ $$ = new List<string>(); $$.Add($1); }
	;

