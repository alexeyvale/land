using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
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

	public abstract class TypedPrioritizedContextElement
	{
		public double Priority { get; set; }

		public string Type { get; set; }
	}

	public class HeaderContextElement: TypedPrioritizedContextElement, IEqualsIgnoreValue
	{
		public bool ExactMatch { get; set; }

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

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = 1685606927;
				hashCode = hashCode * -1521134295 + ExactMatch.GetHashCode();
				hashCode = hashCode * -1521134295 + Priority.GetHashCode();
				hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Type);

				foreach (var elem in Value)
				{
					hashCode = hashCode * -1521134295 +
						EqualityComparer<string>.Default.GetHashCode(elem);
				}

				return hashCode;
			}
		}

		public static bool operator ==(HeaderContextElement a, HeaderContextElement b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(HeaderContextElement a, HeaderContextElement b)
		{
			return !a.Equals(b);
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

	public class InnerContext
	{
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

	public class AncestorsContextElement
	{
		public string Type { get; set; }

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

	#region Old

	public class ContextElement
	{
		public string Type { get; set; }

		public List<HeaderContextElement> HeaderContext { get; set; }

		public override bool Equals(object obj)
		{
			if (obj is ContextElement elem)
			{
				return ReferenceEquals(this, elem) || Type == elem.Type
					&& HeaderContext.SequenceEqual(elem.HeaderContext);
			}

			return false;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = 1660800360;
				hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Type);

				foreach(var elem in HeaderContext)
				{
					hashCode = hashCode * -1521134295 + 
						EqualityComparer<HeaderContextElement>.Default.GetHashCode(elem);
				}

				return hashCode;
			}
		}

		public static bool operator ==(ContextElement a, ContextElement b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(ContextElement a, ContextElement b)
		{
			return !a.Equals(b);
		}

		public static explicit operator ContextElement(Node node)
		{
			return new ContextElement()
			{
				Type = node.Type,
				HeaderContext = PointContext.GetHeaderContext(node)
			};
		}
	}

	#endregion

	public class SiblingsContext
	{
		public TextOrHash Before { get; set; }
		public TextOrHash After { get; set; }
	}

	public class FileContext
	{
		public HashSet<Guid> LinkedPoints { get; set; } = new HashSet<Guid>();

		/// <summary>
		/// Имя файла
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Количество строк
		/// </summary>
		public int LineCount { get; set; }

		/// <summary>
		/// Нечёткий хеш содержимого файла
		/// </summary>
		public TextOrHash Content { get; set; }
	}

	public class PointContext
	{
		public HashSet<Guid> LinkedPoints { get; private set; } = 
			new HashSet<Guid>();

		public HashSet<Tuple<Guid, int>> LinkedClosestPoints { get; private set; } = 
			new HashSet<Tuple<Guid, int>>();

		public void LinkPoint(Guid pointId)
		{
			this.LinkedPoints.Add(pointId);

			/// Проверки на != null нужны для десериализации

			for (var i = 0; i < this.ClosestContext.Count; ++i)
			{
				if (this.ClosestContext[i] != null)
				{
					this.ClosestContext[i].LinkedClosestPoints
						.Add(new Tuple<Guid, int>(pointId, i));
				}
			}

			if (this.FileContext != null)
			{
				this.FileContext.LinkedPoints.Add(pointId);
			}
		}

		/// <summary>
		/// Тип сущности, которой соответствует точка привязки
		/// </summary>
		public string Type { get; set; }

		/// <summary>
		/// Номер строки в файле, на которой начинается сущность
		/// </summary>
		public int Line { get; set; }

		private FileContext _fileContext;

		/// <summary>
		/// Контекст файла, в котором находится помеченный элемент
		/// </summary>
		[JsonIgnore]
		public FileContext FileContext
		{
			get { return _fileContext; }

			set
			{
				_fileContext = value;
				_fileContext.LinkedPoints.UnionWith(LinkedPoints);
			}
		}

		/// <summary>
		/// Контекст заголовка узла, к которому привязана точка разметки
		/// </summary>
		public List<HeaderContextElement> HeaderContext { get; set; }

		/// <summary>
		/// Внутренний контекст в виде одной сущности
		/// </summary>
		public InnerContext InnerContext { get; set; }

		/// <summary>
		/// Контекст предков узла, к которому привязана точка разметки
		/// </summary>
		public List<AncestorsContextElement> AncestorsContext { get; set; }

		/// <summary>
		/// Контекст уровня, на котором находится узел, к которому привязана точка разметки
		/// </summary>
		public SiblingsContext SiblingsContext { get; set; }

		private List<PointContext> _closestContext;

		/// <summary>
		/// Контекст наиболее похожих на помеченный элементов
		/// </summary>
		[JsonIgnore]
		public List<PointContext> ClosestContext
		{
			get { return _closestContext; }

			set
			{
				_closestContext = value;

				for (var i = 0; i < this.ClosestContext.Count; ++i)
				{
					ClosestContext[i].LinkedClosestPoints
						.UnionWith(LinkedPoints.Select(id => new Tuple<Guid, int>(id, i)));
				}
			}
		}

		#region Old

		public string Name =>
			String.Join("", this.HeaderContext.Where(e => e.Type == "name").SelectMany(e => e.Value));

		public List<ContextElement> InnerContext_old { get; set; }

		#endregion

		public static PointContext GetCoreContext(
			Node node,
			ParsedFile file)
		{
			return new PointContext
			{
				Type = node.Type,
				Line = node.Location.Start.Line.Value,
				FileContext = file.BindingContext,
				HeaderContext = GetHeaderContext(node),
				InnerContext = GetInnerContext(node, file),
				AncestorsContext = GetAncestorsContext(node),
				#region Old
				InnerContext_old = GetInnerContext_old(node, file)
				#endregion
			};
		}

		public static PointContext GetExtendedContext(
			Node node, 
			ParsedFile file, 
			SiblingsConstructionArgs siblingsArgs,
			ClosestConstructionArgs closestArgs,
			PointContext core = null)
		{
			if (core == null)
			{
				core = PointContext.GetCoreContext(node, file);
			}

			if (siblingsArgs !=null && core.SiblingsContext == null)
			{
				core.SiblingsContext = GetSiblingsContext(node, file);
			}
			if (closestArgs != null && core.SiblingsContext == null)
			{
				core.ClosestContext = GetClosestContext(
					node, file, core, 
					closestArgs.SearchArea, closestArgs.GetParsed, closestArgs.ContextFinder
				);
			}

			return core;
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
			var result = new List<HeaderContextElement>();

			/// Иначе вычисляем контекст заголовка
			/// Листовой узел сам себе заголовок
			if (node.Value.Count > 0)
			{
				result = new List<HeaderContextElement>()
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
				result = new List<HeaderContextElement>();

				var stack = new Stack<Node>(Enumerable.Reverse(node.Children));

				while (stack.Any())
				{
					var current = stack.Pop();

					if ((current.Children.Count == 0 ||
						current.Children.All(c => c.Type == Grammar.CUSTOM_BLOCK_RULE_NAME)) &&
						current.Options.GetPriority() > 0)
					{
						result.Add((HeaderContextElement)current);
					}
					else
					{
						if (current.Type == Grammar.CUSTOM_BLOCK_RULE_NAME)
							for (var i = current.Children.Count - 2; i >= 1; --i)
								stack.Push(current.Children[i]);
					}
				}
			}

			return result;
		}

		public static List<AncestorsContextElement> GetAncestorsContext(Node node)
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

			return context;
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

		#region Old

		public static List<ContextElement> GetInnerContext_old(Node node, ParsedFile file)
		{
			var result = new List<ContextElement>();
			var stack = new Stack<Node>(Enumerable.Reverse(node.Children));

			while (stack.Any())
			{
				var current = stack.Pop();

				if (current.Children.Count > 0)
				{
					if (current.Type != Grammar.CUSTOM_BLOCK_RULE_NAME)
					{
						result.Add((ContextElement)current);
					}
					else
					{
						for (var i = current.Children.Count - 2; i >= 1; --i)
							stack.Push(current.Children[i]);
					}
				}
			}

			return result;
		}

		#endregion

		public static FileContext GetFileContext(string name, string text)
		{
			return new FileContext
			{
				Name = name,
				LineCount = text.Count(c => c == '\n') + 1,
				Content = new TextOrHash(text)
			};
		}

		public static SiblingsContext GetSiblingsContext(
			Node node, 
			ParsedFile file)
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

				//EntityBefore = markedElementIndex > 0
				//	? contextFinder.ContextManager.GetContext(siblings[markedElementIndex - 1], file) : null,
				//EntityAfter = markedElementIndex < siblings.Count
				//	? contextFinder.ContextManager.GetContext(siblings[markedElementIndex], file) : null,
			};

			return context;
		}

		public static List<PointContext> GetClosestContext(
			Node node,
			ParsedFile file,
			PointContext nodeContext,
			List<ParsedFile> searchArea,
			Func<string, ParsedFile> getParsed,
			ContextFinder contextFinder)
		{
			const double CLOSE_ELEMENT_HEADER_THRESHOLD = 0.5;
			const double CLOSE_ELEMENT_INNER_THRESHOLD = 0.8;
			const int MAX_COUNT = 10;

			var candidates = new List<RemapCandidateInfo>();

			foreach (var f in searchArea)
			{
				if (f.Root == null)
				{
					f.Root = getParsed(f.Name)?.Root;
				}

				var visitor = new GroupNodesByTypeVisitor(new List<string> { node.Type });
				file.Root.Accept(visitor);

				candidates.AddRange(visitor.Grouped[node.Type].Except(new List<Node> { node })
					.Select(n => new RemapCandidateInfo
					{
						Context = contextFinder.ContextManager.GetContext(n, file)
					})
				);
			};

			contextFinder.ComputeCoreSimilarities(nodeContext, candidates);

			candidates = nodeContext.HeaderContext.Count > 0
				? candidates
					.OrderByDescending(c => c.HeaderSimilarity)
					.Take(MAX_COUNT)
					.TakeWhile(c => c.HeaderSimilarity >= CLOSE_ELEMENT_HEADER_THRESHOLD)
					.ToList()
				: candidates
					.OrderByDescending(c => c.InnerSimilarity)
					.Take(MAX_COUNT)
					.TakeWhile(c => c.InnerSimilarity >= CLOSE_ELEMENT_INNER_THRESHOLD)
					.ToList();

			return candidates.Select(c=>c.Context).ToList();
		}
	}

	public class TextOrHash
	{
		public const int MAX_TEXT_LENGTH = 100;

		public string Text { get; set; }

		public int TextLength { get; set; }

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

	public class SiblingsConstructionArgs { }

	public class ClosestConstructionArgs
	{
		public List<ParsedFile> SearchArea { get; set; }
		public Func<string, ParsedFile> GetParsed { get; set; }
		public ContextFinder ContextFinder { get; set; }
	}
}
