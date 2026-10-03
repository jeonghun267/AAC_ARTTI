using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public sealed class LatestMenuManualPrice
{
    public string brand;
    public string menuName;
    public string price;
}

[Serializable]
public sealed class OfficialMenuEntry
{
    public string menuName;
    public string description;
    public string imageUrl;
    // Prices deliberately do not belong to downloaded data or the refresh cache.
}

[Serializable]
public sealed class OfficialMenuSnapshot
{
    public int version = 1;
    public string brand;
    public string fetchedAtUtc;
    public List<OfficialMenuEntry> menus;
}

// Public website HTML, not an external-developer API. If the websites change their
// markup or reject a request, the last successful cache remains available.
// This is a plain helper: no new scene component or UI object is required.
public static class OfficialMenuCatalog
{
    public const int MaximumMenus = 14;
    public const int RefreshIntervalDays = 14;
    public const string PricePending = "가격 확인 중";

    private const string MegaCoffeeUrl = "https://www.mega-mgccoffee.com/menu/menu.php?page=1&menu_category1=1&menu_category2=1&category=1&list_checkbox_all=";
    private const string TenCoffeeUrl = "https://tenpercentcoffee.com/bbs/board.php?bo_table=sub23";
    private const string TenSignatureUrl = "https://tenpercentcoffee.com/bbs/board.php?bo_table=sub22";
    private const int TimeoutSeconds = 15;
    private const int MaximumHtmlLength = 2 * 1024 * 1024;
    private const int MaximumImageBytes = 8 * 1024 * 1024;

