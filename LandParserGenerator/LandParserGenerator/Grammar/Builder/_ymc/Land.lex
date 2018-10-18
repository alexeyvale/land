%{
	public List<Message> Log = new List<Message>();
%}

%using System.Linq;
%using QUT.Gppg;
%using Land.Core;

%namespace Land.Core.Builder

%option stack

%x in_terminal_declaration
%x in_options
%x in_option
%x before_option_args
%x before_body_element_args

LETTER [_a-zA-Z]
DIGIT [0-9]
INUM {DIGIT}+
RNUM {INUM}(\.{INUM})?
ID {LETTER}({LETTER}|{DIGIT})*

LINE_COMMENT "//".*    
MULTILINE_COMMENT "/*"([^*]|\*[^/])*"*/"
STRING \'([^'\\]*|(\\\\)+|\\[^\\])*\'

%%

<0, in_options, in_option> {
	{LINE_COMMENT} |
	{MULTILINE_COMMENT} {}
}

// Группа и все возможные квантификаторы

"+" {
	yylval.quantVal = Quantifier.ONE_OR_MORE; 
	return (int)Tokens.ONE_OR_MORE;
}

"*" {
	yylval.quantVal = Quantifier.ZERO_OR_MORE; 
	return (int)Tokens.ZERO_OR_MORE;
}

"?" {
	yylval.quantVal = Quantifier.ZERO_OR_ONE;
	return (int)Tokens.OPTIONAL;
}

"!" {
	return (int)Tokens.PREC_NONEMPTY;
}

"," {
	return (int)Tokens.COMMA;
}

"=>" {
	return (int)Tokens.ARROW;
}

// Начало правила

^{ID} {
	yylval.strVal = yytext;
	return (int)Tokens.ENTITY_NAME;
}

"=" return (int)Tokens.EQUALS;

// Для пар

"%left" return (int)Tokens.LEFT;

"%right" return (int)Tokens.RIGHT;

// Символы, означающие нечто внутри правила

"|" return (int)Tokens.OR;

"~" return (int)Tokens.IS_LIST_NODE;

// Элементы правила

{STRING} {
	yylval.strVal = yytext;
	return (int)Tokens.STRING;
}

":" {
	BEGIN(in_terminal_declaration);
	return (int)Tokens.COLON;
}

<in_terminal_declaration> {
	.+ {
		BEGIN(0);
		
		yylval.strVal = yytext.Trim();
		return (int)Tokens.REGEX;
	}
}

^"%%" {
	BEGIN(in_options);
	return (int)Tokens.PROC;
}

"%"{ID}"("? {	
	if(yytext.Contains('('))
	{
		yyless(yytext.Length - 1);
		yy_push_state(before_option_args);
	}
		
	yylval.strVal = yytext.ToLower().Trim('%').Trim('(');	
	return (int)Tokens.OPTION_NAME;
}

<before_option_args> {
	"(" {
		yy_pop_state();
		return (int)Tokens.OPT_LROUND_BRACKET;
	}
}

<before_body_element_args> {
	"(" {
		yy_pop_state();
		return (int)Tokens.ELEM_LROUND_BRACKET;
	}
}

{ID}"("? {
	if(yytext.Contains('('))
	{
		yyless(yytext.Length - 1);
		yy_push_state(before_body_element_args);
	}
	
	yylval.strVal = yytext;
	return (int)Tokens.ID;
}
	
<0, in_option> {
	"(" return (int)Tokens.LROUND_BRACKET;
	
	")" return (int)Tokens.RROUND_BRACKET;
	
	{RNUM} {
		yylval.doubleVal = double.Parse(yytext, CultureInfo.InvariantCulture);
		return (int)Tokens.RNUM;
	}
}

<in_options> {
	"%"{ID} {
		yylval.strVal = yytext.ToLower().Trim('%');
		return (int)Tokens.CATEGORY_NAME;
	}
	
	{ID} {
		BEGIN(in_option);	
		yylval.strVal = yytext;
		return (int)Tokens.OPTION_NAME;
	}
	
	"{" return (int)Tokens.LCURVE_BRACKET;
	
	"}" return (int)Tokens.RCURVE_BRACKET;
}

<in_option> {	
	{ID} {
		yylval.strVal = yytext;
		return (int)Tokens.ID;
	}
	
	"\n" BEGIN(in_options);
	
	"}" {
		BEGIN(in_options);
		return (int)Tokens.RCURVE_BRACKET;
	}
	
	"%"{ID} yyerror("Встречено имя категории '{0}', ожидалось продолжение опции", yytext);
}


%{
  yylloc = new SegmentLocation()
  	{
		Start = new PointLocation(tokLin, tokCol, tokPos),
		End = new PointLocation(tokELin, tokECol, tokEPos)
	};
%}

%%

public override void yyerror(string format, params object[] args)
{ 
	Log.Add(Message.Error(
		String.Format(format, args.Select(a=>a.ToString()).ToArray()),
		new PointLocation(yyline, yycol, yypos),
		"GPPG"
	));
}
