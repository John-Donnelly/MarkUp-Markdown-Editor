using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace MarkUp.UITests;

public abstract class AppSession
{
    private const string WinAppDriverUrl      = "http://127.0.0.1:4723";
    private const string WinAppDriverPath     = @"C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe";
    private const string AppPackageName       = "JADApps.MarkUpMarkdownEditor";
    private const string AppWindowTitleSuffix = "— MarkUp";

    // ── Remote mode ──────────────────────────────────────────────────────────
    // UI tests always target the shared remote WinAppDriver host.
    // WinAppDriver must already be running on the remote machine before the test run starts.

    /// <summary>WinAppDriver endpoint on the remote test machine.</summary>
    private const string RemoteDriverUrl = "http://192.168.0.100:4723";
    private const string RemoteAppEnvironmentVariable = "UITEST_REMOTE_APP";
    private const string RemoteAppAumidEnvironmentVariable = "UITEST_REMOTE_AUMID";
    private const string RemoteAppPathEnvironmentVariable = "UITEST_REMOTE_APP_PATH";
    private const string RemoteWinRmUsernameEnvironmentVariable = "UITEST_REMOTE_WINRM_USERNAME";
    private const string RemoteWinRmPasswordEnvironmentVariable = "UITEST_REMOTE_WINRM_PASSWORD";
    private const string DotEnvFileName = ".env";
    private const string RemoteAppAumidDefault = "JADApps.MarkUpMarkdownEditor_30vn2v44e6ykm!App";
    private const string UiTestPackageCertificatePassword = "MarkUpUiTestsCertificate!2026";

    /// <summary>
    /// Default path to the app executable AS SEEN BY WinAppDriver on the remote machine.
    /// Override with the <c>UITEST_REMOTE_APP_PATH</c> environment variable when the path differs.
    /// </summary>
    private const string RemoteAppPathDefault =
        @"Z:\repos\MarkUp Markdown Editor\MarkUp Markdown Editor\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\MarkUp Markdown Editor.exe";

    /// <summary>Active WinAppDriver URL for this workspace.</summary>
    private static string DriverUrl => RemoteDriverUrl;

    // Retry timing for FindById (no IPC during the sleep — purely in-process)
    private const int ElementRetryMs  = 200;
    private const int ElementTimeoutS = 10;

    private static Process? _winAppDriverProcess;
    private static nint     _appWindowHandle;
    private static string? _lastSessionInitializationError;

    protected static WindowsDriver? Session       { get; private set; }
    protected static WindowsDriver? DesktopSession { get; private set; }
    protected static bool IsSessionAvailable => Session is not null;

    // ─────────────────────────────────────────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    internal static void InitialiseSession()
    {
        InitialiseRemoteSession();
    }

    /// <summary>
    /// Initialises a session against WinAppDriver running on a remote machine.
    /// The remote WinAppDriver process must already be listening before this is called.
    /// The app is launched by WinAppDriver on the remote machine using the <c>app</c> capability;
    /// no local process management or Win32 HWND operations are performed.
    /// </summary>
    private static void InitialiseRemoteSession()
    {
        LoadDotEnvVariables(FindSolutionRoot());
        _lastSessionInitializationError = null;
        Trace.WriteLine($"[UITests] Remote mode — driver: {DriverUrl}");

        if (!IsRemoteDriverListening())
        {
            _lastSessionInitializationError =
                $"Remote WinAppDriver endpoint {DriverUrl} is not reachable. " +
                $"Run MarkUp.UITests\\Setup-RemoteUiTestHost.ps1 on the remote machine before running tests.";
            Trace.WriteLine($"[UITests] {_lastSessionInitializationError}");
            return;
        }

        string? installedAumid = null;
        string? packageInstallError = null;

        try
        {
            installedAumid = TryInstallLatestRemotePackage();
        }
        catch (InvalidOperationException ex)
        {
            packageInstallError = $"Could not install the latest remote package: {ex.Message}";
            Trace.WriteLine($"[UITests] {packageInstallError}");
        }

        foreach (var appId in GetRemoteAppTargets(installedAumid))
        {
            Trace.WriteLine($"[UITests] Trying remote app target: {appId}");
            try
            {
                Session = CreateRemoteAppSession(appId);
                Session.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(2000);
                DesktopSession = CreateRemoteDesktopSession();
                DesktopSession.Manage().Timeouts().ImplicitWait = TimeSpan.FromMilliseconds(500);
                if (!WarmUpSessionRoot())
                    throw new WebDriverException(
                        $"App editor 'EditorTextBox' did not appear on the remote machine within 30 seconds " +
                        $"after launching '{appId}'. The app may not have started correctly.");
                try { Session?.Manage().Window.Maximize(); Thread.Sleep(500); } catch { }
                _lastSessionInitializationError = null;
                return;
            }
            catch (WebDriverException ex)
            {
                CleanupRemoteSessions();
                _lastSessionInitializationError = $"Could not start remote session with '{appId}': {ex.Message}";
                Trace.WriteLine($"[UITests] {_lastSessionInitializationError}");
            }
        }

        _lastSessionInitializationError ??= packageInstallError ?? "Could not start remote session because no remote app target is configured.";
    }

    internal static void CleanupSession()
    {
        try { Session?.Quit(); } catch { }
        Session = null;
        try { DesktopSession?.Quit(); } catch { }
        DesktopSession = null;
        _lastSessionInitializationError = null;
        try { if (_winAppDriverProcess is { HasExited: false }) _winAppDriverProcess.Kill(entireProcessTree: true); } catch { }
        _winAppDriverProcess = null;
        _appWindowHandle = 0;
    }

