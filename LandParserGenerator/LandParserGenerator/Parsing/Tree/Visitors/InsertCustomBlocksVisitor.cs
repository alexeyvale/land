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

		private List<CustomBlockNode> CustomBlocks { get; set; }

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
			CustomBlocks = customBlocks;
		}

		public override void Visit(Node root)
		{
			Root = root;

			var outerBlocks = CustomBlocks.Where(b => b.Location.Includes(Root.Anchor)).ToList();

			Visit(Root, CustomBlocks.Except(outerBlocks).ToList());

			foreach(var block in outerBlocks)
			{
				var newNode = new Node(Grammar.CUSTOM_BLOCK_RULE_NAME, new LocalOptions { IsLand = true });

				newNode.AddFirstChild(block.Start);
				newNode.AddLastChild(Root);
				newNode.AddLastChild(block.End);

				Root = newNode;
			}
		}

		public void Visit(Node node, List<CustomBlockNode> blocks)
		{
			if (blocks.Count > 0)
			{
				foreach (var child in node.Children)
				{
					var innerBlocks = blocks.SkipWhile(b => b.StartOffset < child.Anchor.Start.Offset)
						.TakeWhile(b => child.Anchor.Includes(b.Location) && !child.Anchor.Equals(b.Location))
						.ToList();

					foreach (var block in innerBlocks)
						blocks.Remove(block);

					Visit(child, innerBlocks);
				}


				foreach(var block in blocks)
				{
					var innerChildren = node.Children.Select((child, idx) => new { child, idx })
						.Where(p => block.Location.Includes(p.child.Anchor) || block.Location.Equals(p.child.Anchor))
						.ToList();

					if(innerChildren.Count > 0 
						&& (innerChildren.First().idx == 0 || !node.Children[innerChildren.First().idx - 1].Anchor.Overlaps(block.Location))
						&& (innerChildren.Last().idx == node.Children.Count - 1 || !node.Children[innerChildren.Last().idx + 1].Anchor.Overlaps(block.Location)))
					{
						var newNode = new Node(Grammar.CUSTOM_BLOCK_RULE_NAME, new LocalOptions { IsLand = true });
						newNode.Parent = node;

						newNode.AddFirstChild(block.Start);
						foreach(var inner in innerChildren)
							newNode.AddLastChild(inner.child);
						newNode.AddLastChild(block.End);

						node.Children.RemoveRange(innerChildren.First().idx, innerChildren.Count);
						node.Children.Insert(innerChildren.First().idx, newNode);
					}
				}
			}
		}
	}
}
