using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	public interface IEqualsIgnoreValue
	{
		bool EqualsIgnoreValue(object obj);
	}

	[DataContract]
	public abstract class TypedPrioritizedContextElement
	{
		[DataMember]
		public double Priority { get; set; }

		[DataMember]
		public string Type { get; set; }
	}

	[DataContract]
	public class HeaderContextElement: TypedPrioritizedContextElement, IEqualsIgnoreValue
	{
		[DataMember]
		public bool ExactMatch { get; set; }

		[DataMember]
		public List<string> Value { get; set; }

		/// Проверка двух контекстов на совпадение всех полей, кроме поля Value
		public bool EqualsIgnoreValue(object obj)
		{
			if (obj is HeaderContextElement elem)
			{
				return ReferenceEquals(this, elem) || Priority == elem.Priority
					&& Type == elem.Type
					&& ExactMatch == elem.ExactMatch;
			}

			return false;
		}

		public override bool Equals(object obj)
		{
			if(obj is HeaderContextElement elem)
			{
				return ReferenceEquals(this, elem) || Priority == elem.Priority 
					&& Type == elem.Type
					&& ExactMatch == elem.ExactMatch
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
				Priority = node.Options.Priority.Value,
				ExactMatch = node.Options.ExactMatch
			};
		}
	}

	[DataContract]
	public class InnerContextElement: TypedPrioritizedContextElement, IEqualsIgnoreValue
	{
		/// Для базовой версии алгоритмов перепривязки
		public List<HeaderContextElement> HeaderContext { get; set; }

		[DataMember]
		public TextOrHash Content { get; set; }

		public InnerContextElement() { }

		public InnerContextElement(Node node, string fileText)
		{
			Type = node.Type;
			Priority = node.Options.Priority.Value;
			HeaderContext = PointContext.GetHeaderContext(node);

			/// Удаляем из текста все пробельные символы
			var text = fileText.Substring(
				node.Location.Start.Offset, 
				node.Location.Length.Value
			);

			Content = new TextOrHash(text);
		}

		public InnerContextElement(List<SegmentLocation> locations, string fileText)
		{
			/// Удаляем из текста все пробельные символы
			var text = String.Join(" ", locations.Select(l => 
				fileText.Substring(l.Start.Offset, l.Length.Value)
			));

			Content = new TextOrHash(text);
		}

		public bool EqualsIgnoreValue(object obj)
		{
			if (obj is InnerContextElement elem)
			{
				return ReferenceEquals(this, elem) || Priority == elem.Priority
					&& Type == elem.Type;
			}

			return false;
		}
	}

	[DataContract]
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

	[DataContract]
	public class SiblingsContext
	{
		[DataMember]
		public TextOrHash Before { get; set; }

		[DataMember]
		public TextOrHash After { get; set; }
	}

	[DataContract]
	public class TextOrHash
	{
		public const int MAX_TEXT_LENGTH = 100;

		[DataMember]
		public string Text { get; set; }

		[DataMember]
		public int TextLength { get; set; }

		[DataMember]
		public byte[] Hash { get; set; }

		public TextOrHash() { }

		public TextOrHash(string text)
		{
			text = System.Text.RegularExpressions.Regex.Replace(
				text, "[\n\r\f\t ]+", " "
			);

			TextLength = text.Length;

			/// Хэш от строки можем посчитать, только если длина строки
			/// больше заданной константы
			if (text.Length > FuzzyHashing.MIN_TEXT_LENGTH)
				Hash = FuzzyHashing.GetFuzzyHash(text);

			if (text.Length <= MAX_TEXT_LENGTH)
				Text = text;
		}
	}

	[DataContract]
	public class PointContext
	{
		[DataMember]
		public string FileName { get; set; }

		[DataMember]
		public string NodeType { get; set; }

		/// <summary>
		/// Контекст заголовка узла, к которому привязана точка разметки
		/// </summary>
		[DataMember]
		public List<HeaderContextElement> HeaderContext { get; set; }

		/// <summary>
		/// Контекст потомков узла, к которому привязана точка разметки
		/// </summary>
		[DataMember]
		public List<InnerContextElement> InnerContext { get; set; }

		/// <summary>
		/// Внутренний контекст в виде одной сущности
		/// </summary>
		[DataMember]
		public InnerContextElement InnerContextElement { get; set; }

		/// <summary>
		/// Контекст предков узла, к которому привязана точка разметки
		/// </summary>
		[DataMember]
		public List<AncestorsContextElement> AncestorsContext { get; set; }

		/// <summary>
		/// Контекст уровня, на котором находится узел, к которому привязана точка разметки
		/// </summary>
		[DataMember]
		public SiblingsContext SiblingsContext { get; set; }

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
				var headerContext = new List<HeaderContextElement>();

				var stack = new Stack<Node>(Enumerable.Reverse(node.Children));

				while(stack.Any())
				{
					var current = stack.Pop();

					if ((current.Children.Count == 0 || current.Children.All(c=>c.Type == Grammar.CUSTOM_BLOCK_RULE_NAME)) 
						&& current.Options.Priority > 0)
						headerContext.Add((HeaderContextElement)current);
					else
					{
						if (current.Type == Grammar.CUSTOM_BLOCK_RULE_NAME)
							for (var i = current.Children.Count - 2; i >= 1; --i)
								stack.Push(current.Children[i]);
					}
				}

				return headerContext;
			}
		}

		public static List<AncestorsContextElement> GetAncestorsContext(Node node)
		{
			var context = new List<AncestorsContextElement>();
			var currentNode = node.Parent;

			while(currentNode != null)
			{			
				if(currentNode.Symbol != Grammar.CUSTOM_BLOCK_RULE_NAME 
					&& currentNode.Options.IsLand)
					context.Add((AncestorsContextElement)currentNode);

				currentNode = currentNode.Parent;
			}

			return context;
		}

		public static Tuple<List<InnerContextElement>, InnerContextElement> GetInnerContext(TargetFileInfo info)
		{
			var innerContext = new List<InnerContextElement>();
			var locations = new List<SegmentLocation>();
			var stack = new Stack<Node>(Enumerable.Reverse(info.TargetNode.Children));

			while (stack.Any())
			{
				var current = stack.Pop();

				if (current.Children.Count > 0)
				{
					if (current.Type != Grammar.CUSTOM_BLOCK_RULE_NAME)
					{
						locations.Add(current.Location);
						innerContext.Add(new InnerContextElement(current, info.FileText));
					}
					else
					{
						for (var i = current.Children.Count - 2; i >= 1; --i)
							stack.Push(current.Children[i]);
					}
				}
			}

			return new Tuple<List<InnerContextElement>, InnerContextElement>(
				innerContext, new InnerContextElement(locations, info.FileText));
		}

		public static SiblingsContext GetSiblingsContext(TargetFileInfo info)
		{
			/// Находим островного родителя
			var parentNode = info.TargetNode.Parent;
			while (parentNode != null && !parentNode.Options.IsLand)
				parentNode = parentNode.Parent;

			/// Если это корень, горизонтального контекста нет
			if (parentNode == null)
				return null;

			/// Спускаемся от родителя и собираем первые в глубину потомки-острова
			var siblings = parentNode.Children;
			for (var i = 0; i < siblings.Count; ++i)
			{
				if (!siblings[i].Options.IsLand)
				{
					var current = siblings[i];
					siblings.RemoveAt(i);
					siblings.InsertRange(i, current.Children);
				}
			}

			/// Индекс помечаемого элемента
			var markedElementIndex = siblings.IndexOf(info.TargetNode);

			return new SiblingsContext
			{
				Before = new TextOrHash(String.Join(" ", siblings
					.Take(markedElementIndex)
					.Where(n => n.Location != null)
					.Select(n => info.FileText.Substring(n.Location.Start.Offset, n.Location.Length.Value))
				)),
				After = new TextOrHash(String.Join(" ", siblings
					.Skip(markedElementIndex + 1)
					.Where(n => n.Location != null)
					.Select(n => info.FileText.Substring(n.Location.Start.Offset, n.Location.Length.Value))
				))
			};
		}

		public static PointContext Create(TargetFileInfo info)
		{
			var point = new PointContext()
			{
				FileName = info.FileName,
				NodeType = info.TargetNode.Type,
				HeaderContext = GetHeaderContext(info.TargetNode),
				AncestorsContext = GetAncestorsContext(info.TargetNode),
				SiblingsContext = GetSiblingsContext(info)
			};

			var innerContexts = GetInnerContext(info);
			point.InnerContext = innerContexts.Item1;
			point.InnerContextElement = innerContexts.Item2;

			return point;
		}
	}

	public class EqualsIgnoreValueComparer : 
		IEqualityComparer<IEqualsIgnoreValue>
	{
		public bool Equals(IEqualsIgnoreValue e1, 
			IEqualsIgnoreValue e2) => e1.EqualsIgnoreValue(e2);

		public int GetHashCode(IEqualsIgnoreValue e) 
			=> e.GetHashCode();
	}
}
