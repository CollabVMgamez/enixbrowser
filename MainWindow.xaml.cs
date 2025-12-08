using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace EnixBrowser;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly string _homeUrl = "https://duckduckgo.com";
    private readonly List<Bookmark> _bookmarks = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        InitializeBookmarks();
    }

    #region Initialization

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Ensure WebView2 is ready
            await BrowserView.EnsureCoreWebView2Async();

            // Basic WebView2 settings
            if (BrowserView.CoreWebView2 is not null)
            {
                var settings = BrowserView.CoreWebView2.Settings;
                settings.AreDefaultContextMenusEnabled = true;
                settings.AreDevToolsEnabled = true;

                // Append a custom user agent identifier for EnixBrowser
                try
                {
                    var defaultUserAgent = settings.UserAgent;
                    settings.UserAgent = string.IsNullOrWhiteSpace(defaultUserAgent)
                        ? "EnixBrowser/1.0"
                        : $"{defaultUserAgent} EnixBrowser/1.0";
                }
                catch
                {
                    // Ignore failures when setting the user agent
                }
            }

            NavigateTo(_homeUrl);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to initialize WebView2 runtime.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "Enix Browser",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void InitializeBookmarks()
    {
        _bookmarks.Clear();

        _bookmarks.AddRange(new[]
        {
            new Bookmark("ChatGPT",        "https://chatgpt.com/"),
            new Bookmark("Claude",         "https://claude.ai/"),
            new Bookmark("DeepSeek",       "https://www.deepseek.com/"),
            new Bookmark("Gemini",         "https://gemini.google.com/"),
            new Bookmark("Collab VM",      "https://computernewb.com/collab-vm/")
        });

        BookmarkBar.ItemsSource = _bookmarks;
    }

    #endregion

    #region Navigation helpers

    private void NavigateTo(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || BrowserView.CoreWebView2 is null)
            return;

        var text = input.Trim();
        string targetUrl;

        if (IsProbablyUrl(text))
        {
            var url = text;

            // If it's not an absolute URI, treat as https URL
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                url = "https://" + url;
            }

            targetUrl = url;
        }
        else
        {
            var query = Uri.EscapeDataString(text);
            targetUrl = $"https://chatgpt.com/?q={query}";
        }

        AddressBar.Text = targetUrl;

        try
        {
            BrowserView.CoreWebView2.Navigate(targetUrl);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Navigation failed: {ex.Message}";
        }
    }

    private static bool IsProbablyUrl(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();

        // If there's whitespace, treat as a chat/query instead of a URL
        if (text.Any(char.IsWhiteSpace))
            return false;

        // Explicit scheme like http:// or https://
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return true;
        }

        // Heuristic: dot + plausible TLD (e.g., example.com)
        if (!text.Contains('.'))
            return false;

        var lastDot = text.LastIndexOf('.');
        if (lastDot <= 0 || lastDot == text.Length - 1)
            return false;

        var tld = text[(lastDot + 1)..];
        if (tld.Length is < 2 or > 24)
            return false;

        return true;
    }

    #endregion

    #region UI event handlers

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (BrowserView.CanGoBack)
        {
            BrowserView.GoBack();
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (BrowserView.CanGoForward)
        {
            BrowserView.GoForward();
        }
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BrowserView.Reload();
        }
        catch
        {
            // ignore if reload is not possible yet
        }
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(_homeUrl);
    }

    private void AddressBar_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateTo(AddressBar.Text);
            e.Handled = true;
        }
    }

    private void BookmarkBarItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string url)
        {
            NavigateTo(url);
        }
    }

    private void AddBookmarkButton_Click(object sender, RoutedEventArgs e)
    {
        var currentUrl = BrowserView.Source?.ToString() ?? AddressBar.Text;
        if (string.IsNullOrWhiteSpace(currentUrl))
            return;

        var title = BrowserView.CoreWebView2?.DocumentTitle;
        if (string.IsNullOrWhiteSpace(title))
            title = currentUrl;

        // Avoid duplicates by URL
        if (_bookmarks.Any(b => string.Equals(b.Url, currentUrl, StringComparison.OrdinalIgnoreCase)))
            return;

        _bookmarks.Add(new Bookmark(title, currentUrl));
        BookmarkBar.Items.Refresh();
    }

    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        // Placeholder: history UI will be implemented later
        MessageBox.Show("History view is not implemented yet.", "Enix Browser",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void ChatWithPageButton_Click(object sender, RoutedEventArgs e)
    {
        await TriggerChatWithPageAsync();
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Cmd+K equivalent on Windows: Ctrl+K
        if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            await TriggerChatWithPageAsync();
        }
    }

    private void ToggleSandboxButton_Click(object sender, RoutedEventArgs e)
    {
        if (SandboxPanel.Visibility == Visibility.Visible)
        {
            SandboxPanel.Visibility = Visibility.Collapsed;
            StatusText.Text = "Sandbox hidden";
        }
        else
        {
            SandboxPanel.Visibility = Visibility.Visible;
            StatusText.Text = "Sandbox visible";
        }
    }

    private async void PreviewCodeButton_Click(object sender, RoutedEventArgs e)
    {
        var code = CodeEditor.Text;
        if (string.IsNullOrWhiteSpace(code))
        {
            StatusText.Text = "Sandbox: paste some HTML/CSS/JS code to preview.";
            return;
        }

        try
        {
            await PreviewView.EnsureCoreWebView2Async();
            if (PreviewView.CoreWebView2 is null)
                return;

            var html = BuildHtmlPreviewDocument(code);
            PreviewView.CoreWebView2.NavigateToString(html);
            StatusText.Text = "Sandbox preview updated.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Sandbox preview failed: {ex.Message}";
        }
    }

    private void ClearCodeButton_Click(object sender, RoutedEventArgs e)
    {
        CodeEditor.Clear();

        try
        {
            if (PreviewView.CoreWebView2 is not null)
            {
                PreviewView.CoreWebView2.NavigateToString("<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body></body></html>");
            }
        }
        catch
        {
            // ignore
        }

        StatusText.Text = "Sandbox cleared.";
    }

    private async Task TriggerChatWithPageAsync()
    {
        try
        {
            if (BrowserView.CoreWebView2 is null)
            {
                await BrowserView.EnsureCoreWebView2Async();
            }

            if (BrowserView.CoreWebView2 is null)
                return;

            // Extract main text content from the current page
            const string script = @"(() => {
    try {
        if (document.body) {
            return document.body.innerText || '';
        }
        if (document.documentElement) {
            return document.documentElement.innerText || '';
        }
        return '';
    } catch {
        return '';
    }
})()";

            var rawResult = await BrowserView.CoreWebView2.ExecuteScriptAsync(script);
            if (string.IsNullOrWhiteSpace(rawResult))
                return;

            // WebView2 returns JSON-encoded values; decode safely
            string? pageText = null;
            try
            {
                using var doc = JsonDocument.Parse(rawResult);
                if (doc.RootElement.ValueKind == JsonValueKind.String)
                {
                    pageText = doc.RootElement.GetString();
                }
            }
            catch
            {
                // If parsing fails, fall back to rawResult with quotes trimmed
                pageText = rawResult.Trim('"');
            }

            if (string.IsNullOrWhiteSpace(pageText))
                return;

            // Limit size so the URL does not explode
            const int maxLength = 4000;
            if (pageText.Length > maxLength)
            {
                pageText = pageText[..maxLength] + "...";
            }

            // Intent: feed the page content into ChatGPT as context
            var payload = $"PAGE_CONTENT:\\n\\n{pageText}";
            var targetUrl = "https://chatgpt.com/?q=" + Uri.EscapeDataString(payload);

            AddressBar.Text = targetUrl;
            BrowserView.CoreWebView2.Navigate(targetUrl);
            StatusText.Text = "Opening ChatGPT with current page content...";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Chat with page failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Build an HTML document suitable for live preview from a snippet of HTML/CSS/JS.
    /// Uses regex/intention heuristics to decide how to wrap the code.
    /// </summary>
    private static string BuildHtmlPreviewDocument(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body></body></html>";
        }

        var trimmed = code.Trim();

        // Intent detection: full HTML document vs snippet vs style-only
        var looksLikeDoctype = Regex.IsMatch(trimmed, @"<!DOCTYPE\s+html", RegexOptions.IgnoreCase);
        var looksLikeHtmlTag = Regex.IsMatch(trimmed, @"<\s*html[^>]*>", RegexOptions.IgnoreCase);

        if (looksLikeDoctype || looksLikeHtmlTag)
        {
            // Already a full HTML document, just return it as-is
            return trimmed;
        }

        // Heuristic: looks like standalone CSS (no tags but has braces/semicolons)
        var looksLikeCss = !trimmed.Contains("<") &&
                           Regex.IsMatch(trimmed, @"[{};]");

        if (looksLikeCss)
        {
            var builderCss = new StringBuilder();
            builderCss.AppendLine("<!DOCTYPE html>");
            builderCss.AppendLine("<html>");
            builderCss.AppendLine("<head>");
            builderCss.AppendLine("<meta charset=\"utf-8\">");
            builderCss.AppendLine("<title>CSS Preview</title>");
            builderCss.AppendLine("<style>");
            builderCss.AppendLine(trimmed);
            builderCss.AppendLine("</style>");
            builderCss.AppendLine("</head>");
            builderCss.AppendLine("<body>");
            builderCss.AppendLine("<div>CSS preview (no HTML markup was provided).</div>");
            builderCss.AppendLine("</body>");
            builderCss.AppendLine("</html>");
            return builderCss.ToString();
        }

        // Default: treat as HTML fragment (which may embed JS)
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html>");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\">");
        builder.AppendLine("<title>Live Preview</title>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine(trimmed);
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    #endregion

    #region WebView2 events

    private void BrowserView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        StatusText.Text = $"Loading {e.Uri}...";
    }

    private void BrowserView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            var uri = BrowserView.Source?.ToString() ?? string.Empty;
            AddressBar.Text = uri;

            var title = BrowserView.CoreWebView2?.DocumentTitle;
            Title = string.IsNullOrWhiteSpace(title)
                ? "Enix Browser"
                : $"Enix Browser - {title}";

            StatusText.Text = "Done";
        }
        else
        {
            StatusText.Text = $"Navigation error: {e.WebErrorStatus}";
        }
    }

    #endregion
}

public sealed class Bookmark
{
    public string Title { get; }
    public string Url { get; }

    public Bookmark(string title, string url)
    {
        Title = title;
        Url = url;
    }
}