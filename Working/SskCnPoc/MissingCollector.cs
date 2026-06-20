using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using System.Text.RegularExpressions;

namespace SskCnPoc;

/// <summary>
/// 缺失翻译收集器：收集未翻译的文本并保存到文件
/// </summary>
internal static class MissingCollector
{
    private static readonly HashSet<string> _missing = new(StringComparer.Ordinal);
    private static readonly object _lock = new();
    private static DateTime _lastSaveTime = DateTime.MinValue;
    private static readonly TimeSpan _saveInterval = TimeSpan.FromSeconds(10);
    
    // 常见的动态参数模式（用于规范化收集）
    private static readonly (string prefix, string suffix)[] DynamicPatterns = new[]
    {
        ("(", "%):"),
        ("(", "%)"),
        (" v", ""),
        (" ", " points"),
        (" ", " coins"),
        (" ", "%"),
        (": ", ""),
    };

    // 用于替换数字的正则（用于将动态数字替换为占位符）
    private static readonly Regex _numberRegex = new(@"\d{1,6}(?:[\.,]\d+)?", RegexOptions.Compiled);

    /// <summary>
    /// 收集未翻译的文本
    /// </summary>
    public static void Collect(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (!ShouldCollect(text)) return;
        
        // 跳过已经是中文的文本
        if (text.Any(c => c >= 0x4E00 && c <= 0x9FFF)) return;
        
        // 规范化动态文本
        string normalizedText = NormalizeDynamicText(text);
        
        lock (_lock)
        {
            if (_missing.Add(normalizedText))
            {
                Plugin.LogSrc.LogDebug($"[MISSING] '{normalizedText}'");
                
                if (DateTime.Now - _lastSaveTime > _saveInterval)
                {
                    SaveToFile();
                    _lastSaveTime = DateTime.Now;
                }
            }
        }
    }

    private static bool ShouldCollect(string s)
    {
        if (s.All(char.IsWhiteSpace)) return false;
        if (s.Length <= 1) return false; // 单字符通常为按键绑定或其他非翻译项
        if (IsResolutionFormat(s)) return false;

        // 跳过类似于 URL / 资源路径的字符串
        if (s.Contains("/") || s.Contains("\\")) return false;

        bool allDigitOrPunct = true;
        foreach (char c in s)
        {
            if (char.IsLetter(c) || c > 0x7F) { allDigitOrPunct = false; break; }
            if (char.IsDigit(c)) continue;
            if (char.IsPunctuation(c) || char.IsSymbol(c) || char.IsWhiteSpace(c)) continue;
            allDigitOrPunct = false; break;
        }
        return !allDigitOrPunct;
    }

    private static bool IsResolutionFormat(string s)
    {
        int xIndex = s.IndexOf('x');
        if (xIndex < 0) xIndex = s.IndexOf('×');
        if (xIndex <= 0 || xIndex >= s.Length - 1) return false;

        for (int i = 0; i < xIndex; i++)
        {
            if (!char.IsDigit(s[i])) return false;
        }

        for (int i = xIndex + 1; i < s.Length; i++)
        {
            if (!char.IsDigit(s[i])) return false;
        }

        return true;
    }

    /// <summary>
    /// 规范化动态文本：将动态参数替换为 {0} 占位符
    /// 增强逻辑：使用正则替换数字序列，限定替换次数以避免过度泛化
    /// </summary>
    private static string NormalizeDynamicText(string text)
    {
        // 1) 先使用常见前后缀规则尝试快速规范化
        foreach (var (prefix, suffix) in DynamicPatterns)
        {
            int prefixIdx = text.IndexOf(prefix, StringComparison.Ordinal);
            if (prefixIdx < 0) continue;
            
            int suffixIdx = suffix.Length > 0 
                ? text.IndexOf(suffix, prefixIdx + prefix.Length, StringComparison.Ordinal)
                : -1;
            
            int paramStart = prefixIdx + prefix.Length;
            int paramEnd = suffixIdx >= 0 ? suffixIdx : text.Length;
            
            if (paramEnd > paramStart && paramEnd - paramStart <= 20)
            {
                bool isValidParam = true;
                for (int i = paramStart; i < paramEnd; i++)
                {
                    char c = text[i];
                    if (!char.IsDigit(c) && c != '.' && !char.IsLetter(c))
                    {
                        isValidParam = false;
                        break;
                    }
                }
                
                if (isValidParam)
                {
                    string before = text.Substring(0, paramStart);
                    string after = suffixIdx >= 0 ? text.Substring(suffixIdx) : "";
                    return before + "{0}" + after;
                }
            }
        }

        // 2) 使用数字正则进行替换（限制匹配次数，最多替换3个数字段），以保持可辨识性
        var matches = _numberRegex.Matches(text);
        if (matches.Count > 0 && matches.Count <= 3)
        {
            var sb = new StringBuilder();
            int lastIdx = 0;
            int replIndex = 0;
            foreach (Match m in matches)
            {
                sb.Append(text.Substring(lastIdx, m.Index - lastIdx));
                sb.Append("{" + replIndex + "}");
                lastIdx = m.Index + m.Length;
                replIndex++;
            }
            sb.Append(text.Substring(lastIdx));
            return sb.ToString();
        }

        // 3) 不满足上述条件时，返回原文（后续将作为完整条目保存）
        return text;
    }

    private static void SaveToFile()
    {
        try
        {
            var missingPath = Path.Combine(Paths.PluginPath, "ssk_cn_missing.txt");
            var lines = _missing.OrderBy(s => s).Select(s => $"{s}=");
            File.WriteAllLines(missingPath, lines, Encoding.UTF8);
            Plugin.LogSrc.LogInfo($"[MISSING] Saved {_missing.Count} untranslated texts to ssk_cn_missing.txt");
        }
        catch (Exception ex)
        {
            Plugin.LogSrc.LogWarning($"Failed to save missing translations: {ex.Message}");
        }
    }
}
