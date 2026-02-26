using System;
using System.Collections.Generic;
using TerrariaModder.Core.UI;
using TerrariaModder.Core.UI.Widgets;

namespace Plunder
{
    /// <summary>
    /// Enhanced tooltip with proper word-wrapping, conservative text measurement, and rounded corners.
    /// Drop-in replacement for TerrariaModder's Tooltip class.
    ///
    /// Key improvements over the platform Tooltip:
    ///   - Conservative SafeMeasure: max(real font, chars × 10px) — never underestimates
    ///   - Title is word-wrapped (platform leaves it unwrapped)
    ///   - Rounded corners (2px radius) so you can visually tell it apart from platform tooltips
    ///   - BeginClip/EndClip safety net as a last resort
    ///   - Reusable WordWrap() utility for any widget
    ///   - Configurable max width per call
    ///
    /// Usage:
    ///   Call Set() during hover detection.
    ///   Call DrawDeferred() AFTER panel.EndDraw() so it renders on top, outside any clip region.
    /// </summary>
    public static class RichTooltip
    {
        private static string _text;
        private static string _title;
        private static bool _hasTooltip;
        private static int _maxWidth;

        private const int DefaultMaxWidth = 300;
        private const int LineHeight = 16;
        private const int Padding = 8;
        private const int CursorOffset = 16;
        private const int ScreenMargin = 4;
        private const int CornerRadius = 2;

        /// <summary>
        /// Conservative per-character width estimate (pixels).
        /// MeasureText falls back to 7px which is too optimistic — the actual Terraria
        /// font is closer to 9-10px per character. We use whichever is larger.
        /// </summary>
        private const int FallbackCharWidth = 10;

        // ────────────────────────────────────────────────────────────────
        //  SET / CLEAR
        // ────────────────────────────────────────────────────────────────

        public static void Set(string text)
        {
            _text = text;
            _title = null;
            _maxWidth = DefaultMaxWidth;
            _hasTooltip = true;
        }

        public static void Set(string title, string description)
        {
            _title = title;
            _text = description;
            _maxWidth = DefaultMaxWidth;
            _hasTooltip = true;
        }

        public static void Set(string title, string description, int maxWidth)
        {
            _title = title;
            _text = description;
            _maxWidth = Math.Max(80, maxWidth);
            _hasTooltip = true;
        }

        public static void Clear()
        {
            _hasTooltip = false;
            _text = null;
            _title = null;
        }

        // ────────────────────────────────────────────────────────────────
        //  SAFE MEASUREMENT
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Conservative text width measurement.
        /// Returns the LARGER of the real font measurement and the 10px-per-char estimate.
        /// This prevents the 7px fallback from letting text overflow the tooltip.
        /// </summary>
        public static int SafeMeasure(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int real = TextUtil.MeasureWidth(text);
            int estimated = text.Length * FallbackCharWidth;
            return Math.Max(real, estimated);
        }

        // ────────────────────────────────────────────────────────────────
        //  WORD WRAP
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Word-wrap text to fit within a pixel width using conservative measurement.
        /// Splits on explicit newlines first, then wraps each paragraph at word boundaries.
        /// Falls back to character-level breaking for words that exceed the width.
        /// </summary>
        public static List<string> WordWrap(string text, int maxPixelWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;

            foreach (string paragraph in text.Split('\n'))
            {
                if (string.IsNullOrEmpty(paragraph))
                {
                    lines.Add("");
                    continue;
                }

                if (SafeMeasure(paragraph) <= maxPixelWidth)
                {
                    lines.Add(paragraph);
                    continue;
                }

                // Greedy forward scan — build lines word by word
                string[] words = paragraph.Split(' ');
                string currentLine = "";

                for (int w = 0; w < words.Length; w++)
                {
                    string word = words[w];
                    if (word.Length == 0) continue;

                    string candidate = currentLine.Length == 0
                        ? word
                        : currentLine + " " + word;

                    if (SafeMeasure(candidate) <= maxPixelWidth)
                    {
                        currentLine = candidate;
                    }
                    else if (currentLine.Length == 0)
                    {
                        // Single word wider than max — break by character
                        BreakLongWord(word, maxPixelWidth, lines);
                    }
                    else
                    {
                        // Commit current line
                        lines.Add(currentLine);

                        // Start new line with this word (or break it if too long)
                        if (SafeMeasure(word) <= maxPixelWidth)
                            currentLine = word;
                        else
                        {
                            BreakLongWord(word, maxPixelWidth, lines);
                            currentLine = "";
                        }
                    }
                }

                if (currentLine.Length > 0)
                    lines.Add(currentLine);
            }

            return lines;
        }

