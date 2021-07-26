using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using Land.Core.Specification;
using Land.Core.Lexing;

namespace Land.Core
{
	public static class Extensions
	{
		/// <summary>
		/// Получение строкового описания токена для сущности, обладающей доступом к грамматике языка
		/// </summary>
		public static string Messagify(this IGrammarProvided target, IToken token)
		{
			//if (target.GrammarObject.Options.IsSet(ParsingOption.GROUP_NAME, ParsingOption.USERIFY, token.Name))
			//{
			//	return $"'{token.Text}' ({target.GrammarObject.Options.GetParams(ParsingOption.GROUP_NAME, ParsingOption.USERIFY, token.Name)[0]})";
			//}

			var userified = target.GrammarObject.Userify(token.Name);

			if (userified == token.Name && token.Name != Grammar.ANY_TOKEN_NAME && token.Name != Grammar.EOF_TOKEN_NAME)
			{
				return $"{token.Name}: '{token.Text}'";
			}
			else
			{
				return userified;
			}
		}

		/// <summary>
		/// Получение строкового описания символа для сущности, обладающей доступом к грамматике языка
		/// </summary>
		public static string Messagify(this IGrammarProvided target, string symbol)
		{
			//if (target.GrammarObject.Options.IsSet(ParsingOption.GROUP_NAME, ParsingOption.USERIFY, symbol))
			//{
			//	return $"{target.GrammarObject.Options.GetParams(ParsingOption.GROUP_NAME, ParsingOption.USERIFY, symbol)[0]}";
			//}

			return target.GrammarObject.Userify(symbol);
		}

		/// <summary>
		/// Получение атрибута заданного типа для значения перечислимого типа
		/// </summary>
		public static T GetAttribute<T>(this Enum enumVal) where T : System.Attribute
		{
			var type = enumVal.GetType();
			var memInfo = type.GetMember(enumVal.ToString());
			var attributes = memInfo[0].GetCustomAttributes(typeof(T), false);
			return (attributes.Length > 0) ? (T)attributes[0] : null;
		}

		/// <summary>
		/// Получение атрибута Description
		/// </summary>
		public static string GetDescription(this Enum enumVal)
		{
			return enumVal.GetAttribute<DescriptionAttribute>().Description;
		}
	}
}
