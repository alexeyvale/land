using System;
using System.Collections.Generic;
using System.Linq;
using Land.Core.Specification;
using Land.Core.Lexing;

namespace Land.Core.Parsing.Tree
{
	public class InsertCustomBlocksVisitor : BaseTreeVisitor
	{
		private BaseNodeGenerator NodeGenerator { get; set; }

		private Grammar GrammarObject { get; set; }

		private List<CustomBlockNode> InitialBlocks { get; set; }

		public List<CustomBlockNode> BadBlocks { get; set; } = new List<CustomBlockNode>();

		public Node Root { get; set; }

		/// <summary>
		/// Конструктор визитора
		/// </summary>
		/// <param name="grammar">Грамматика, в соответствии с которой построено AST</param>
		/// <param name="customBlocks">
		/// Последовательность пользовательских блоков, полученная
		/// в результате постфиксного обхода соответствующего дерева
		/// </param>
		public InsertCustomBlocksVisitor(
			Grammar grammar, 
			BaseNodeGenerator nodeGenerator,
			List<CustomBlockNode> customBlocks)
		{
			GrammarObject = grammar;
			NodeGenerator = nodeGenerator;
			InitialBlocks = new List<CustomBlockNode>(customBlocks);
		}

		private Node GetNodeFrom(CustomBlockNode block)
		{
			var node = NodeGenerator.Generate(Grammar.CUSTOM_BLOCK_RULE_NAME);

			node.AddLastChild(block.Start);
			node.AddLastChild(block.End);

			return node;
		}

		public override void Visit(Node root)
		{
			/// Не будем менять список, полученный извне
			var blocks = InitialBlocks.ToList();

			/// Проверяем, есть ли ПБ, охватывающий корень
			var outerBlock = blocks.FirstOrDefault(b => b.Location.Includes(root.Location));

			/// Пока такой ПБ есть
			while (outerBlock != null)
			{
				var newNode = GetNodeFrom(outerBlock);

				if(root.Parent != null)
				{
					root.Parent.ReplaceChild(newNode, 1);
				}
				newNode.InsertChild(root, 1);

				var index = blocks.IndexOf(outerBlock);
				blocks.RemoveAt(index);
				blocks.InsertRange(index, outerBlock.Children);

				outerBlock = blocks.FirstOrDefault(b => b.Location.Includes(root.Location));
			}

			Root = root;
			while(Root.Parent != null)
			{
				Root = Root.Parent;
			}

			Visit(root, blocks);
		}

		public void Visit(Node node, List<CustomBlockNode> blocks)
		{
			while (blocks.Count > 0)
			{
				foreach (var block in blocks.ToList())
				{
					/// Выбираем потомков текущего узла, пересекающихся с блоком или объемлющих его, но не совпадающих с ним
					var overlappingOrOuterChildren = node.Children
						.Where(p => p.Location != null && (p.Location.Overlaps(block.Location) 
							|| p.Location.Includes(block.Location) && !p.Location.Equals(block.Location)))
						.ToList();

					if (overlappingOrOuterChildren.Count == 0)
					{
						/// Выбираем потомков текущего узла, вложенных или совпадающих с некоторым пользовательским блоком
						var innerChildren = node.Children.Select((child, idx) => new { child, idx })
							.Where(p => block.Location.Includes(p.child.Location))
							.ToList();

						if (innerChildren.Count > 0)
						{
							var newNode = GetNodeFrom(block);
							foreach (var inner in innerChildren)
								newNode.InsertChild(inner.child, newNode.Children.Count - 1);

							node.Children.RemoveRange(innerChildren.First().idx,
								innerChildren.Last().idx - innerChildren.First().idx + 1);

							node.InsertChild(newNode, innerChildren.First().idx);
						}
						else
						{
							var insertionIndex = node.Children
								.Select((child, idx) => new { child, idx })
								.LastOrDefault(pair => pair.child.Location?.Start.Offset > block.EndOffset)?.idx;
							var newNode = GetNodeFrom(block);

							if (insertionIndex.HasValue)
							{
								node.InsertChild(newNode, insertionIndex.Value);
							}
							else
							{
								node.AddLastChild(newNode);
							}
						}

						var index = blocks.IndexOf(block);
						blocks.RemoveAt(index);
						blocks.InsertRange(index, block.Children);
					}
				}

				var locatedNodes = node.Children.Where(c => c.Location != null).ToList();

				/// Находим вложенные в потомков блоки и блоки, перекрывающиеся ровно с одним потомком,
				/// обрабатываем их при рекурсивных посещениях
				for (var i = 0; i < locatedNodes.Count; ++i)
				{
					var innerBlocks = blocks
						.Where(b => locatedNodes[i].Location.Overlaps(b.Location)
							&& (i == locatedNodes.Count - 1 || !b.Location.Overlaps(locatedNodes[i + 1].Location) && !b.Location.Includes(locatedNodes[i + 1].Location))
							&& (i == 0 || !b.Location.Overlaps(locatedNodes[i - 1].Location) && !b.Location.Includes(locatedNodes[i - 1].Location))
							|| locatedNodes[i].Location.Includes(b.Location) && !locatedNodes[i].Location.Equals(b.Location))
						.ToList();

					foreach (var block in innerBlocks)
						blocks.Remove(block);

					Visit(locatedNodes[i], innerBlocks);
				}

				BadBlocks.AddRange(blocks);
				blocks = blocks.SelectMany(b => b.Children).ToList();
			}
		}
	}
}