    public static string MenuKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return Regex.Replace(name.Normalize(NormalizationForm.FormKC), @"\s+", string.Empty).ToUpperInvariant();
    }

    public static bool IsRefreshDue(OfficialMenuSnapshot snapshot, DateTime utcNow)
    {
        if (snapshot == null || snapshot.menus == null || snapshot.menus.Count == 0
            || !DateTimeOffset.TryParse(snapshot.fetchedAtUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out DateTimeOffset fetched)) return true;
        DateTime now = utcNow.ToUniversalTime();
        return fetched.UtcDateTime > now || now - fetched.UtcDateTime >= TimeSpan.FromDays(RefreshIntervalDays);
    }

    public static string CachePath(string brand)
    {
        string file = brand == LatestMenuController.MegaCoffeeBrand ? "mega.json"
            : brand == LatestMenuController.TenPercentCoffeeBrand ? "tenpercent.json"
            : throw new ArgumentException("Unsupported menu brand.", nameof(brand));
        return Path.Combine(Application.persistentDataPath, "latest-menu-cache", file);
    }

    public static OfficialMenuSnapshot ReadCache(string brand)
    {
        try
        {
            string path = CachePath(brand);
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (json.Length > MaximumHtmlLength) return null;
            OfficialMenuSnapshot snapshot = JsonUtility.FromJson<OfficialMenuSnapshot>(json);
            if (snapshot == null || snapshot.version != 1 || snapshot.brand != brand) return null;
            snapshot.menus = CleanEntries(snapshot.menus, brand);
            return snapshot.menus.Count > 0 ? snapshot : null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[LatestMenu] 메뉴 캐시 읽기 실패: {exception.Message}");
            return null;
        }
    }

    public static void SaveCache(OfficialMenuSnapshot snapshot)
    {
        List<OfficialMenuEntry> clean = CleanEntries(snapshot?.menus, snapshot?.brand);
        if (clean.Count == 0) throw new InvalidDataException("No valid official menu data.");
        snapshot.menus = clean;
        string path = CachePath(snapshot.brand);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonUtility.ToJson(snapshot), Encoding.UTF8);
            // Never truncate the previous successful JSON while writing a new response.
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static string ImageCachePath(string imageUrl)
    {
        using (SHA256 hash = SHA256.Create())
        {
            string name = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(imageUrl)))
                .Replace("-", string.Empty).ToLowerInvariant();
            return Path.Combine(Application.persistentDataPath, "latest-menu-cache", "images", name + ".img");
        }
    }

    public static byte[] ReadImage(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl)) return null;
        try
        {
            string path = ImageCachePath(imageUrl);
            if (!File.Exists(path) || new FileInfo(path).Length > MaximumImageBytes) return null;
            return File.ReadAllBytes(path);
        }
        catch (Exception) { return null; }
    }

    public static async UniTask<OfficialMenuSnapshot> FetchAsync(string brand, CancellationToken token)
    {
        List<OfficialMenuEntry> entries;
        if (brand == LatestMenuController.MegaCoffeeBrand)
        {
            entries = ParseMegaHtml(await DownloadHtmlAsync(MegaCoffeeUrl, token));
        }
        else if (brand == LatestMenuController.TenPercentCoffeeBrand)
        {
            List<OfficialMenuEntry> coffee = ParseTenPercentHtml(await DownloadHtmlAsync(TenCoffeeUrl, token));
            List<OfficialMenuEntry> signature = ParseTenPercentHtml(await DownloadHtmlAsync(TenSignatureUrl, token));
            if (coffee.Count == 0 || signature.Count == 0)
                throw new InvalidDataException("Ten Percent menu markup could not be read.");
            coffee.AddRange(signature);
            entries = coffee;
        }
        else throw new ArgumentException("Unsupported menu brand.", nameof(brand));

        entries = CleanEntries(entries, brand);
        if (entries.Count == 0) throw new InvalidDataException("Official menu markup could not be read.");
        await CacheImagesAsync(entries, token);
        token.ThrowIfCancellationRequested();
        return new OfficialMenuSnapshot
        {
            brand = brand,
            fetchedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            menus = entries
        };
    }

    public static bool HasMissingImages(OfficialMenuSnapshot snapshot)
    {
        return snapshot?.menus != null && snapshot.menus.Any(entry =>
            !string.IsNullOrEmpty(entry.imageUrl) && ReadImage(entry.imageUrl) == null);
    }

    public static async UniTask CacheImagesAsync(List<OfficialMenuEntry> entries, CancellationToken token)
    {
        // Bounded batches: three photos at a time; no request is made for unused cards 15~20.
        for (int start = 0; start < Math.Min(entries.Count, MaximumMenus); start += 3)
        {
            var batch = new List<UniTask>();
            for (int i = start; i < Math.Min(start + 3, Math.Min(entries.Count, MaximumMenus)); i++)
                batch.Add(CacheImageAsync(entries[i].imageUrl, token));
            await UniTask.WhenAll(batch);
        }
    }

    private static async UniTask<string> DownloadHtmlAsync(string url, CancellationToken token)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = TimeoutSeconds;
            await request.SendWebRequest().ToUniTask(cancellationToken: token);
            if (request.result != UnityWebRequest.Result.Success)
                throw new IOException($"Official menu HTTP {request.responseCode}: {request.error}");
            string html = request.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(html) || html.Length > MaximumHtmlLength)
                throw new InvalidDataException("Invalid official menu response.");
            return html;
        }
    }

    private static async UniTask CacheImageAsync(string url, CancellationToken token)
    {
        if (string.IsNullOrEmpty(url)) return;
        string path = ImageCachePath(url);
        if (ReadImage(url) != null && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < TimeSpan.FromDays(RefreshIntervalDays)) return;
        try
        {
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url, true))
            {
                request.timeout = TimeoutSeconds;
                await request.SendWebRequest().ToUniTask(cancellationToken: token);
                if (request.result != UnityWebRequest.Result.Success)
                    throw new IOException($"HTTP {request.responseCode}");
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                try
                {
                    byte[] data = request.downloadHandler.data;
                    if (texture == null || data == null || data.Length == 0 || data.Length > MaximumImageBytes)
                        throw new InvalidDataException("Invalid menu photo.");
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, data);
                }
                finally { if (texture != null) UnityEngine.Object.Destroy(texture); }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            // A photo failure does not remove menu names or overwrite manual prices.
            Debug.LogWarning($"[LatestMenu] 메뉴 사진 조회 실패: {exception.Message}");
        }
    }

    public static List<OfficialMenuEntry> CleanEntries(IEnumerable<OfficialMenuEntry> source, string brand)
    {
        var result = new List<OfficialMenuEntry>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (source == null) return result;
        foreach (OfficialMenuEntry entry in source)
        {
            string key = MenuKey(entry?.menuName);
            if (key.Length == 0 || entry.menuName.Length > 160 || !names.Add(key)) continue;
            result.Add(new OfficialMenuEntry
            {
                menuName = entry.menuName.Trim(),
                description = entry.description?.Trim() ?? string.Empty,
                imageUrl = IsAllowedImage(entry.imageUrl, brand) ? entry.imageUrl : string.Empty
            });
            if (result.Count == MaximumMenus) break;
        }
        return result;
    }

    private static bool IsAllowedImage(string url, string brand)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps) return false;
        return brand == LatestMenuController.MegaCoffeeBrand
            ? uri.Host == "img.79plus.co.kr" || uri.Host == "www.mega-mgccoffee.com"
            : brand == LatestMenuController.TenPercentCoffeeBrand && uri.Host == "tenpercentcoffee.com";
    }

    public static List<OfficialMenuEntry> ParseMegaHtml(string html)
    {
        HtmlNode list = ParseHtml(html).Find(n => n.Attribute("id") == "menu_list").FirstOrDefault();
        var result = new List<OfficialMenuEntry>();
        if (list == null) return result;
        foreach (HtmlNode card in list.children.Where(n => n.tag == "li"))
        {
            HtmlNode name = card.Find(n => n.tag == "b").FirstOrDefault();
            HtmlNode description = card.Find(n => n.HasClass("text2")).FirstOrDefault();
            HtmlNode image = card.Find(n => n.tag == "img").FirstOrDefault();
            if (name == null) continue;
            result.Add(new OfficialMenuEntry
            {
                menuName = name.Text(), description = description?.Text() ?? string.Empty,
                imageUrl = ResolveImage(image?.Attribute("src"), MegaCoffeeUrl)
            });
        }
        return result;
    }

    public static List<OfficialMenuEntry> ParseTenPercentHtml(string html)
    {
        var result = new List<OfficialMenuEntry>();
        foreach (HtmlNode card in ParseHtml(html).Find(n => n.HasClass("bx-grandi")))
        {
            HtmlNode title = card.Find(n => n.tag == "span" && n.Attribute("onclick").Contains("wr_id=")).FirstOrDefault();
            HtmlNode image = card.Find(n => n.tag == "img").FirstOrDefault();
            HtmlNode original = card.Find(n => n.tag == "a" && n.HasClass("big")).FirstOrDefault();
            string name = title?.Text() ?? image?.Attribute("title");
            if (string.IsNullOrWhiteSpace(name)) continue;
            result.Add(new OfficialMenuEntry
            {
                menuName = name, description = string.Empty,
                imageUrl = ResolveImage(original?.Attribute("href") ?? image?.Attribute("src"), TenCoffeeUrl)
            });
        }
        return result;
    }

    private static string ResolveImage(string value, string page)
    {
        return !string.IsNullOrWhiteSpace(value) && Uri.TryCreate(new Uri(page), value, out Uri uri)
            ? uri.AbsoluteUri : string.Empty;
    }

    // Small HTML reader for the two verified public layouts. In particular, Mega's
    // nested nutrition <li> elements must never be treated as additional menu cards.
    private sealed class HtmlNode
    {
        public string tag;
        public HtmlNode parent;
        public readonly Dictionary<string, string> attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<HtmlNode> children = new List<HtmlNode>();
        public readonly List<object> content = new List<object>();
        public string Attribute(string name) => attributes.TryGetValue(name, out string value) ? value : string.Empty;
        public bool HasClass(string name) => Attribute("class").Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Contains(name);
        public string Text() => Regex.Replace(WebUtility.HtmlDecode(string.Join(" ", content.Select(x => x is HtmlNode node ? node.Text() : (string)x))), @"\s+", " ").Trim();
        public IEnumerable<HtmlNode> Find(Func<HtmlNode, bool> predicate)
        {
            foreach (HtmlNode child in children)
            {
                if (predicate(child)) yield return child;
                foreach (HtmlNode nested in child.Find(predicate)) yield return nested;
            }
        }
    }

    private static readonly Regex TagPattern = new Regex(@"<(?<close>/)?(?<tag>[A-Za-z][A-Za-z0-9:-]*)(?<attrs>(?:[^""'>]|""[^""]*""|'[^']*')*)>", RegexOptions.None, TimeSpan.FromSeconds(2));
    private static readonly Regex AttributePattern = new Regex(@"(?<key>[A-Za-z_:][A-Za-z0-9_:.-]*)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s>]+))", RegexOptions.None, TimeSpan.FromSeconds(2));
    private static readonly HashSet<string> VoidTags = new HashSet<string> { "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr" };

    private static HtmlNode ParseHtml(string html)
    {
        if (html == null || html.Length > MaximumHtmlLength) throw new InvalidDataException("Invalid menu HTML size.");
        html = Regex.Replace(html, @"<!--[\s\S]*?-->|<(script|style)\b[^>]*>[\s\S]*?</\1\s*>", string.Empty, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
        var root = new HtmlNode { tag = "root" };
        HtmlNode current = root;
        int previous = 0, count = 0, depth = 0;
        foreach (Match match in TagPattern.Matches(html))
        {
            if (++count > 50000) throw new InvalidDataException("Too many HTML elements.");
            current.content.Add(html.Substring(previous, match.Index - previous));
            previous = match.Index + match.Length;
            string tag = match.Groups["tag"].Value.ToLowerInvariant();
            if (match.Groups["close"].Success)
            {
                HtmlNode ancestor = current;
                int skipped = 0;
                while (ancestor.parent != null && ancestor.tag != tag) { ancestor = ancestor.parent; skipped++; }
                if (ancestor.parent != null) { current = ancestor.parent; depth -= skipped + 1; }
                continue;
            }
            var node = new HtmlNode { tag = tag, parent = current };
            foreach (Match attribute in AttributePattern.Matches(match.Groups["attrs"].Value))
                node.attributes[attribute.Groups["key"].Value] = WebUtility.HtmlDecode(attribute.Groups["value"].Value);
            current.children.Add(node);
            current.content.Add(node);
            if (!VoidTags.Contains(tag) && !match.Groups["attrs"].Value.TrimEnd().EndsWith("/", StringComparison.Ordinal))
            {
                if (++depth > 128) throw new InvalidDataException("Menu HTML is too deeply nested.");
                current = node;
            }
        }
        current.content.Add(html.Substring(previous));
        return root;
    }
}
