using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using Land.Core;
using Land.Core.Specification;
using Land.Core.Parsing.Tree;
using Land.Markup.CoreExtension;

namespace Land.Markup.Binding
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
				Priority = node.Options.GetPriority().Value,
				ExactMatch = node.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.EXACTMATCH)
			};
		}
	}

	[DataContract]
	public class InnerContext
	{
		[DataMember]
		public TextOrHash Content { get; set; }

		public InnerContext() { }

		public InnerContext(List<SegmentLocation> locations, string fileText)
		{
			var text = String.Join(" ", locations.Select(l => 
				fileText.Substring(l.Start.Offset, l.Length.Value)
			));

			Content = new TextOrHash(text);
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
	public class ClosestContextElement
	{
		[DataMember]
		public byte[] AncestorsHash { get; set; }

		[DataMember]
		public byte[] HeaderInnerHash { get; set; }
	}

	[DataContract]
	public class FileContext
	{
		/// <summary>
		/// Имя файла
		/// </summary>
		[DataMember]
		public string Name { get; set; }

		/// <summary>
		/// Количество строк
		/// </summary>
		[DataMember]
		public int LineCount { get; set; }

		/// <summary>
		/// Нечёткий хеш содержимого файла
		/// </summary>
		[DataMember]
		public TextOrHash Content { get; set; }
	}

	[DataContract]
	public class PointContext
	{
		/// <summary>
		/// Тип сущности, которой соответствует точка привязки
		/// </summary>
		[DataMember]
		public string Type { get; set; }

		/// <summary>
		/// Номер строки в файле, на которой начинается сущность
		/// </summary>
		[DataMember]
		public int Line { get; set; }

		/// <summary>
		/// Хеш текста, соответствующего сущности
		/// </summary>
		[DataMember]
		public byte[] HeaderInnerHash { get; set; }

		/// <summary>
		/// Контекст файла, в котором находится помеченный элемент
		/// </summary>
		[DataMember]
		public FileContext FileContext { get; set; }

		/// <summary>
		/// Контекст заголовка узла, к которому привязана точка разметки
		/// </summary>
		[DataMember]
		public List<HeaderContextElement> HeaderContext { get; set; }

		/// <summary>
		/// Внутренний контекст в виде одной сущности
		/// </summary>
		[DataMember]
		public InnerContext InnerContext { get; set; }

		[DataMember]
		public byte[] AncestorsHash { get; set; }

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

		/// <summary>
		/// Контекст наиболее похожих на помеченный элементов
		/// </summary>
		[DataMember]
		public List<ClosestContextElement> ClosestContext { get; set; }

		public static PointContext GetCoreContext(Node node, ParsedFile file)
		{
			var ancestors = GetAncestorsContexAndHash(node);

			return new PointContext
			{
				Type = node.Type,
				Line = node.Location.Start.Line.Value,
				FileContext = file.BindingContext,
				HeaderContext = GetHeaderContext(node),
				InnerContext = GetInnerContext(node, file),
				HeaderInnerHash = GetHash(node, file),
				AncestorsContext = ancestors.Item1,
				AncestorsHash = ancestors.Item2,	
			};
		}

		public static PointContext GetFullContext(
			Node node, 
			ParsedFile file, 
			List<ParsedFile> searchArea, 
			Func<string, ParsedFile> getParsed)
		{
			var context = GetCoreContext(node, file);

			if (file.MarkupSettings.UseSiblingsContext)
				context.SiblingsContext = GetSiblingsContext(node, file);

			context.ClosestContext = GetClosestContext(node, file, context, searchArea, getParsed);

			return context;
		}

		public static byte[] GetHash(Node node, ParsedFile file)
		{
			var text = file.Text.Substring(
					node.Location.Start.Offset,
					node.Location.Length.Value
				);

			return GetHash(text);
		}

		public static byte[] GetHash(string text)
		{
			/// Считаем хеш от всего текста помечаемого элемента за вычетом пробельных символов
			using (var md5 = System.Security.Cryptography.MD5.Create())
			{
				return md5.ComputeHash(Encoding.ASCII.GetBytes(
					System.Text.RegularExpressions.Regex.Replace(text.ToLower(), "[\n\r\f\t ]+", "")
				));
			}
		}

		public static List<HeaderContextElement> GetHeaderContext(Node node)
		{
			var cachingNode = (ContextCachingNode)node;

			/// Если есть закешированный контекст, возвращаем его
			//if (cachingNode.HeaderContext != null)
			//return cachingNode.HeaderContext;

			/// Иначе вычисляем контекст заголовка
			/// Листовой узел сам себе заголовок
			if (node.Value.Count > 0)
			{
				cachingNode.HeaderContext = new List<HeaderContextElement>()
				{
					new HeaderContextElement()
					{
						 Type = node.Type,
						 Priority = 1,
						 Value = node.Value
					}
				};
			}
			/// Для нелистового ищем листовых непосредственных потомков
			else
			{
				cachingNode.HeaderContext = new List<HeaderContextElement>();

				var stack = new Stack<Node>(Enumerable.Reverse(node.Children));

				while (stack.Any())
				{
					var current = stack.Pop();

					if ((current.Children.Count == 0 ||
						current.Children.All(c => c.Type == Grammar.CUSTOM_BLOCK_RULE_NAME)) &&
						current.Options.GetPriority() > 0)
					{
						cachingNode.HeaderContext.Add((HeaderContextElement)current);
					}
					else
					{
						if (current.Type == Grammar.CUSTOM_BLOCK_RULE_NAME)
							for (var i = current.Children.Count - 2; i >= 1; --i)
								stack.Push(current.Children[i]);
					}
				}
			}

			return cachingNode.HeaderContext;
		}

		public static Tuple<List<AncestorsContextElement>, byte[]> GetAncestorsContexAndHash(Node node)
		{
			var context = new List<AncestorsContextElement>();
			var currentNode = node.Parent;

			while (currentNode != null)
			{
				if (currentNode.Symbol != Grammar.CUSTOM_BLOCK_RULE_NAME
					&& currentNode.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
					context.Add((AncestorsContextElement)currentNode);

				currentNode = currentNode.Parent;
			}

			return new Tuple<List<AncestorsContextElement>, byte[]>(
				context,
				GetHash(String.Join("", context.Select(e => String.Join("", e.HeaderContext.SelectMany(h => h.Value)))))
			);
		}

		public static InnerContext GetInnerContext(Node node, ParsedFile file)
		{
			var locations = new List<SegmentLocation>();
			var stack = new Stack<Node>(Enumerable.Reverse(node.Children));

			while (stack.Any())
			{
				var current = stack.Pop();

				if (current.Children.Count > 0)
				{
					if (current.Type != Grammar.CUSTOM_BLOCK_RULE_NAME)
					{
						locations.Add(current.Location);
					}
					else
					{
						for (var i = current.Children.Count - 2; i >= 1; --i)
							stack.Push(current.Children[i]);
					}
				}
			}

			return new InnerContext(locations, file.Text);
		}

		public static SiblingsContext GetSiblingsContext(Node node, ParsedFile file)
		{
			/// Находим островного родителя
			var parentNode = node.Parent;
			while (parentNode != null && !parentNode.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
				parentNode = parentNode.Parent;

			/// Если это корень, горизонтального контекста нет
			if (parentNode == null)
				return null;

			/// Спускаемся от родителя и собираем первые в глубину потомки-острова
			var siblings = new List<Node>(parentNode.Children);
			for (var i = 0; i < siblings.Count; ++i)
			{
				if (!siblings[i].Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
				{
					var current = siblings[i];
					siblings.RemoveAt(i);
					siblings.InsertRange(i, current.Children);
				}
			}

			/// Индекс помечаемого элемента
			var markedElementIndex = siblings.IndexOf(node);
			siblings.RemoveAt(markedElementIndex);

			var context = new SiblingsContext
			{
				Before = new TextOrHash(String.Join(" ", siblings
					.Take(markedElementIndex)
					.Where(n => n.Location != null)
					.Select(n => file.Text.Substring(n.Location.Start.Offset, n.Location.Length.Value))
				)),
				After = new TextOrHash(String.Join(" ", siblings
					.Skip(markedElementIndex)
					.Where(n => n.Location != null)
					.Select(n => file.Text.Substring(n.Location.Start.Offset, n.Location.Length.Value))
				)),
			};

			return context;
		}

		public static FileContext GetFileContext(string name, string text)
		{
			return new FileContext
			{
				Name = name,
				LineCount = text.Count(c => c == '\n') + 1,
				Content = new TextOrHash(text)
			};
		}

		public static List<ClosestContextElement> GetClosestContext(
			Node node,
			ParsedFile file,
			PointContext nodeContext,
			List<ParsedFile> searchArea,
			Func<string, ParsedFile> getParsed)
		{
			const double CLOSE_ELEMENT_THRESHOLD = 0.8;

			/// Отбираем файлы, наиболее похожие на содержащий помечаемый элемент
			var similarFiles = searchArea
				.Where(f => ContextFinder.AreFilesSimilarEnough(f.BindingContext.Content, file.BindingContext.Content))
				.ToList();

			foreach (var f in similarFiles)
			{
				if (f.Root == null)
					f.Root = getParsed(f.Name)?.Root;
			};

			var candidates = new List<PointContext>();

			foreach (var similarFile in similarFiles)
			{
				/// Если не смогли распарсить файл, переходим к следующему
				if (similarFile.Root == null)
					continue;

				var visitor = new GroupNodesByTypeVisitor(new List<string> { node.Type });
				file.Root.Accept(visitor);

				/// Для каждого элемента вычисляем основные контексты
				candidates.AddRange(visitor.Grouped[node.Type].Except(new List<Node> { node })
					.Select(n => PointContext.GetCoreContext(n, similarFile))
				);
			}

			return candidates
				.Where(c => 
					ContextFinder.EvalSimilarity(c.HeaderContext, nodeContext.HeaderContext) > CLOSE_ELEMENT_THRESHOLD &&
					ContextFinder.EvalSimilarity(c.InnerContext, nodeContext.InnerContext) > CLOSE_ELEMENT_THRESHOLD
				)
				.Select(c => new ClosestContextElement
				{
					AncestorsHash = c.AncestorsHash,
					HeaderInnerHash = c.HeaderInnerHash
				})
				.ToList();
		}
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
				text.ToLower(), "[\n\r\f\t ]+", ""
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

	public class EqualsIgnoreValueComparer : 
		IEqualityComparer<IEqualsIgnoreValue>
	{
		public bool Equals(IEqualsIgnoreValue e1, 
			IEqualsIgnoreValue e2) => e1.EqualsIgnoreValue(e2);

		public int GetHashCode(IEqualsIgnoreValue e) 
			=> e.GetHashCode();
	}
}
