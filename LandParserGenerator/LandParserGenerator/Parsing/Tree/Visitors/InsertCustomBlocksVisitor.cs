using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Land.Core.Lexing;

namespace Land.Core.Parsing.Tree
{
	public class InsertCustomBlocksVisitor : BaseTreeVisitor
	{
		private Grammar GrammarObject { get; set; }

		public List<CustomBlockNode> CustomBlocks { get; set; }

		public Node Root { get; set; }

		/// <summary>
		/// Конструктор визитора
		/// </summary>
		/// <param name="grammar">Грамматика, в соответствии с которой построено AST</param>
		/// <param name="customBlocks">
		/// Последовательность пользовательских блоков, полученная
		/// в результате постфиксного обхода соответствующего дерева
		/// </param>
		public InsertCustomBlocksVisitor(Grammar grammar, List<CustomBlockNode> customBlocks)
		{
			GrammarObject = grammar;
			CustomBlocks = new List<CustomBlockNode>(customBlocks);
		}

		private Node GetNodeFrom(CustomBlockNode block)
		{
			CustomBlocks.Remove(block);

			var node = new Node(Grammar.CUSTOM_BLOCK_RULE_NAME, new LocalOptions { IsLand = true });

			node.AddLastChild(block.Start);
			node.AddLastChild(block.End);

			return node;
		}

		private void ProcessAny(Node node, List<CustomBlockNode> blocks)
		{
			if (blocks.Count > 0)
			{
				var level = new List<Node> { GetNodeFrom(blocks[0]) };
				var linear = new List<Node>();

				for (var i = 1; i < blocks.Count; ++i)
				{
					if(blocks[i].StartOffset > blocks[i-1].EndOffset)
						level.Add(GetNodeFrom(blocks[i]));
					else
					{
						linear.AddRange(level);

						var higherLevel = GetNodeFrom(blocks[i]);

						foreach (var blockNode in level)
							higherLevel.InsertChild(blockNode, higherLevel.Children.Count - 1);

						level.Clear();
						level.Add(higherLevel);
					}
				}

				linear.AddRange(level);

				foreach (var blockNode in linear)
					node.AddLastChild(blockNode);
			}
		}

		public override void Visit(Node root)
		{
			Root = root;

			if (root.Type == Grammar.ANY_TOKEN_NAME)
			{
				ProcessAny(root, CustomBlocks);
			}
			else
			{
				var outerBlocks = CustomBlocks.Where(b => b.Location.Includes(Root.Anchor)).ToList();

				Visit(Root, CustomBlocks.Except(outerBlocks).ToList());

				foreach (var block in outerBlocks)
				{
					var newNode = GetNodeFrom(block);
					newNode.InsertChild(Root, 1);

					Root = newNode;
				}
			}
		}

		public void Visit(Node node, List<CustomBlockNode> blocks)
		{
			if (node.Type == Grammar.ANY_TOKEN_NAME)
			{
				ProcessAny(node, blocks);
			}
			else
			{
				if (blocks.Count > 0)
				{
					/// Находим строго вложенные в потомков блоки и обрабатываем их при посещении этих потомков
					foreach (var child in node.Children.Where(c=>c.Anchor != null))
					{
						var innerBlocks = blocks.SkipWhile(b => b.StartOffset < child.Anchor.Start.Offset)
							.TakeWhile(b => child.Anchor.Includes(b.Location) && !child.Anchor.Equals(b.Location))
							.ToList();

						foreach (var block in innerBlocks)
							blocks.Remove(block);

						Visit(child, innerBlocks);
					}

					foreach (var block in blocks)
					{
						/// Выбираем потомков текущего узла, вложенных или совпадающих с некоторым пользовательским блоком
						var innerChildren = node.Children.Select((child, idx) => new { child, idx })
							.Where(p => block.Location.Includes(p.child.Anchor) || block.Location.Equals(p.child.Anchor))
							.ToList();

						if(innerChildren.Count > 0)
						{
							var leftBorder = node.Children.Take(innerChildren.First().idx)
								.LastOrDefault(c => c.Anchor != null);

							var rightBorder = node.Children.Skip(innerChildren.Last().idx + 1)
								.FirstOrDefault(c => c.Anchor != null);

							if((leftBorder == null || !leftBorder.Anchor.Overlaps(block.Location))
								&& (rightBorder == null || !rightBorder.Anchor.Overlaps(block.Location)))
							{
								var newNode = GetNodeFrom(block);
								foreach (var inner in innerChildren)
									newNode.InsertChild(inner.child, newNode.Children.Count - 1);

								node.Children.RemoveRange(innerChildren.First().idx,
									innerChildren.Last().idx - innerChildren.First().idx + 1);

								node.InsertChild(newNode, innerChildren.First().idx);
							}
						}
					}
				}
			}
		}
	}
}