        /// <summary>
        /// Break a single long word at character boundaries, appending results to the lines list.
        /// Always takes at least 1 character per line to guarantee progress.
        /// </summary>
        private static void BreakLongWord(string word, int maxPixelWidth, List<string> lines)
        {
            int start = 0;
            while (start < word.Length)
            {
                int count = 1;
                while (start + count < word.Length &&
                       SafeMeasure(word.Substring(start, count + 1)) <= maxPixelWidth)
                {
                    count++;
                }

                lines.Add(word.Substring(start, count));
                start += count;
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  ROUNDED RECT DRAWING
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Draw a filled rectangle with 2px rounded corners.
        /// Visually distinguishes RichTooltip from the platform's sharp-cornered tooltip.
        /// </summary>
        private static void DrawRoundedRect(int x, int y, int w, int h, Color4 bg)
        {
            // Vertical center strip (full height, inset by radius on left/right)
            UIRenderer.DrawRect(x + CornerRadius, y, w - CornerRadius * 2, h, bg);
            // Horizontal center strip (full width, inset by radius on top/bottom)
            UIRenderer.DrawRect(x, y + CornerRadius, w, h - CornerRadius * 2, bg);
            // Fill the 4 corner curves — single diagonal pixel per corner
            UIRenderer.DrawRect(x + 1, y + 1, 1, 1, bg);
            UIRenderer.DrawRect(x + w - 2, y + 1, 1, 1, bg);
            UIRenderer.DrawRect(x + 1, y + h - 2, 1, 1, bg);
            UIRenderer.DrawRect(x + w - 2, y + h - 2, 1, 1, bg);
        }

        /// <summary>
        /// Draw a 1px outline with 2px rounded corners.
        /// </summary>
        private static void DrawRoundedRectOutline(int x, int y, int w, int h, Color4 border)
        {
            // Top edge (inset from corners)
            UIRenderer.DrawRect(x + CornerRadius, y, w - CornerRadius * 2, 1, border);
            // Bottom edge
            UIRenderer.DrawRect(x + CornerRadius, y + h - 1, w - CornerRadius * 2, 1, border);
            // Left edge
            UIRenderer.DrawRect(x, y + CornerRadius, 1, h - CornerRadius * 2, border);
            // Right edge
            UIRenderer.DrawRect(x + w - 1, y + CornerRadius, 1, h - CornerRadius * 2, border);
            // Corner diagonals (1px each)
            UIRenderer.DrawRect(x + 1, y + 1, 1, 1, border);
            UIRenderer.DrawRect(x + w - 2, y + 1, 1, 1, border);
            UIRenderer.DrawRect(x + 1, y + h - 2, 1, 1, border);
            UIRenderer.DrawRect(x + w - 2, y + h - 2, 1, 1, border);
        }

        // ────────────────────────────────────────────────────────────────
        //  DRAW
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Draw the tooltip near the mouse cursor.
        /// Call AFTER panel.EndDraw() so it renders on top, outside any panel clip region.
        /// </summary>
        public static void DrawDeferred()
        {
            if (!_hasTooltip) return;
            if (string.IsNullOrEmpty(_text) && string.IsNullOrEmpty(_title))
            {
                _hasTooltip = false;
                return;
            }

            int maxContentWidth = _maxWidth - Padding * 2;

            // Build all lines with title tracking
            var lines = new List<string>();
            int titleLineCount = 0;

            if (!string.IsNullOrEmpty(_title))
            {
                var titleLines = WordWrap(_title, maxContentWidth);
                titleLineCount = titleLines.Count;
                lines.AddRange(titleLines);
            }

            if (!string.IsNullOrEmpty(_text))
                lines.AddRange(WordWrap(_text, maxContentWidth));

            if (lines.Count == 0)
            {
                _hasTooltip = false;
                return;
            }

            // Measure actual widths for tight-fitting tooltip (using safe measurement)
            int contentWidth = 0;
            foreach (var line in lines)
                contentWidth = Math.Max(contentWidth, SafeMeasure(line));

            int tooltipWidth = Math.Min(contentWidth + Padding * 2, _maxWidth);
            int tooltipHeight = lines.Count * LineHeight + Padding * 2;

            // Position near mouse, clamped to screen edges
            int tx = WidgetInput.MouseX + CursorOffset;
            int ty = WidgetInput.MouseY + CursorOffset;

            if (tx + tooltipWidth > WidgetInput.ScreenWidth - ScreenMargin)
                tx = WidgetInput.MouseX - tooltipWidth - ScreenMargin;
            if (ty + tooltipHeight > WidgetInput.ScreenHeight - ScreenMargin)
                ty = WidgetInput.MouseY - tooltipHeight - ScreenMargin;

            tx = Math.Max(ScreenMargin, tx);
            ty = Math.Max(ScreenMargin, ty);

            // Rounded background + border
            DrawRoundedRect(tx, ty, tooltipWidth, tooltipHeight, UIColors.TooltipBg);
            DrawRoundedRectOutline(tx, ty, tooltipWidth, tooltipHeight, UIColors.Border);

            // Clip text to tooltip bounds — safety net in case anything still slips through
            UIRenderer.BeginClip(tx, ty, tooltipWidth, tooltipHeight);

            int ly = ty + Padding;
            for (int i = 0; i < lines.Count; i++)
            {
                Color4 color = (i < titleLineCount) ? UIColors.TextTitle : UIColors.Text;
                UIRenderer.DrawText(lines[i], tx + Padding, ly, color);
                ly += LineHeight;
            }

            UIRenderer.EndClip();

            _hasTooltip = false;
        }
    }
}
