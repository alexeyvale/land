using Land.Core;
using Land.Core.Parsing.Tree;
using Land.Core.Specification;
using Land.Markup.CoreExtension;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Land.Markup.Binding
{
	public interface IEqualsIgnoreValue
	{
		bool EqualsIgnoreValue(object obj);

		int GetHashCodeIgnoreValue();
	}

	public class PrioritizedWord
	{
		public double Priority { get; set; }
		public string Text { get; set; }

		public override bool Equals(object obj)
		{
			if (obj is PrioritizedWord elem)
			{
				return ReferenceEquals(this, elem) || Priority == elem.Priority
					&& Text == elem.Text;
			}

			return false;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = 1685606927;
				hashCode = hashCode * -1521134295 + Priority.GetHashCode();
				hashCode = hashCode * -1521134295 + Text.GetHashCode();

				return hashCode;
			}
		}
	}

	public abstract class TypedPrioritizedContextElement
	{
		public double Priority { get; set; }

		public string Type { get; set; }
	}

	public class HeaderContextElement: TypedPrioritizedContextElement, IEqualsIgnoreValue
	{
		public bool ExactMatch { get; set; }

		public List<PrioritizedWord> Value { get; set; }

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
					hashCode = hashCode * -1521134295 
						+ EqualityComparer<PrioritizedWord>.Default.GetHashCode(elem);
				}

				return hashCode;
			}
		}

		public int GetHashCodeIgnoreValue()
		{
			unchecked
			{
				var hashCode = 1685606927;
				hashCode = hashCode * -1521134295 + ExactMatch.GetHashCode();
				hashCode = hashCode * -1521134295 + Priority.GetHashCode();
				hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Type);

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
			var isExactMatch = node.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.EXACTMATCH);

			return new HeaderContextElement()
			{
				Type = node.Type,
				Value = isExactMatch
					? new List<PrioritizedWord> { new PrioritizedWord { Text = String.Join("", node.Value), Priority = 1 } }
					: node.Value.SelectMany(e=>GetWords(e)).ToList(),
				Priority = node.Options.GetPriority().Value,
				ExactMatch = isExactMatch
			};
		}

		public static List<PrioritizedWord> GetWords(string str)
		{
			var result = new List<PrioritizedWord>();
			var splitted = str.Split(
				new char[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries
			);

			foreach (var part in splitted)
			{
				var firstIdx = 0;

				for (var i = 1; i < part.Length; ++i)
				{
					if (Char.IsLower(part[i - 1]) && Char.IsUpper(part[i])
						|| Char.IsUpper(part[i - 1]) && Char.IsUpper(part[i]) && i < part.Length - 1 && Char.IsLower(part[i + 1])
						|| Char.IsLetterOrDigit(part[i - 1]) != Char.IsLetterOrDigit(part[i])
						|| Char.IsDigit(part[i - 1]) != Char.IsDigit(part[i]))
					{
						var word = part.Substring(firstIdx, i - firstIdx);
						result.Add(new PrioritizedWord
						{
							Text = word,
							Priority = word.All(c => char.IsLetterOrDigit(c)) ? 1 : 0.1
						});

						firstIdx = i;
					}
				}

				var lastWord = part.Substring(firstIdx);
				result.Add(new PrioritizedWord
				{
					Text = lastWord,
					Priority = lastWord.All(c=>char.IsLetterOrDigit(c)) ? 1 : 0.1
				});
			}

			return result;
		}
	}

	public class AncestorsContextElement
	{
		public string Type { get; set; }

		public HeaderContext HeaderContext { get; set; }

		public override bool Equals(object obj)
		{
			if (obj is AncestorsContextElement elem)
			{
				return ReferenceEquals(this, elem) || Type == elem.Type
					&& HeaderContext.Equals(elem.HeaderContext);
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

	public class InnerContext
	{
		public TextOrHash Content { get; set; }

		public InnerContext() 
		{ 
			Content = new TextOrHash(); 
		}

		public InnerContext(List<SegmentLocation> locations, string fileText)
		{
			var text = String.Join(" ", locations.Select(l => 
				fileText.Substring(l.Start.Offset, l.Length.Value)
			));

			Content = new TextOrHash(text);
		}
	}

	public class HeaderContext
	{
		public List<HeaderContextElement> Sequence { get; set; }
		public List<int> NonCoreIndices { get; set; }
		public List<int> CoreIndices { get; set; }

		[JsonIgnore]
		public List<HeaderContextElement> NonCore =>
			NonCoreIndices.Select(i => Sequence[i]).ToList();

		[JsonIgnore]
		public List<HeaderContextElement> Core =>
			CoreIndices.Select(i => Sequence[i]).ToList();

		#region Old

		public List<string> Sequence_old { get; set; }

		public string Core_old =>
			String.Join("", Core.Select(e => String.Join("", e.Value.Select(word => word.Text))).ToList());

		#endregion

		public override bool Equals(object obj)
		{
			if (obj is HeaderContext elem)
			{
				return ReferenceEquals(this, elem) 
					|| Sequence.SequenceEqual(elem.Sequence);
			}

			return false;
		}

		public bool EqualsByCore(object obj)
		{
			if (obj is HeaderContext elem)
			{
				return ReferenceEquals(this, elem) ||
					Core.SequenceEqual(elem.Core);
			}

			return false;
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}
	}

	#region Old

	public class ContextElement
	{
		public string Type { get; set; }

		public List<string> HeaderContext { get; set; }

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
						EqualityComparer<string>.Default.GetHashCode(elem);
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
				HeaderContext = PointContext.GetHeaderContext(node).Sequence.SelectMany(e=>e.Value.Select(valElem => valElem.Text)).ToList()
			};
		}
	}

	#endregion

	public class SiblingsContext
	{
		public SiblingsContextPart Before { get; set; }
		public SiblingsContextPart After { get; set; }
	}

	public class SiblingsContextPart
	{
		public TextOrHash GlobalHash { get; set; }
		public byte[] EntityMd5 { get; set; }
		public string EntityType { get; set; }

		[JsonIgnore]
		public bool IsNotEmpty => GlobalHash.TextLength > 0;
	}

	public class AncestorSiblingsPair
	{
		public Node Ancestor { get; set; }
		public List<Node> Siblings { get; set; }
	}

	public class FileContext
	{
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
		/// <summary>
		/// Идентификаторы связанных точек привязки
		/// </summary>
		public HashSet<Guid> LinkedPoints { get; private set; } = 
			new HashSet<Guid>();

		/// <summary>
		/// Идентификаторы точек привязки, 
		/// для которых данный контекст описывает ближайшую сущность
		/// </summary>
		public HashSet<Tuple<Guid, int>> LinkedClosestPoints { get; private set; } = 
			new HashSet<Tuple<Guid, int>>();

		public void LinkPoint(Guid pointId)
		{
			this.LinkedPoints.Add(pointId);

			for (var i = 0; i < this.ClosestContext?.Count; ++i)
			{
				if (this.ClosestContext[i] != null)
				{
					this.ClosestContext[i].LinkedClosestPoints
						.Add(new Tuple<Guid, int>(pointId, i));
				}
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
				FileName = _fileContext?.Name;
			}
		}

		public string FileName { get; set; }

		/// <summary>
		/// Контекст заголовка узла, к которому привязана точка разметки
		/// </summary>
		public HeaderContext HeaderContext { get; set; }

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

				for (var i = 0; i < this.ClosestContext?.Count; ++i)
				{
					ClosestContext[i].LinkedClosestPoints
						.UnionWith(LinkedPoints.Select(id => new Tuple<Guid, int>(id, i)));
				}
			}
		}

		#region Old

		public List<ContextElement> InnerContext_old { get; set; }

		public List<ContextElement> SiblingsLeftContext_old { get; set; }

		public List<ContextElement> SiblingsRightContext_old { get; set; }

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
				core.SiblingsContext = GetSiblingsContext(node, file, siblingsArgs.Range);

				#region Old
				var oldSiblingsContext = GetSiblingsContext_old(node, file);

				core.SiblingsLeftContext_old = oldSiblingsContext.Item1;
				core.SiblingsRightContext_old = oldSiblingsContext.Item2;
				#endregion old
			}
			if (closestArgs != null && core.ClosestContext == null)
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

		public static Node GetAncestor(Node node)
		{
			var currentNode = node.Parent;

			while (currentNode != null)
			{
				if (currentNode.Symbol != Grammar.CUSTOM_BLOCK_RULE_NAME
					&& currentNode.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
					return currentNode;

				currentNode = currentNode.Parent;
			}

			return currentNode;
		}

		public static HeaderContext GetHeaderContext(Node node)
		{
			var headerCoreTypes = new HashSet<string>(node.Options.GetHeaderCore());
			List<Node> sequence;

			if (node.Value.Count > 0)
			{
				sequence = new List<Node>() { node };
			}
			else
			{
				sequence = new List<Node>();

				var stack = new Stack<Node>(Enumerable.Reverse(node.Children));

				while (stack.Any())
				{
					var current = stack.Pop();

					if ((current.Children.Count == 0 ||
						current.Children.All(c => c.Type == Grammar.CUSTOM_BLOCK_RULE_NAME)) &&
						current.Options.GetPriority() > 0)
					{
						sequence.Add(current);
					}
					else
					{
						/// TODO Вспомнить, почему тут так написано, и написать комментарий
						if (current.Type == Grammar.CUSTOM_BLOCK_RULE_NAME)
						{
							for (var i = current.Children.Count - 2; i >= 1; --i)
							{
								stack.Push(current.Children[i]);
							}
						}
					}
				}
			}

			var headerSequence = sequence.Select(e => (HeaderContextElement)e).ToList();
			var grouped = sequence
				.Select((e, i) => new { elem = e, idx = i })
				.GroupBy(e => headerCoreTypes.Contains(e.elem.Symbol) || headerCoreTypes.Contains(e.elem.Alias))
				.ToDictionary(g => g.Key, g => g.ToList());

			return new HeaderContext
			{
				Sequence = headerSequence,
				NonCoreIndices = grouped.ContainsKey(false)
					? grouped[false].Select(e => e.idx).ToList()
					: new List<int>(),
				CoreIndices = grouped.ContainsKey(true)
					? grouped[true].Select(e => e.idx).ToList()
					: new List<int>(),
				Sequence_old = sequence.SelectMany(e=>e.Value).ToList(),
			};
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
						if (current.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
						{
							result.Add((ContextElement)current);
						}
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

		public static Tuple<List<ContextElement>, List<ContextElement>> GetSiblingsContext_old(
			Node node,
			ParsedFile file,
			AncestorSiblingsPair pair = null)
		{
			Node parentNode = null;
			List<Node> siblings = null;

			if (pair?.Ancestor != null)
			{
				parentNode = pair.Ancestor;
				goto SkipParentSearch;
			}

			/// Находим островного родителя
			parentNode = node.Parent;
			while (parentNode != null
				&& !parentNode.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
			{
				parentNode = parentNode.Parent;
			}

			/// Если при подъёме дошли до неостровного корня, 
			/// и сам элемент не является этим корнем
			if (parentNode == null)
			{
				if (node != file.Root)
				{
					parentNode = file.Root;
				}
				else
				{
					return new Tuple<List<ContextElement>, List<ContextElement>>(
						new List<ContextElement>(),
						new List<ContextElement>()
					);
				}
			}

			if (pair != null)
			{
				pair.Ancestor = parentNode;
			}

		SkipParentSearch:

			if (pair?.Siblings != null)
			{
				siblings = pair.Siblings.ToList();
				goto SkipSiblingsSearch;
			}

			/// Спускаемся от родителя и собираем первые в глубину потомки-острова
			siblings = new List<Node>(parentNode.Children);
			for (var i = 0; i < siblings.Count; ++i)
			{
				if (!siblings[i].Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
				{
					var current = siblings[i];
					siblings.RemoveAt(i);
					siblings.InsertRange(i, current.Children);

					--i;
				}
			}

			if (pair != null)
			{
				pair.Siblings = siblings.ToList();
			}

		SkipSiblingsSearch:

			/// Индекс помечаемого элемента
			var markedElementIndex = siblings.IndexOf(node);
			siblings.RemoveAt(markedElementIndex);

			var beforeSiblings = siblings
				.Take(markedElementIndex)
				.Where(n => n.Location != null)
				.ToList();
			var afterSiblings = siblings
				.Skip(markedElementIndex)
				.Where(n => n.Location != null)
				.ToList();

			return new Tuple<List<ContextElement>, List<ContextElement>>(
					beforeSiblings.Select(e => new ContextElement
					{
						Type = e.Type,
						HeaderContext = PointContext.GetHeaderContext(e).Sequence
							.SelectMany(el => el.Value.Select(valElem => valElem.Text)).ToList()
					}).ToList(),
					afterSiblings.Select(e => new ContextElement
					{
						Type = e.Type,
						HeaderContext = PointContext.GetHeaderContext(e).Sequence
							.SelectMany(el => el.Value.Select(valElem => valElem.Text)).ToList()
					}).ToList()
				);
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
			ParsedFile file,
			SiblingsConstructionArgs.SiblingsRange range,
			AncestorSiblingsPair pair = null)
		{
			Node parentNode = null;
			List<Node> siblings = null;

			if (pair?.Ancestor != null)
			{
				parentNode = pair.Ancestor;
				goto SkipParentSearch;
			}
				
			/// Находим островного родителя
			parentNode = node.Parent;
			while (parentNode != null 
				&& !parentNode.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
			{
				parentNode = parentNode.Parent;
			}

			/// Если при подъёме дошли до неостровного корня, 
			/// и сам элемент не является этим корнем
			if (parentNode == null)
			{ 
				if (node != file.Root)
				{
					parentNode = file.Root;
				}
				else
				{
					return new SiblingsContext
					{
						After = new SiblingsContextPart { GlobalHash = new TextOrHash() },
						Before = new SiblingsContextPart { GlobalHash = new TextOrHash() }
					};
				}
			}

			if (pair != null)
			{
				pair.Ancestor = parentNode;
			}

		SkipParentSearch:

			if(pair?.Siblings != null)
			{
				siblings = pair.Siblings.ToList();
				goto SkipSiblingsSearch;
			}

			/// Спускаемся от родителя и собираем первые в глубину потомки-острова
			siblings = new List<Node>(parentNode.Children);
			for (var i = 0; i < siblings.Count; ++i)
			{
				if (!siblings[i].Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
				{
					var current = siblings[i];
					siblings.RemoveAt(i);
					siblings.InsertRange(i, current.Children);

					--i;
				}
			}

			if(pair != null)
			{
				pair.Siblings = siblings.ToList();
			}

		SkipSiblingsSearch:

			/// Индекс помечаемого элемента
			var markedElementIndex = siblings.IndexOf(node);
			siblings.RemoveAt(markedElementIndex);

			var beforeBuilder = new StringBuilder();
			var beforeSiblings = range == SiblingsConstructionArgs.SiblingsRange.All
				? siblings
					.Take(markedElementIndex)
					.Where(n => n.Location != null)
					.ToList()
				: markedElementIndex > 0 
					? new List<Node> { siblings[markedElementIndex - 1] }
					: new List<Node>();

			foreach (var part in beforeSiblings
					.Select(n => file.Text.Substring(n.Location.Start.Offset, n.Location.Length.Value)))
			{
				beforeBuilder.Append(part);
			}
			
			var afterBuilder = new StringBuilder();
			var afterSiblings = range == SiblingsConstructionArgs.SiblingsRange.All
				? siblings
					.Skip(markedElementIndex)
					.Where(n => n.Location != null)
					.ToList()
				: markedElementIndex < siblings.Count
					? new List<Node> { siblings[markedElementIndex] }
					: new List<Node>();

			foreach (var part in afterSiblings
					.Select(n => file.Text.Substring(n.Location.Start.Offset, n.Location.Length.Value)))
			{
				afterBuilder.Append(part);
			}

			var context = new SiblingsContext
			{
				Before = new SiblingsContextPart {
					GlobalHash = new TextOrHash(beforeBuilder.ToString()),
					EntityMd5 = markedElementIndex > 0 
						? GetHash(siblings[markedElementIndex - 1], file) : null,
					EntityType = markedElementIndex > 0 
						? siblings[markedElementIndex - 1].Type : null
				},

				After = new SiblingsContextPart
				{
					GlobalHash = new TextOrHash(afterBuilder.ToString()),
					EntityMd5 = markedElementIndex < siblings.Count 
						? GetHash(siblings[markedElementIndex], file) : null,
					EntityType = markedElementIndex < siblings.Count 
						? siblings[markedElementIndex].Type : null
				}
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
			const double CLOSE_ELEMENT_THRESHOLD = 0.6;
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

			contextFinder.ComputeCoreContextSimilarities(nodeContext, candidates);
			contextFinder.ComputeTotalSimilarities(nodeContext, candidates);

			candidates = candidates
				.OrderByDescending(c => c.Similarity)
				.ThenByDescending(c => c.AncestorSimilarity)
				.Take(MAX_COUNT)
				.TakeWhile(c => c.Similarity >= CLOSE_ELEMENT_THRESHOLD)
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
			{
				Hash = FuzzyHashing.GetFuzzyHash(text);
			}

			if (text.Length <= MAX_TEXT_LENGTH)
			{
				Text = text;
			}
		}
	}

	public class EqualsIgnoreValueComparer :
		IEqualityComparer<IEqualsIgnoreValue>
	{
		public bool Equals(IEqualsIgnoreValue e1,
			IEqualsIgnoreValue e2) => e1.EqualsIgnoreValue(e2);

		public int GetHashCode(IEqualsIgnoreValue e)
			=> e.GetHashCodeIgnoreValue();
	}

	public class SiblingsConstructionArgs 
	{ 
		public enum SiblingsRange
		{
			All,
			Nearest
		}

		public SiblingsRange Range { get; set; }
	}

	public class ClosestConstructionArgs
	{
		public List<ParsedFile> SearchArea { get; set; }
		public Func<string, ParsedFile> GetParsed { get; set; }
		public ContextFinder ContextFinder { get; set; }
	}
}
