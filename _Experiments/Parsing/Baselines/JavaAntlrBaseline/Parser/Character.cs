using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public static class Character
{
	public static bool isJavaIdentifierStart(int chrCode)
	{
		var character = (char)chrCode;
		return Char.IsLetter(character) || character == '_' || character == '$';
	}

	public static bool isJavaIdentifierPart(int chrCode)
	{
		var character = (char)chrCode;
		return Char.IsLetterOrDigit(character) || character == '_' || character == '$';
	}
}
