using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using AspectCore;
using Land.Markup.Binding;
using Land.Markup.CoreExtension;
using Land.Core.Specification;
using Land.Core.Parsing.Tree;

namespace Comparison
{
	public class LandToCoreConverter: BaseTreeVisitor
	{
		public Grammar GrammarObject { get; set; }

		public string FileName { get; set; }

		public PointOfInterest Root { get; set; }
		private PointOfInterest CurrentParent { get; set; }

		public LandToCoreConverter(Grammar grammar, string fileName)
		{
			GrammarObject = grammar;
			FileName = fileName;
		}

		public override void Visit(Node node)
		{
			/// Создаём узел, хранящий актуальные текстовые координаты
			var newNode = new PointOfInterest();
			/// Коллекция потомков, которые могут попасть в новое дерево
			var children = new List<Node>(node.Children.Where(c=>c.Location != null));
			/// Заголовок узла
			List<string> value = null;

			if (node.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
			{
				/// Для LanD-узла заголовок формируется по алгоритму вычисления контекста заголовка
				if (node.Value.Count > 0)
				{
					value = new List<string>(node.Value);
				}
				else
				{
					value = new List<string>();

					var stack = new Stack<Node>(Enumerable.Reverse(node.Children));

					while (stack.Any())
					{
						var current = stack.Pop();

						if ((current.Children.Count == 0 ||
							current.Children.All(c => c.Type == Grammar.CUSTOM_BLOCK_RULE_NAME)) &&
							current.Options.GetPriority() > 0)
						{
							value.AddRange(current.Value);
						}
						else
						{
							if (current.Type == Grammar.CUSTOM_BLOCK_RULE_NAME)
							{
								for (var i = current.Children.Count - 2; i >= 1; --i)
									stack.Push(current.Children[i]);
							}
						}
					}
				}

				/// Из потомков LanD-узла убираем листья и пользовательские блоки
				for (var i = 0; i < children.Count; ++i)
				{
					if (children[i].Type == Grammar.CUSTOM_BLOCK_RULE_NAME)
					{
						var smbToRemove = children[i];
						children.RemoveAt(i);

						for (var j = smbToRemove.Children.Count - 1; j >= 0; --j)
						{
							children.Insert(i, smbToRemove.Children[j]);
						}

						--i;
					}
					else if(children[i].Value?.Count > 0 && children[i].Options.GetPriority() > 0)
					{
						children.RemoveAt(i);
						--i;
					}
				}
			}

			newNode.Context.Add(new OuterContextNode(value, node.Type));
			if (value != null && value.Count != 0)
			{
				newNode.Title = string.Join(" ", value);
			}
			newNode.FileName = FileName;

			if (CurrentParent != null)
			{
				CurrentParent.Items.Add(newNode);
			}
			else
			{
				Root = newNode;
			}

			var oldParent = CurrentParent;
			CurrentParent = newNode;

			foreach (var child in children)
			{
				Visit(child);
			}

			var lastItemLocation = newNode.Items.LastOrDefault()?.Location;

			newNode.Location = node.Location != null ?
				new QUT.Gppg.LexLocation(
					node.Location.Start.Line ?? 0,
					node.Location.Start.Column ?? 0,
					lastItemLocation?.EndLine ?? 
						lastItemLocation?.StartLine ?? node.Location.Start.Line ?? 0,
					lastItemLocation?.EndColumn ?? 
						lastItemLocation?.StartColumn ?? node.Location.Start.Column ?? 0
				) : null;

			CurrentParent = oldParent;
		}
	}
}
