using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.FindSymbols;

namespace SharpDependencyAnalyzer
{
	public static class EnumExtension
	{
		public static T GetAttribute<T>(this Enum enumVal) where T : System.Attribute
		{
			var type = enumVal.GetType();
			var memInfo = type.GetMember(enumVal.ToString());
			var attributes = memInfo[0].GetCustomAttributes(typeof(T), false);
			return (attributes.Length > 0) ? (T)attributes[0] : null;
		}
	}

	public class SharpDependencyAnalyzer
	{
		private Solution Solution { get; set; }
		private Dictionary<string, Compilation> Projects { get; set; }

		public void LoadSolution(string path)
		{
			Projects = new Dictionary<string, Compilation>();

			Solution = MSBuildWorkspace.Create().OpenSolutionAsync(path).Result;
			var depsGraph = Solution.GetProjectDependencyGraph();

			foreach (var projectId in depsGraph.GetTopologicallySortedProjects())
			{
				var projName = Solution.GetProject(projectId).Name;
				var projDeps = depsGraph.GetProjectsThatThisProjectDirectlyDependsOn(projectId);

				foreach (var depId in projDeps)
				{
					var dep = Solution.GetProject(depId);

					Projects[dep.Name] = dep.GetCompilationAsync().Result;
				}
			}
		}

		public Response GetDependencies(Request request)
		{
			var response = new Response();
			var project = Projects.FirstOrDefault(p => p.Value.SyntaxTrees.Any(t => t.FilePath == request.FilePath)).Value;

			if (project != null)
			{
				var tree = project.SyntaxTrees.FirstOrDefault(t => t.FilePath == request.FilePath);
				var entityNode = tree.GetRoot();

				foreach(var entity in request.EntityPath)
				{
					var types = entity.Type.GetAttribute<RoslynNodeTypeAttribute>().Types;
					var candidates = new Queue<SyntaxNode>();
					candidates.Enqueue(entityNode);
					entityNode = null;

					while(candidates.Count > 0)
					{
						var current = candidates.Dequeue();

						if(types.Any(t => current.GetType() == t) && GetName(current) == entity.Name)
						{
							entityNode = current;
							break;
						}
					}

					if (entityNode == null)
						break;
				}

				if(entityNode != null)
				{
					var semantic = project.GetSemanticModel(tree);
					var symbol = semantic.GetDeclaredSymbol(entityNode);

					var references = SymbolFinder.FindReferencesAsync(symbol, Solution).Result;

					foreach(var loc in references.SelectMany(s=>s.Locations))
					{
						if (loc.Location.IsInSource)
						{
							var ancestorOperator = loc.Location.SourceTree.GetRoot().FirstAncestorOrSelf<OperatorDeclarationSyntax>();

							response.Points.Add(new ReferencePoint()
							{
								FilePath = loc.Document.FilePath,
								PointOffset = loc.Location.SourceSpan.Start,
								EnclosingOperatorStartOffset = ancestorOperator.Span.Start,
								EnclosingOperatorEndOffset = ancestorOperator.Span.End
							});
						}
					}
				}
			}

			return response;
		}

		private string GetName(SyntaxNode node)
		{
			if (node is NamespaceDeclarationSyntax @namespace)
				return @namespace.Name.ToString();
			else if (node is ClassDeclarationSyntax @class)
				return @class.Identifier.ToString();
			else if (node is StructDeclarationSyntax @struct)
				return @struct.Identifier.ToString();
			else if (node is InterfaceDeclarationSyntax @interface)
				return @interface.Identifier.ToString();
			else if (node is MethodDeclarationSyntax method)
				return method.Identifier.ToString();
			else if (node is PropertyDeclarationSyntax property)
				return property.Identifier.ToString();
			else if (node is VariableDeclaratorSyntax field)
				return field.Identifier.ToString();
			else
				return String.Empty;
		}
    }
}
