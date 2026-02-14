using Microsoft.UI.Input;
using Microsoft.UI.Xaml;

namespace MarkUp_Markdown_Editor;

internal static class UIElementExtensions
{
    public static void ChangeCursor(this UIElement element, InputSystemCursorShape cursorShape)
    {
        // ProtectedCursor is a protected property on UIElement.
        // Use reflection to set it so the cursor persists while over the element.
        var prop = typeof(UIElement).GetProperty("ProtectedCursor",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        prop?.SetValue(element, InputSystemCursor.Create(cursorShape));
    }
}
