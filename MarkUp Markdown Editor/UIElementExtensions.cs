using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;

namespace MarkUp_Markdown_Editor;

internal static class UIElementExtensions
{
    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr hCursor);

    private const int IDC_SIZEWE = 32644;
    private const int IDC_ARROW = 32512;

    public static void ChangeCursor(this UIElement element, InputSystemCursorShape cursorShape)
    {
        int cursorId = cursorShape switch
        {
            InputSystemCursorShape.SizeWestEast => IDC_SIZEWE,
            _ => IDC_ARROW
        };
        var cursor = LoadCursor(IntPtr.Zero, cursorId);
        SetCursor(cursor);
    }
}