    /// <summary>
    /// Quits the current WinAppDriver sessions and reopens fresh ones to the same app window.
    /// Call this after any operation that permanently corrupts the UIA session
    /// (e.g. <see cref="CoreWebView2.ShowPrintUI"/>, which puts WebView2 into print mode and
    /// leaves the WinAppDriver UIA provider in an unrecoverable blocking state).
    /// </summary>
    protected static void ReinitializeSession()
    {
        try { Session?.Quit(); } catch { }
        Session = null;
        try { DesktopSession?.Quit(); } catch { }
        DesktopSession = null;

        InitialiseRemoteSession();
        try { Session?.Manage().Window.Maximize(); Thread.Sleep(300); } catch { }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Test helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Restores and focuses the app window. Pure Win32 — no WinAppDriver IPC.</summary>
    protected static void BringToFront()
    {
        if (_appWindowHandle == 0) return;
        ShowWindow(_appWindowHandle, SW_RESTORE);
        SetForegroundWindow(_appWindowHandle);
        Thread.Sleep(200);
    }

    protected static void SkipIfNoSession()
    {
        if (Session is null || !IsSessionResponsive())
            ReinitializeSession();

        if (Session is null)
            Assert.Inconclusive($"{_lastSessionInitializationError ?? "WinAppDriver session not available."} See MarkUp.UITests\\README.md.");
    }

    /// <summary>
    /// Clears the editor to a known-empty state. Closes the find bar if open (via Escape — no UIA
    /// traversal needed), then Ctrl+A → Delete. Does NOT use File &gt; New.
    /// </summary>
    protected static void ResetToCleanState()
    {
        try { SendEscapeKey(); } catch { }  // Close find bar if open (harmless if not)
        Thread.Sleep(100);

        var editor = TryFindById("EditorTextBox");
        if (editor is null)
        {
            CleanupSession();
            InitialiseSession();
            SkipIfNoSession();
            editor = FindById("EditorTextBox");
        }

        editor.Click();
        Thread.Sleep(100);
        editor.SendKeys(Keys.Control + "a");  // Direct to editor — avoids SwitchTo().ActiveElement() focus race
        Thread.Sleep(100);
        editor.SendKeys(Keys.Delete);
        Thread.Sleep(200);
    }

    /// <summary>Switches to split view
    protected static void EnsureSplitView()
    {
        if (Session is null) return;
        try { TryFindById("EditorTextBox")?.Click(); } catch { }  // Dismiss any open flyout before clicking menu
        Thread.Sleep(100);
        ClickMenu("MenuBarView", "MenuViewSplit");
        Thread.Sleep(300);
    }

    /// <summary>Resets zoom to 100% via menu. Used by <see cref="ViewWorkflowTests"/>.</summary>
    protected static void EnsureZoom100()
    {
        if (Session is null) return;
        try { TryFindById("EditorTextBox")?.Click(); } catch { }  // Dismiss any open flyout before clicking menu
        Thread.Sleep(100);
        ClickMenu("MenuBarView", "MenuZoomReset");
        Thread.Sleep(150);
    }

    /// <summary>Shows the status bar if it was hidden. Used by <see cref="ViewWorkflowTests"/>.</summary>
    protected static void EnsureStatusBarVisible()
    {
        if (Session is null) return;
        if (IsHidden("StatusBarStats"))
            ClickMenu("MenuBarView", "MenuToggleStatusBar");
    }

    /// <summary>
    /// Lightweight cleanup: brings the app to front and sends two Escape key presses.
    /// The first closes any open sub-menu or ContentDialog; the second closes any parent menu
    /// that may have been left open (e.g. File menu still open after export sub-menu test fails).
    /// Does NOT use <see cref="SendEscapeToFocused"/> — that could cancel a WebView2 back-navigation.
    /// </summary>
    protected static void DismissModal()
    {
        if (!IsSessionResponsive())
        {
            CleanupSession();
            InitialiseSession();
            return;
        }

        BringToFront();
        try { SendEscapeKey(); } catch { }  // Close submenu / ContentDialog
        Thread.Sleep(150);
        try { SendEscapeKey(); } catch { }  // Close parent menu (harmless if nothing open)
        Thread.Sleep(100);
        try { TryFindById("EditorTextBox")?.Click(); } catch { }  // Dismiss any remaining WinUI3 flyout
        Thread.Sleep(100);
    }

    /// <summary>Thorough transient-window dismissal. Use in specific test helpers, not in TestCleanup.</summary>
    protected static void DismissTransientWindows()
    {
        if (Session is null) return;
        try { TryFindById("CloseFindButton")?.Click(); } catch { }
        Thread.Sleep(100);
        try
        {
            foreach (var name in new[] { "Don't Save", "Cancel", "Close" })
            {
                var el = TryFindInAppByName(name);
                if (el is not null) { el.Click(); return; }
            }
        }
        catch { }
        try { TryFindDesktopByAnyName("Don't Save", "Cancel", "Close")?.Click(); } catch { }
        try { SendEscapeKey(); Thread.Sleep(150); } catch { }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Element finders
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds a required element by AutomationId, retrying for up to <see cref="ElementTimeoutS"/> seconds.
    /// Each attempt uses <see cref="TryFindById"/> which issues a <c>FindFirst</c> (not <c>FindAll</c>) call
    /// so traversal stops at the first match and never enters the WebView2 subtree for elements that precede
    /// it in the UIA tree. <see cref="ElementRetryMs"/> adds a short in-process back-off between retries.
    /// </summary>
    protected static AppiumElement FindById(string automationId)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < ElementTimeoutS)
        {
            var el = TryFindById(automationId) ?? TryFindByFallbackName(automationId);
            if (el is not null) return el;
            Thread.Sleep(ElementRetryMs);
        }
        var last = TryFindById(automationId) ?? TryFindByFallbackName(automationId);
        return last ?? throw new NoSuchElementException(
            $"Element '{automationId}' was not found within {ElementTimeoutS}s.");
    }

    /// <summary>
    /// Tries to find an element by AutomationId using <c>FindFirst</c> (not <c>FindAll</c>).
    /// <c>FindFirst</c> stops at the first match and does NOT traverse the WebView2 subtree
    /// for elements (Toolbar, MenuBar, EditorTextBox) that appear before it in the UIA tree,
    /// avoiding the WinAppDriver block that occurs when WebView2 is navigating.
    /// </summary>
    protected static AppiumElement? TryFindById(string automationId)
    {
        if (Session is null) return null;
        try { return Session.FindElement(MobileBy.AccessibilityId(automationId)); }
        catch (Exception ex) when (IsRecoverableWindowException(ex))
        {
            ReinitializeSession();
            if (Session is null) return null;
            try { return Session.FindElement(MobileBy.AccessibilityId(automationId)); }
            catch { return null; }
        }
        catch { return null; }
    }

