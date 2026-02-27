using TerrariaModder.Core.UI;
using TerrariaModder.Core.UI.Widgets;

namespace Plunder
{
    /// <summary>
    /// Draws a TextInput with a small X clear button instead of the built-in "Clear" button.
    /// Usage: TextInputX.Draw(myTextInput, x, y, w, h)
    /// The TextInput is drawn narrower to leave room for the X, so the built-in Clear
    /// button is pushed off-screen (clipped). Our X is drawn in the remaining space.
    /// </summary>
    public static class TextInputX
    {
        private const int XWidth = 20;

        /// <summary>
        /// Draw a TextInput with an X clear button instead of the default Clear button.
        /// </summary>
        public static void Draw(TextInput input, int x, int y, int width, int height)
        {
            bool hasText = !string.IsNullOrEmpty(input.Text);

            // Draw the TextInput narrower so the built-in Clear is clipped off
            int inputW = hasText ? width - XWidth : width;
            input.Draw(x, y, inputW, height);

            // Draw our X button in the remaining space
            if (hasText)
            {
                int xBtnX = x + width - XWidth;
                bool xHover = WidgetInput.IsMouseOver(xBtnX, y, XWidth, height);

                // Background to cover any bleed from the input
                UIRenderer.DrawRect(xBtnX, y, XWidth, height,
                    input.IsFocused ? UIColors.InputFocusBg : UIColors.InputBg);

                UIRenderer.DrawTextSmall("X", xBtnX + 6, y + (height - 11) / 2,
                    xHover ? UIColors.Error : UIColors.TextDim);

                if (xHover && WidgetInput.MouseLeftClick)
                {
                    input.Clear();
                    WidgetInput.ConsumeClick();
                }
            }
        }
    }
}
