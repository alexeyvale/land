using System;
using System.Collections.Generic;
using System.Linq;
using SW = System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Text.RegularExpressions;

using ICSharpCode.AvalonEdit.Search;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace TestGUI
{
    public class SelectedTextColorizer : DocumentColorizingTransformer
    {
        public int StartOffset { get; set; }
        public int EndOffset { get; set; }

        private TextArea linkedTextArea;
        private Regex isWord = new Regex(@"(?<=\W+)\w+(?=\W+)");
        private Regex selectedString = null;
        private int selectedLength = 0;
        private EventHandler selectionChanged;
        private bool colorSelections = true;
		private Brush BackgroundBrush = new SolidColorBrush(Color.FromRgb(180, 180, 250));

		public bool colorSelectedWord
        {
            get { return colorSelections; }

            set
            {
                if (value)
                {
                    if (!colorSelections)
                        linkedTextArea.SelectionChanged += selectionChanged;
                }
                else
                {
                    if (colorSelections)
                    {
                        selectedString = null;
                        linkedTextArea.SelectionChanged -= selectionChanged;
                        linkedTextArea.TextView.Redraw();
                    }
                }
                colorSelections = value;
            }
        }

        public SelectedTextColorizer(TextArea owner)
        {
            linkedTextArea = owner;
            linkedTextArea.TextView.LineTransformers.Add(this);
            selectionChanged = new EventHandler(linkedTextArea_SelectionChanged);
            linkedTextArea.SelectionChanged += selectionChanged;
        }

        private void linkedTextArea_SelectionChanged(object sender, EventArgs e)
        {
            if (!linkedTextArea.Selection.IsMultiline)
            {
                string selectedStr = linkedTextArea.Selection.GetText();
                if (selectedStr != "")
                {
                    string cLine = linkedTextArea.Document.GetText(linkedTextArea.Document.GetLineByOffset(linkedTextArea.Selection.Segments.First().StartOffset));
                    if (!(new Regex(@"^[^\W\d]\w*$")).IsMatch(selectedStr) || !(new Regex(@"\b" + selectedStr + @"\b")).IsMatch(cLine))
                        selectedString = null;
                    else
                    {
                        selectedString = new Regex(@"\b" + selectedStr + @"\b");
                        selectedLength = selectedStr.Length;
                    }
                }
                else
                    selectedString = null;
            }
            else
                selectedString = null;
            linkedTextArea.TextView.Redraw();
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (selectedString != null)
            {
                int ind = 0;
                var mtch = selectedString.Match(CurrentContext.Document.GetText(line), ind);
                while (mtch.Success)
                {
                    int start = line.Offset + mtch.Index;
                    if (linkedTextArea.Selection.IsEmpty || linkedTextArea.Selection.SurroundingSegment.Offset >= start + selectedLength ||
                        linkedTextArea.Selection.SurroundingSegment.EndOffset <= start)
                        ChangeLinePart(start, start + selectedLength, element => element.TextRunProperties.SetBackgroundBrush(BackgroundBrush));
                    ind = mtch.Index + selectedLength;
                    mtch = selectedString.Match(CurrentContext.Document.GetText(line), ind);
                }
            }
        }

        public void Reset()
        {
            selectedString = null;
            linkedTextArea.TextView.Redraw();
        }
    }
}