    /// <summary>
    /// Finds a child element by AutomationId scoped to <paramref name="container"/>, retrying
    /// for up to <see cref="ElementTimeoutS"/> seconds. Limits UIA traversal to the container's
    /// subtree so Chrome's process-global hook only affects the small number of nodes inside
    /// the container rather than the entire window tree.
    /// </summary>
    protected static AppiumElement FindByIdWithin(AppiumElement container, string automationId)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < ElementTimeoutS)
        {
            var el = TryFindByIdWithin(container, automationId);
            if (el is not null) return el;
            Thread.Sleep(ElementRetryMs);
        }
        var last = TryFindByIdWithin(container, automationId);
        return last ?? throw new NoSuchElementException(
            $"Element '{automationId}' was not found within container in {ElementTimeoutS}s.");
    }

    /// <summary>Tries to find a child element by AutomationId scoped to the given container.</summary>
    protected static AppiumElement? TryFindByIdWithin(AppiumElement container, string automationId)
    {
        try { return container.FindElement(MobileBy.AccessibilityId(automationId)); }
        catch { return null; }
    }

    protected static AppiumElement GetCachedElement(ref AppiumElement? cache, string automationId)
    {
        if (cache is not null)
        {
            try
            {
                _ = cache.Enabled;
                return cache;
            }
            catch (Exception ex) when (IsRecoverableElementException(ex))
            {
                CleanupSession();
                InitialiseSession();
                cache = null;
            }
        }

        cache = FindById(automationId);
        return cache;
    }

    protected static AppiumElement GetCachedElementWithin(ref AppiumElement? cache, ref AppiumElement? containerCache,
        string containerAutomationId, string automationId)
    {
        AppiumElement container;
        try
        {
            container = GetCachedElement(ref containerCache, containerAutomationId);
        }
        catch (NoSuchElementException)
        {
            cache = FindById(automationId);
            return cache;
        }

        if (cache is not null)
        {
            try
            {
                _ = cache.Enabled;
                return cache;
            }
            catch (Exception ex) when (IsRecoverableElementException(ex))
            {
                CleanupSession();
                InitialiseSession();
                containerCache = null;
                container = GetCachedElement(ref containerCache, containerAutomationId);
                cache = null;
            }
        }

        cache = FindByIdWithin(container, automationId);
        return cache;
    }

    private static bool IsRecoverableWindowException(Exception ex) =>
        ex is NoSuchWindowException
        || ex is NotImplementedException { Message: var notImplementedMessage }
            && notImplementedMessage.Contains("requested resource could not be found", StringComparison.OrdinalIgnoreCase)
        || ex is WebDriverException { Message: var webDriverMessage }
            && (webDriverMessage.Contains("window has been closed", StringComparison.OrdinalIgnoreCase)
                || webDriverMessage.Contains("requested resource could not be found", StringComparison.OrdinalIgnoreCase));

    private static bool IsRecoverableElementException(Exception ex) =>
        IsRecoverableWindowException(ex) || ex is StaleElementReferenceException;

    private static bool IsSessionResponsive()
    {
        if (Session is null) return false;

        try
        {
            _ = Session.Title;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Polls for <c>EditorTextBox</c> in the remote session's UIA tree for up to 30 seconds.
    /// Returns <c>true</c> when the element is found (app is ready), <c>false</c> on timeout
    /// (app did not appear on the remote machine within the expected window).
    /// </summary>
    private static bool WarmUpSessionRoot()
    {
        if (Session is null) return false;

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            try
            {
                var editor = Session.FindElement(MobileBy.AccessibilityId("EditorTextBox"));
                if (editor is not null)
                    return true;
            }
            catch { }

            Thread.Sleep(250);
        }

        return false;
    }

    protected static bool IsDisplayed(string automationId) =>
        TryFindById(automationId)?.Displayed == true;

    protected static bool IsHidden(string automationId) =>
        !IsDisplayed(automationId);

    protected static void ClickMenu(string menuBarId, string menuItemId)
    {
        FindById(menuBarId).Click();
        Thread.Sleep(350);
        FindById(menuItemId).Click();
        Thread.Sleep(200);
    }

    protected static void ClickSubMenu(string menuBarId, string subMenuId, string menuItemId)
    {
        FindById(menuBarId).Click();
        Thread.Sleep(350);
        FindById(subMenuId).Click();
        Thread.Sleep(300);

        // Try the app session first (AccessibilityId then Name fallback)
        var item = TryFindById(menuItemId);

        // WinUI3 flyout sub-menus open in the desktop UIA tree, not the app session tree.
        // Fall back to DesktopSession when the item is not visible in the app session.
        if (item is null && DesktopSession is not null)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < ElementTimeoutS)
            {
                try
                {
                    item = DesktopSession.FindElement(MobileBy.AccessibilityId(menuItemId));
                    if (item is not null) break;
                }
                catch { }

                var name = GetFallbackName(menuItemId);
                if (name is not null)
                {
                    try
                    {
                        item = DesktopSession.FindElement(MobileBy.Name(name));
                        if (item is not null) break;
                    }
                    catch { }
                }

                Thread.Sleep(ElementRetryMs);
            }
        }

        if (item is null)
            throw new NoSuchElementException(
                $"Element '{menuItemId}' was not found within {ElementTimeoutS}s.");

        item.Click();
        Thread.Sleep(200);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Desktop / app name searches
    // ─────────────────────────────────────────────────────────────────────────

    protected static AppiumElement? TryFindDesktopByName(string name)
    {
        if (DesktopSession is null) return null;
        try
        {
            var els = DesktopSession.FindElements(MobileBy.Name(name));
            return els.Count > 0 ? els[0] : null;
        }
        catch { return null; }
    }

    protected static AppiumElement? TryFindDesktopByAnyName(params string[] names)
    {
        foreach (var n in names)
        {
            var el = TryFindDesktopByName(n);
            if (el is not null) return el;
        }
        return null;
    }

    protected static AppiumElement? WaitForDesktopByAnyName(TimeSpan timeout, params string[] names)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var el = TryFindDesktopByAnyName(names);
            if (el is not null) return el;
            Thread.Sleep(100);
        }
        return TryFindDesktopByAnyName(names);
    }

    protected static AppiumElement? TryFindInAppByName(string name)
    {
        if (Session is null) return null;
        try { return Session.FindElement(MobileBy.Name(name)); }
        catch { return null; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Keyboard injection (Appium Actions — routes through WinAppDriver to the remote machine)
    // ─────────────────────────────────────────────────────────────────────────

    protected static void SendCtrlShortcut(char key)       => SendRemoteModifiedKeys(Keys.Control, key.ToString().ToLower());
    protected static void SendCtrlShortcut(ushort vk)      => SendRemoteModifiedKeys(Keys.Control, VkToSeleniumKey(vk));
    protected static void SendCtrlShiftShortcut(char key)  => SendRemoteModifiedKeys(Keys.Control, Keys.Shift, key.ToString().ToLower());
    protected static void SendCtrlShiftShortcut(ushort vk) => SendRemoteModifiedKeys(Keys.Control, Keys.Shift, VkToSeleniumKey(vk));
    protected static void SendAltShortcut(ushort vk)       => SendRemoteModifiedKeys(Keys.Alt, VkToSeleniumKey(vk));

    /// <summary>
    /// Injects <paramref name="text"/> into the editor via the <c>AutomationEditorInput</c>
    /// automation path.
    /// <para>
    /// Appium Windows driver 5.x uses keyboard simulation (~100 ms/char), not
    /// <c>IValueProvider.SetValue</c>.  This means <c>#</c> maps to <c>£</c> on a UK keyboard
    /// layout.  Newlines and <c>#</c> are therefore encoded as safe ASCII placeholders
    /// (<c>|NEWLINE|</c> and <c>|HASH|</c>) before sending; the <c>EditorSyncTimer</c> decodes
    /// them back and applies the content to <c>EditorTextBox</c> once the content is stable for
    /// ≥2 consecutive 150 ms ticks (debounce = 300 ms).
    /// </para>
    /// </summary>
    protected static void PasteText(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        // Encode newlines and # as safe placeholders before injecting into the single-line input TextBox.
        var encoded = text.Replace("\r\n", "|NEWLINE|").Replace("\n", "|NEWLINE|").Replace("#", "|HASH|");
        var input = FindById("AutomationEditorInput");
        input.SendKeys(encoded);
        // Phase 1: wait for at least the first character to arrive in the TextBox.
        // Appium dispatches key events faster than WinUI3 processes them, so the TextBox may still be
        // empty immediately after SendKeys returns.  Polling without this guard would exit prematurely.
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalMilliseconds < 3000)
        {
            Thread.Sleep(50);
            try { if (!string.IsNullOrEmpty(input.Text)) break; }
            catch { break; }
        }
        // Phase 2: wait until the EditorSyncTimer debounce clears the input (content is stable for
        // ≥2 ticks = ≥300ms, timer has set EditorTextBox.Text and cleared AutomationEditorInput).
        sw.Restart();
        while (sw.Elapsed.TotalMilliseconds < 3000)
        {
            Thread.Sleep(100);
            try { if (string.IsNullOrEmpty(input.Text)) break; }
            catch { break; }
        }
        // Small margin for UIA value propagation after EditorTextBox.Text is set programmatically.
        Thread.Sleep(200);
    }

    protected static void SendHashCharacter() => PasteText("#");

    protected static void SendText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        FindById("EditorTextBox").SendKeys(text);
        Thread.Sleep(150);
    }

    protected static void SendCtrlAddShortcut()      => SendRemoteModifiedKeys(Keys.Control, Keys.Add);
    protected static void SendCtrlSubtractShortcut() => SendRemoteModifiedKeys(Keys.Control, Keys.Subtract);
    protected static void SendEscapeKey()            => SendRemoteKey(Keys.Escape);
    /// <summary>Sends Escape through the active WinAppDriver session (closes dialogs, menus, or find bar).</summary>
    protected static void SendEscapeToFocused()      => SendRemoteKey(Keys.Escape);
    protected static void SendDeleteKey()            => SendRemoteKey(Keys.Delete);
    protected static void SendEnterKey()             => SendRemoteKey(Keys.Return);
    protected static void SendDownKey()              => SendRemoteKey(Keys.ArrowDown);
    protected static void SendRightKey()             => SendRemoteKey(Keys.ArrowRight);

    private static void SendRemoteModifiedKeys(params string[] keys)
    {
        if (Session is null) return;
        // Chord notation (e.g. Keys.Control + "a") routes through /element/{id}/value,
        // which WinAppDriver supports. The W3C Actions keyboard input-source is NOT supported.
        var chord = string.Concat(keys);
        SendKeysViaElement(chord);
        Thread.Sleep(200);
    }

    private static void SendRemoteKey(string key)
    {
        if (Session is null) return;
        SendKeysViaElement(key);
        Thread.Sleep(150);
    }

    /// <summary>Sends keys to the currently focused element, falling back to the editor text box.</summary>
    private static void SendKeysViaElement(string keys)
    {
        if (Session is null) return;
        IWebElement? target = null;
        try { target = Session.SwitchTo().ActiveElement(); } catch { }
        target ??= TryFindById("EditorTextBox");
        target?.SendKeys(keys);
    }

    private static string VkToSeleniumKey(ushort vk) => vk switch
    {
        VK_ESCAPE    => Keys.Escape,
        VK_RETURN    => Keys.Return,
        VK_DELETE    => Keys.Delete,
        VK_DOWN      => Keys.ArrowDown,
        VK_RIGHT     => Keys.ArrowRight,
        VK_ADD       => Keys.Add,
        VK_SUBTRACT  => Keys.Subtract,
        _            => throw new ArgumentOutOfRangeException(nameof(vk), vk, "No Selenium key mapping for this virtual key code.")
    };

    // ─────────────────────────────────────────────────────────────────────────
    //  WinAppDriver startup
    // ─────────────────────────────────────────────────────────────────────────

    private static bool EnsureWinAppDriverRunning()
    {
        if (IsWinAppDriverListening()) return true;

        if (!File.Exists(WinAppDriverPath))
        {
            Trace.WriteLine($"[UITests] WinAppDriver not found at '{WinAppDriverPath}'.");
            return false;
        }

        if (TryStartWinAppDriver(elevated: false, TimeSpan.FromSeconds(5)))
            return true;

        try { if (_winAppDriverProcess is { HasExited: false }) _winAppDriverProcess.Kill(entireProcessTree: true); } catch { }
        _winAppDriverProcess = null;

        return TryStartWinAppDriver(elevated: true, TimeSpan.FromSeconds(20));
    }

    private static bool TryStartWinAppDriver(bool elevated, TimeSpan timeout)
    {
        try
        {
            _winAppDriverProcess = Process.Start(new ProcessStartInfo
            {
                FileName       = WinAppDriverPath,
                UseShellExecute = elevated,
                Verb           = elevated ? "runas" : string.Empty
            });
        }
        catch (Win32Exception ex)
        {
            Trace.WriteLine($"[UITests] Could not start WinAppDriver{(elevated ? " (elevated)" : string.Empty)}: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[UITests] Could not start WinAppDriver: {ex.Message}");
            return false;
        }

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (IsWinAppDriverListening()) return true;
            if (_winAppDriverProcess is { HasExited: true }) return false;
            Thread.Sleep(250);
        }
        return IsWinAppDriverListening();
    }

    private static bool IsWinAppDriverListening()
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync("127.0.0.1", 4723).Wait(1000) && client.Connected;
        }
        catch { return false; }
    }

    /// <summary>
    /// Checks that the remote WinAppDriver endpoint is reachable via TCP.
    /// Called before any deployment or session creation so that a missing remote host
    /// is diagnosed immediately rather than silently failing through all app-target fallbacks.
    /// </summary>
    private static bool IsRemoteDriverListening()
    {
        var uri = new Uri(DriverUrl);
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync(uri.Host, uri.Port).Wait(3000) && client.Connected;
        }
        catch { return false; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  App activation & window discovery
    // ─────────────────────────────────────────────────────────────────────────

    private static string? ResolveAumid()
    {
        try
        {
            using var ps = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "powershell.exe",
                    Arguments              = $"-NoProfile -Command \"(Get-AppxPackage -Name '{AppPackageName}*').PackageFamilyName\"",
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                }
            };
            ps.Start();
            string pfn = ps.StandardOutput.ReadToEnd().Trim();
            ps.WaitForExit();
            return string.IsNullOrWhiteSpace(pfn) ? null : $"{pfn}!App";
        }
        catch { return null; }
    }

    private static string? TryInstallLatestRemotePackage()
    {
        string solutionRoot = FindSolutionRoot();
        string localPackagePath = FindLatestLocalPackagePath(solutionRoot);
        EnsureLocalPackageCertificate(localPackagePath, solutionRoot);
        string localPackageDirectory = Path.GetDirectoryName(localPackagePath)!;
        string localPackageVersion = ExtractPackageVersion(Path.GetFileNameWithoutExtension(localPackagePath));
        Trace.WriteLine($"[UITests] Local package: {Path.GetFileName(localPackagePath)} (v{localPackageVersion})");

        return ExecuteRemotePackageInstall(localPackageDirectory, localPackageVersion, TimeSpan.FromMinutes(3));
    }

    /// <summary>Extracts the four-part version from an MSIX base name, e.g. "MarkUp Markdown Editor_1.4.0.0_x64_Debug" → "1.4.0.0".</summary>
    private static string ExtractPackageVersion(string msixBaseName)
    {
        var m = Regex.Match(msixBaseName, @"_(\d+\.\d+\.\d+\.\d+)_");
        return m.Success ? m.Groups[1].Value : "0.0.0.0";
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "MarkUp.UITests"))
                && Directory.Exists(Path.Combine(directory.FullName, "MarkUp Markdown Editor")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the solution root for the UI test workspace.");
    }

    private static void LoadDotEnvVariables(string solutionRoot)
    {
        string dotEnvPath = Path.Combine(solutionRoot, DotEnvFileName);
        if (!File.Exists(dotEnvPath)) return;

        foreach (string rawLine in File.ReadAllLines(dotEnvPath))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            int separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            string key = line[..separatorIndex].Trim();
            if (string.IsNullOrWhiteSpace(key) || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                continue;

            string value = line[(separatorIndex + 1)..].Trim();
            if (value.Length >= 2)
            {
                if ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
                    value = value[1..^1];
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string FindLatestLocalPackagePath(string solutionRoot)
    {
        string appPackagesDirectory = Path.Combine(solutionRoot, "MarkUp Markdown Editor", "AppPackages");
        if (!Directory.Exists(appPackagesDirectory))
            throw new InvalidOperationException($"App package directory '{appPackagesDirectory}' was not found.");

        string dependenciesSegment = $"{Path.DirectorySeparatorChar}Dependencies{Path.DirectorySeparatorChar}";
        string bundleSegment = $"{Path.DirectorySeparatorChar}Bundle{Path.DirectorySeparatorChar}";

        string? latestPackagePath = Directory
            .EnumerateFiles(appPackagesDirectory, "*.msix", SearchOption.AllDirectories)
            .Where(path => path.Contains("_x64", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(dependenciesSegment, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(bundleSegment, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return latestPackagePath
            ?? throw new InvalidOperationException($"No x64 app package was found under '{appPackagesDirectory}'.");
    }

    private static void EnsureLocalPackageCertificate(string localPackagePath, string solutionRoot)
    {
        string localCertificatePath = Path.ChangeExtension(localPackagePath, ".cer");
        if (File.Exists(localCertificatePath))
            return;

        string certificatePfxPath = FindLocalPackageCertificatePath(solutionRoot);

        using var certificate = new X509Certificate2(
            certificatePfxPath,
            UiTestPackageCertificatePassword,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);

        File.WriteAllBytes(localCertificatePath, certificate.Export(X509ContentType.Cert));
    }

    private static string FindLocalPackageCertificatePath(string solutionRoot)
    {
        string appProjectDirectory = Path.Combine(solutionRoot, "MarkUp Markdown Editor");
        if (!Directory.Exists(appProjectDirectory))
            throw new InvalidOperationException($"App project directory '{appProjectDirectory}' was not found.");

        string? certificatePfxPath = Directory
            .EnumerateFiles(appProjectDirectory, "MarkUpMarkdownEditor.UITests.pfx", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return certificatePfxPath
            ?? throw new InvalidOperationException($"No generated package signing certificate was found under '{appProjectDirectory}'.");
    }

    /// <summary>
    /// Builds a PowerShell script that deploys the app package to the remote machine.
    /// Uses <c>New-PSSession</c> + <c>Copy-Item -ToSession</c> to transfer files over WinRM,
    /// bypassing SMB share access entirely, then installs via a scheduled task under the
    /// interactive user token (required for AppX PLM initialization).
    /// Skips the file copy and install entirely when the correct version is already installed.
    /// </summary>
    private static string BuildRemotePackageInstallScript(string localPackageDirectory, string localPackageVersion)
    {
        // The generated script runs locally (via ExecuteRemotePackageInstall).
        // It opens a PSSession, then checks if the right version is already installed.
        // If yes it returns the AUMID immediately. If no it copies the files and installs.
        return $$"""
$ErrorActionPreference = 'Stop'
$computerName = '{{EscapePowerShellSingleQuotedString(new Uri(DriverUrl).Host)}}'
$username = $env:{{RemoteWinRmUsernameEnvironmentVariable}}
$password = $env:{{RemoteWinRmPasswordEnvironmentVariable}}
$localPackageDirectory = '{{EscapePowerShellSingleQuotedString(localPackageDirectory)}}'
$packageName = '{{AppPackageName}}'
$localVersion = '{{localPackageVersion}}'

if ([string]::IsNullOrWhiteSpace($username) -or [string]::IsNullOrWhiteSpace($password))
{
    throw 'Remote WinRM credentials are required (UITEST_REMOTE_WINRM_USERNAME / UITEST_REMOTE_WINRM_PASSWORD).'
}

$securePassword = ConvertTo-SecureString $password -AsPlainText -Force
$credential = [pscredential]::new($username, $securePassword)
$session = New-PSSession -ComputerName $computerName -Credential $credential -Authentication Negotiate

try
{
    # Fast path: check if the correct version is already installed on the remote machine.
    # If yes, skip the expensive file copy and re-install entirely.
    $remoteCheck = Invoke-Command -Session $session -ArgumentList $packageName, $localVersion -ScriptBlock {
        param($pkgName, $version)
        $pkg = Get-AppxPackage -Name ($pkgName + '*') | Select-Object -First 1
        if ($null -ne $pkg -and $pkg.Version -eq $version -and $pkg.Status -eq 'Ok')
        {
            $pkg.PackageFamilyName
        }
        else
        {
            ''
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($remoteCheck))
    {
        # Already installed at the correct version — return AUMID immediately.
        $remoteCheck + '!App'
    }
    else
    {
        # Slow path: copy files to remote staging and install.
        $stagingDirectory = 'C:\ProgramData\MarkUpUiTestsPackage'
        $remotePkgDirName = [IO.Path]::GetFileName($localPackageDirectory)
        $remotePkgDir = Join-Path $stagingDirectory $remotePkgDirName

        Invoke-Command -Session $session -ArgumentList $stagingDirectory, $remotePkgDir -ScriptBlock {
            param($staging, $pkgDir)
            New-Item -Path $staging -ItemType Directory -Force | Out-Null
            if (Test-Path -LiteralPath $pkgDir) { Remove-Item -LiteralPath $pkgDir -Recurse -Force }
        }

        # Copy the entire package directory from local machine to remote staging over WinRM
        Copy-Item -Path $localPackageDirectory -Destination $stagingDirectory -ToSession $session -Recurse -Force

        # Run the install on the remote machine
        $aumid = Invoke-Command -Session $session -ArgumentList $stagingDirectory, $remotePkgDirName, $packageName, $username -ScriptBlock {
            param($stagingDirectory, $pkgDirName, $packageName, $winRmUsername)
            $ErrorActionPreference = 'Stop'

            $localPackageDirectory = Join-Path $stagingDirectory $pkgDirName

            # Resolve local paths
            $localMsixPath = Get-ChildItem -LiteralPath $localPackageDirectory -Filter '*.msix' -File |
                Where-Object { $_.Name -notmatch 'Dependencies' } |
                Select-Object -First 1 -ExpandProperty FullName
            if (-not $localMsixPath) { throw "No .msix file found in $localPackageDirectory" }

            $localCerFiles = @(Get-ChildItem -LiteralPath $localPackageDirectory -Filter '*.cer' -File | Select-Object -ExpandProperty FullName)
            $depDir = Join-Path $localPackageDirectory 'Dependencies\x64'
            $localDepPaths = @()
            if (Test-Path -LiteralPath $depDir)
            {
                $localDepPaths = @(Get-ChildItem -LiteralPath $depDir -Filter '*.msix' -File | Select-Object -ExpandProperty FullName)
            }

            # Write the install script that the scheduled task will run
            $installScriptPath = Join-Path $stagingDirectory 'Install-Package.ps1'
            $exitCodePath = Join-Path $stagingDirectory 'install.exitcode.txt'
            $logPath = Join-Path $stagingDirectory 'install.log'

            # Build the script content as an array of lines to avoid heredoc escaping issues
            $scriptLines = @()
            $scriptLines += '$ErrorActionPreference = ''Stop'''
            $scriptLines += 'try {'
            foreach ($cer in $localCerFiles)
            {
                $scriptLines += "    certutil.exe -addstore TrustedPeople '$cer' 2>&1 | Out-Null"
            }

            $depArgs = ''
            if ($localDepPaths.Count -gt 0)
            {
                $depList = ($localDepPaths | ForEach-Object { "'$_'" }) -join ','
                $depArgs = " -DependencyPath @($depList)"
            }

            $scriptLines += "    Add-AppxPackage -Path '$localMsixPath'$depArgs -ForceUpdateFromAnyVersion -ForceApplicationShutdown -ErrorAction Stop"
            $scriptLines += "    Set-Content -LiteralPath '$exitCodePath' -Value '0' -Encoding UTF8 -Force"
            $scriptLines += '} catch {'
            $scriptLines += "    `$_ | Out-String | Set-Content -LiteralPath '$logPath' -Encoding UTF8 -Force"
            $scriptLines += "    Set-Content -LiteralPath '$exitCodePath' -Value '1' -Encoding UTF8 -Force"
            $scriptLines += '}'

            $scriptLines | Set-Content -LiteralPath $installScriptPath -Encoding UTF8 -Force

            # Clean up previous run artifacts
            Remove-Item -LiteralPath $exitCodePath -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $logPath -Force -ErrorAction SilentlyContinue

            # Launch the install script as a scheduled task under the interactive user token
            $taskName = 'MarkUpUiTestsInstall-' + [Guid]::NewGuid().ToString('N')
            $taskAction = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument ('-NoLogo -NoProfile -ExecutionPolicy Bypass -File "' + $installScriptPath + '"')
            $taskPrincipal = New-ScheduledTaskPrincipal -UserId $winRmUsername -LogonType Interactive -RunLevel Highest
            Register-ScheduledTask -TaskName $taskName -Action $taskAction -Principal $taskPrincipal -Force | Out-Null

            try
            {
                Start-ScheduledTask -TaskName $taskName
                $timeoutAt = (Get-Date).AddMinutes(5)
                while (-not (Test-Path -LiteralPath $exitCodePath) -and (Get-Date) -lt $timeoutAt)
                {
                    Start-Sleep -Seconds 3
                }
            }
            finally
            {
                Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
            }

            if (-not (Test-Path -LiteralPath $exitCodePath))
            {
                throw 'Timed out waiting for package install scheduled task to complete.'
            }

            $installExitCode = (Get-Content -LiteralPath $exitCodePath -Raw).Trim()
            if ($installExitCode -ne '0')
            {
                $installLog = if (Test-Path -LiteralPath $logPath) { (Get-Content -LiteralPath $logPath -Raw).Trim() } else { '' }
                if ([string]::IsNullOrWhiteSpace($installLog))
                {
                    throw "Package install failed with exit code $installExitCode."
                }
                throw $installLog
            }

            # Verify the package is registered
            $packageFamilyName = Get-AppxPackage -Name ($packageName + '*') | Select-Object -First 1 -ExpandProperty PackageFamilyName
            if ([string]::IsNullOrWhiteSpace($packageFamilyName))
            {
                throw 'Installed package family name could not be resolved.'
            }

            $packageFamilyName + '!App'
        }

        $aumid
    }
}
finally
{
    Remove-PSSession -Session $session -ErrorAction SilentlyContinue
}
""";
    }

    private static string ExecuteRemotePackageInstall(string localPackageDirectory, string localPackageVersion, TimeSpan timeout)
    {
        string script = BuildRemotePackageInstallScript(localPackageDirectory, localPackageVersion);
        string encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        using var ps = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        ps.Start();
        string stdout = ps.StandardOutput.ReadToEnd();
        string stderr = ps.StandardError.ReadToEnd();

        if (!ps.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try { ps.Kill(entireProcessTree: true); } catch { }
            throw new InvalidOperationException("Timed out waiting for remote package installation to complete.");
        }

        if (ps.ExitCode != 0)
        {
            string error = string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
                ? $"Remote package installation failed with exit code {ps.ExitCode}."
                : error);
        }

        string? installedAumid = stdout
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .LastOrDefault(line => line.Contains("!App", StringComparison.Ordinal));

        return installedAumid
            ?? throw new InvalidOperationException("Remote package installation did not return an installed app AUMID.");
    }

    private static string EscapePowerShellSingleQuotedString(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    private static IReadOnlyList<string> GetRemoteAppTargets(string? installedAumid)
    {
        var targets = new List<string>();
        AddRemoteAppTarget(targets, Environment.GetEnvironmentVariable(RemoteAppEnvironmentVariable));
        AddRemoteAppTarget(targets, Environment.GetEnvironmentVariable(RemoteAppAumidEnvironmentVariable));
        AddRemoteAppTarget(targets, installedAumid);
        AddRemoteAppTarget(targets, ResolveAumid() ?? RemoteAppAumidDefault);
        AddRemoteAppTarget(targets, Environment.GetEnvironmentVariable(RemoteAppPathEnvironmentVariable));
        AddRemoteAppTarget(targets, RemoteAppPathDefault);
        return targets;
    }

    private static void AddRemoteAppTarget(List<string> targets, string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return;
        foreach (var existing in targets)
            if (string.Equals(existing, target, StringComparison.OrdinalIgnoreCase)) return;
        targets.Add(target);
    }

    private static void CleanupRemoteSessions()
    {
        try { Session?.Quit(); } catch { }
        Session = null;
        try { DesktopSession?.Quit(); } catch { }
        DesktopSession = null;
    }

    private static nint WaitForAppWindow(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var hwnd = FindAppWindowHandle();
            if (hwnd != 0) return hwnd;
            Thread.Sleep(500);
        }
        throw new TimeoutException(
            $"App window with title ending '{AppWindowTitleSuffix}' did not appear within {timeout.TotalSeconds}s.");
    }

    private static WindowsDriver CreateAppSession(nint hwnd)
    {
        var options = new AppiumOptions();
        options.AutomationName = "Windows";
        options.PlatformName   = "Windows";
        options.AddAdditionalAppiumOption("appTopLevelWindow", $"0x{hwnd:X}");
        return new WindowsDriver(new Uri(WinAppDriverUrl), options, TimeSpan.FromSeconds(120));
    }

    private static WindowsDriver CreateDesktopSession()
    {
        var options = new AppiumOptions();
        options.AutomationName = "Windows";
        options.PlatformName   = "Windows";
        options.App            = "Root";
        return new WindowsDriver(new Uri(WinAppDriverUrl), options, TimeSpan.FromSeconds(120));
    }

    /// <summary>
    /// Creates a WinAppDriver session on the remote machine using the supplied <c>app</c> capability.
    /// The value may be a packaged app AUMID or an executable path valid on the remote machine.
    /// </summary>
    private static WindowsDriver CreateRemoteAppSession(string appId)
    {
        var options = new AppiumOptions();
        options.AutomationName = "Windows";
        options.PlatformName   = "Windows";
        options.DeviceName     = "WindowsPC";
        options.App            = appId;
        return new WindowsDriver(new Uri(DriverUrl), options, TimeSpan.FromSeconds(120));
    }

    /// <summary>Creates a desktop-root session connected to the remote WinAppDriver endpoint.</summary>
    private static WindowsDriver CreateRemoteDesktopSession()
    {
        var options = new AppiumOptions();
        options.AutomationName = "Windows";
        options.PlatformName   = "Windows";
        options.App            = "Root";
        return new WindowsDriver(new Uri(DriverUrl), options, TimeSpan.FromSeconds(120));
    }

    private static AppiumElement? TryFindByFallbackName(string automationId)
    {
        var name = GetFallbackName(automationId);
        if (string.IsNullOrWhiteSpace(name) || Session is null) return null;
        try { return Session.FindElement(MobileBy.Name(name)); }
        catch { return null; }
    }

    private static string? GetFallbackName(string automationId) => automationId switch
    {
        "MenuBarFile"          => "File",
        "MenuBarEdit"          => "Edit",
        "MenuBarFormat"        => "Format",
        "MenuBarView"          => "View",
        "MenuBarHelp"          => "Help",
        "MenuNew"              => "New",
        "MenuOpen"             => "Open...",
        "MenuSave"             => "Save",
        "MenuSaveAs"           => "Save As...",
        "MenuExport"           => "Export",
        "MenuExportHtml"       => "Export as HTML...",
        "MenuExportPlainText"  => "Export as Plain Text...",
        "MenuExportPdf"        => "Export as PDF...",
        "MenuPrint"            => "Print...",
        "MenuExit"             => "Exit",
        "MenuUndo"             => "Undo",
        "MenuRedo"             => "Redo",
        "MenuCut"              => "Cut",
        "MenuCopy"             => "Copy",
        "MenuPaste"            => "Paste",
        "MenuSelectAll"        => "Select All",
        "MenuFind"             => "Find & Replace...",
        "MenuBold"             => "Bold",
        "MenuItalic"           => "Italic",
        "MenuStrikethrough"    => "Strikethrough",
        "MenuInlineCode"       => "Inline Code",
        "MenuHeading"          => "Heading",
        "MenuHeading1"         => "Heading 1",
        "MenuHeading2"         => "Heading 2",
        "MenuHeading3"         => "Heading 3",
        "MenuHeading4"         => "Heading 4",
        "MenuHeading5"         => "Heading 5",
        "MenuHeading6"         => "Heading 6",
        "MenuHorizontalRule"   => "Horizontal Rule",
        "MenuUnorderedList"    => "Bullet List",
        "MenuOrderedList"      => "Numbered List",
        "MenuTaskList"         => "Task List",
        "MenuBlockquote"       => "Blockquote",
        "MenuCodeBlock"        => "Code Block",
        "MenuInsertLink"       => "Insert Link",
        "MenuInsertImage"      => "Insert Image",
        "MenuInsertTable"      => "Insert Table",
        "MenuViewEditor"       => "Editor Only",
        "MenuViewPreview"      => "Preview Only",
        "MenuViewSplit"        => "Split View",
        "MenuToggleWordWrap"   => "Toggle Word Wrap",
        "MenuZoomIn"           => "Zoom In",
        "MenuZoomOut"          => "Zoom Out",
        "MenuZoomReset"        => "Reset Zoom",
        "MenuToggleStatusBar"  => "Toggle Status Bar",
        "MenuMarkdownRef"      => "Markdown Reference",
        "MenuAbout"            => "About MarkUp",
        _                      => null
    };

    // ─────────────────────────────────────────────────────────────────────────
    //  COM activation
    // ─────────────────────────────────────────────────────────────────────────

    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments, uint options, out uint processId);
        int ActivateForFile([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            nint pSFI, [MarshalAs(UnmanagedType.LPWStr)] string verb, out uint processId);
        int ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            nint eventArgs, out uint processId);
    }

    [ComImport, Guid("45ba127d-10a8-46ea-8ab7-56ea9078943c"), ClassInterface(ClassInterfaceType.None)]
    private class ApplicationActivationManagerClass { }

    private static int ActivateApplication(string aumid)
    {
        var mgr = (IApplicationActivationManager)Activator.CreateInstance(typeof(ApplicationActivationManagerClass))!;
        int hr = mgr.ActivateApplication(aumid, null, 0, out uint pid);
        if (hr < 0) throw new InvalidOperationException($"ActivateApplication failed: 0x{hr:X8}");
        return (int)pid;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Win32
    // ─────────────────────────────────────────────────────────────────────────

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    private const int    SW_RESTORE      = 9;
    private const ushort VK_ESCAPE       = 0x1B;
    private const ushort VK_RETURN       = 0x0D;
    private const ushort VK_DELETE       = 0x2E;
    private const ushort VK_DOWN         = 0x28;
    private const ushort VK_RIGHT        = 0x27;
    private const ushort VK_ADD          = 0x6B;
    private const ushort VK_SUBTRACT     = 0x6D;

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc fn, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(nint hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hWnd);

    private static nint FindAppWindowHandle()
    {
        foreach (var process in Process.GetProcessesByName("MarkUp Markdown Editor"))
        {
            try
            {
                process.Refresh();
                if (process.MainWindowHandle != 0)
                    return process.MainWindowHandle;
            }
            catch { }
        }

        return FindTopLevelWindowByTitleSuffix(AppWindowTitleSuffix);
    }

    private static nint FindTopLevelWindowByTitleSuffix(string suffix)
    {
        nint found = 0;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            var title = GetWindowTitle(hWnd);
            if (title.EndsWith(suffix, StringComparison.Ordinal)) { found = hWnd; return false; }
            return true;
        }, 0);
        return found;
    }

    private static string GetWindowTitle(nint hWnd)
    {
        int len = GetWindowTextLength(hWnd);
        if (len <= 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
