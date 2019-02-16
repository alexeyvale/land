using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SharpDependencyAnalyzer
{
	public class RoslynNodeTypeAttribute: Attribute
	{
		public List<Type> Types { get; set; }

		public RoslynNodeTypeAttribute(params Type[] types)
		{
			Types = types.ToList();
		}
	}

	public enum CSharpEntityType
	{
		[RoslynNodeType(typeof(NamespaceDeclarationSyntax))]
		Namespace,
		[RoslynNodeType(typeof(ClassDeclarationSyntax), typeof(StructDeclarationSyntax), typeof(InterfaceDeclarationSyntax))]
		ClassStructInterface,
		[RoslynNodeType(typeof(MethodDeclarationSyntax))]
		Method,
		[RoslynNodeType(typeof(VariableDeclaratorSyntax))]
		Field,
		[RoslynNodeType(typeof(PropertyDeclarationSyntax))]
		Property
	}

	public class Entity
	{
		public CSharpEntityType Type { get; set; }
		public string Name { get; set; }
	}

	public class Request
	{
		public string FilePath { get; set; }
		public List<Entity> EntityPath { get;set; }
	}
}
