using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace RetroMenu.Views
{
    /// <summary>Which mark to draw while a pinned entry is dragged over another.</summary>
    internal enum DropMarkKind
    {
        /// <summary>Dropping here puts both entries into one folder.</summary>
        Frame,

        /// <summary>Dropping here slots the entry in in front of this one.</summary>
        Before,

        /// <summary>And here, behind it.</summary>
        After
    }

    /// <summary>
    /// The frame around an entry, or the line beside it, that says where a dragged
    /// pin would land. It lives in the adorner layer, so it can be drawn over the
    /// menu without the entries underneath moving a pixel to make room for it.
    /// </summary>
    internal sealed class DropMark : Adorner
    {
        private readonly DropMarkKind _kind;
        private readonly bool _sideways;
        private readonly Pen _pen;

        /// <param name="sideways">
        /// True for the tile panel, whose entries run across the panel, so the line
        /// stands upright between two of them; false for the column on the left,
        /// where it lies across the row.
        /// </param>
        public DropMark(UIElement target, DropMarkKind kind, bool sideways, Brush brush)
            : base(target)
        {
            _kind = kind;
            _sideways = sideways;

            _pen = new Pen(brush, 2);
            _pen.Freeze();

            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext drawing)
        {
            var size = AdornedElement.RenderSize;
            if (size.Width < 4 || size.Height < 4) return;

            if (_kind == DropMarkKind.Frame)
            {
                drawing.DrawRoundedRectangle(null, _pen,
                    new Rect(1, 1, size.Width - 2, size.Height - 2), 3, 3);
                return;
            }

            if (_sideways)
            {
                double x = _kind == DropMarkKind.Before ? 1 : size.Width - 1;
                drawing.DrawLine(_pen, new Point(x, 2), new Point(x, size.Height - 2));
            }
            else
            {
                double y = _kind == DropMarkKind.Before ? 1 : size.Height - 1;
                drawing.DrawLine(_pen, new Point(2, y), new Point(size.Width - 2, y));
            }
        }
    }
}
