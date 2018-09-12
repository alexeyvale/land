using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	[DataContract(IsReference = true)]
	public class HeaderContextElement
	{
		[DataMember]
		public double Priority { get; set; }

		[DataMember]
		public string Type { get; set; }

		[DataMember]
		public List<string> Value { get; set; }

		public override bool Equals(object obj)
		{
			if(obj is HeaderContextElement elem)
			{
				return ReferenceEquals(this, elem) || Priority == elem.Priority 
					&& Type == elem.Type 
					&& Value.SequenceEqual(elem.Value);
			}

			return false;
		}

		public static bool operator ==(HeaderContextElement a, HeaderContextElement b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(HeaderContextElement a, HeaderContextElement b)
		{
			return !a.Equals(b);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		public static explicit operator HeaderContextElement(Node node)
		{
			return new HeaderContextElement()
			{
				Type = node.Type,
				Value = node.Value,
				Priority = node.Options.Priority.Value
			};
		}
	}

	[DataContract(IsReference = true)]
	public class InnerContextElement
	{
		[DataMember]
		public double Priority { get; set; }

		[DataMember]
		public string Type { get; set; }

		public List<HeaderContextElement> HeaderContext { get; set; }

		public static explicit operator InnerContextElement(Node node)
		{
			return new InnerContextElement()
			{
				Type = node.Type,
				HeaderContext = PointContext.GetHeaderContext(node),
				Priority = node.Options.Priority.Value
			};
		}
	}

	[DataContract(IsReference = true)]
	public class AncestorsContextElement
	{
		[DataMember]
		public string Type { get; set; }

		[DataMember]
		public List<HeaderContextElement> HeaderContext { get; set; }

		public override bool Equals(object obj)
		{
			if (obj is AncestorsContextElement elem)
			{
				return ReferenceEquals(this, elem) || Type == elem.Type
					&& HeaderContext.SequenceEqual(elem.HeaderContext);
			}

			return false;
		}

		public static bool operator ==(AncestorsContextElement a, AncestorsContextElement b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(AncestorsContextElement a, AncestorsContextElement b)
		{
			return !a.Equals(b);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		public static explicit operator AncestorsContextElement(Node node)
		{
			return new AncestorsContextElement()
			{
				Type = node.Type,
				HeaderContext = PointContext.GetHeaderContext(node)
			};
		}
	}

	[DataContract(IsReference = true)]
	public class SiblingsContextElement
	{
		[DataMember]
		public string Type { get; set; }

		public static explicit operator SiblingsContextElement(Node node)
		{
			return new SiblingsContextElement()
			{
				Type = node.Type
			};
		}
	}

	[DataContract(IsReference = true)]
	public class PointContext
	{
		public string FileName { get; set; }

		public string NodeType { get; set; }

		/// <summary>
		/// Контекст заголовка узла, к которому привязана точка разметки
		/// </summary>
		public List<HeaderContextElement> HeaderContext { get; set; }

		/// <summary>
		/// Контекст потомков узла, к которому привязана точка разметки
		/// </summary>
		public List<InnerContextElement> InnerContext { get; set; }

		/// <summary>
		/// Контекст предков узла, к которому привязана точка разметки
		/// </summary>
		public List<AncestorsContextElement> AncestorsContext { get; set; }

		/// <summary>
		/// Контекст уровня, на котором находится узел, к которому привязана точка разметки
		/// </summary>
		public List<SiblingsContextElement> SiblingsContext { get; set; }

		public static List<HeaderContextElement> GetHeaderContext(Node node)
		{
			if (node.Value.Count > 0)
			{
				return new List<HeaderContextElement>() { new HeaderContextElement()
				{
					 Type = node.Type,
					 Priority = 1,
					 Value = node.Value
				}};
			}
			else
			{
				return node.Children
					.Where(c => c.Children.Count == 0 && c.Symbol != Grammar.ANY_TOKEN_NAME)
					.Select(c => (HeaderContextElement)c).ToList();
			}
		}

		public static List<AncestorsContextElement> GetAncestorsContext(Node node)
		{
			var context = new List<AncestorsContextElement>();
			var currentNode = node.Parent;

			while(currentNode != null)
			{
				context.Add((AncestorsContextElement)currentNode);
				currentNode = currentNode.Parent;
			}

			return context;
		}

		public static List<InnerContextElement> GetInnerContext(Node node)
		{
			return node.Children
					.Where(c => c.Children.Count > 0)
					.Select(c => (InnerContextElement)c).ToList();
		}

		public static List<SiblingsContextElement> GetSiblingsContext(Node node)
		{
			return node.Parent != null 
				? node.Parent.Children.Select(c => (SiblingsContextElement)c).ToList()
				: new List<SiblingsContextElement>();
		}

		public static PointContext Create(string fileName, Node node)
		{
			return new PointContext()
			{
				FileName = fileName,
				NodeType = node.Type,
				HeaderContext = GetHeaderContext(node),
				AncestorsContext = GetAncestorsContext(node),
				InnerContext = GetInnerContext(node),
				SiblingsContext = GetSiblingsContext(node)
			};
		}

		public static void ComplementContexts(PointContext context, Node node)
		{
			if (context.HeaderContext == null)
				context.HeaderContext = GetHeaderContext(node);

			if (context.AncestorsContext == null)
				context.AncestorsContext = GetAncestorsContext(node);

			if (context.InnerContext == null)
				context.InnerContext = GetInnerContext(node);

			if (context.SiblingsContext == null)
				context.SiblingsContext = GetSiblingsContext(node);
		}
	}
}
