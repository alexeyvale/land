using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;

using LandParserGenerator.Parsing.Tree;

namespace TestGUI
{
    public class TreeViewAdapter : TreeViewItem
    {
        public Node Source { get; private set; }

		public static explicit operator TreeViewAdapter(Node node)
		{
			var treeItem = new TreeViewAdapter();
			treeItem.Source = node;
			treeItem.Header = node.Symbol;
			var items = new List<TreeViewItem>();
			treeItem.ItemsSource = items;

			foreach (var nd in node.Children)
				items.Add((TreeViewAdapter)nd);

			return treeItem;

		}

		/*
		public static explicit operator TreeViewAdapter(Node node)
        {
            var treeItem = new TreeViewAdapter();
            treeItem.Source = node;
            treeItem.Header = node.ToString();
            var items = new List<TreeViewItem>();
            treeItem.ItemsSource = items;

            if (node is ContainerNode)
            {
                foreach (var nd in (node as ContainerNode).Items)
                    items.Add((TreeViewAdapter)nd);
            }
            else if (node is BinaryOpNode)
            {
                var bop = (BinaryOpNode)node;
                items.Add((TreeViewAdapter)bop.Left);
                items.Add((TreeViewAdapter)bop.Op);
                items.Add((TreeViewAdapter)bop.Right);
            }
            else if (node is UnaryOpNode)
            {
                var uop = (UnaryOpNode)node;
                items.Add((TreeViewAdapter)uop.Op);
                items.Add((TreeViewAdapter)uop.Right);
            }
            else
                treeItem.Background = Brushes.LightGreen;

            return treeItem;
           
        }
		*/
    }
}
