using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ELearning.Application.Questions;

/// <summary>Đổi ký tự Unicode trong công thức (MathType, Word) sang LaTeX mà KaTeX hiển thị được.</summary>
public static partial class LatexSymbols
{
    private static readonly Dictionary<int, string> Map = new()
    {
        [0x2212] = "-",
        [0x00B1] = "\\pm",
        [0x2213] = "\\mp",
        [0x00D7] = "\\times",
        [0x00F7] = "\\div",
        [0x22C5] = "\\cdot",
        [0x00B7] = "\\cdot",
        [0x2219] = "\\cdot",
        [0x2217] = "*",
        [0x2218] = "\\circ",
        [0x00B0] = "^{\\circ}",
        [0x2264] = "\\le",
        [0x2265] = "\\ge",
        [0x2A7D] = "\\le",
        [0x2A7E] = "\\ge",
        [0x2260] = "\\ne",
        [0x2248] = "\\approx",
        [0x2261] = "\\equiv",
        [0x223C] = "\\sim",
        [0x2245] = "\\cong",
        [0x221D] = "\\propto",
        [0x226A] = "\\ll",
        [0x226B] = "\\gg",
        [0x221E] = "\\infty",
        [0x2192] = "\\to",
        [0x2190] = "\\leftarrow",
        [0x2194] = "\\leftrightarrow",
        [0x21D2] = "\\Rightarrow",
        [0x21D0] = "\\Leftarrow",
        [0x21D4] = "\\Leftrightarrow",
        [0x2191] = "\\uparrow",
        [0x2193] = "\\downarrow",
        [0x21A6] = "\\mapsto",
        [0x27F9] = "\\Longrightarrow",
        [0x27FA] = "\\Longleftrightarrow",
        [0x27F6] = "\\longrightarrow",
        [0x2208] = "\\in",
        [0x2209] = "\\notin",
        [0x220B] = "\\ni",
        [0x2282] = "\\subset",
        [0x2283] = "\\supset",
        [0x2286] = "\\subseteq",
        [0x2287] = "\\supseteq",
        [0x2284] = "\\not\\subset",
        [0x222A] = "\\cup",
        [0x2229] = "\\cap",
        [0x2205] = "\\varnothing",
        [0x2200] = "\\forall",
        [0x2203] = "\\exists",
        [0x2204] = "\\nexists",
        [0x00AC] = "\\neg",
        [0x2227] = "\\wedge",
        [0x2228] = "\\vee",
        [0x22A5] = "\\perp",
        [0x27C2] = "\\perp",
        [0x2225] = "\\parallel",
        [0x2226] = "\\nparallel",
        [0x2220] = "\\angle",
        [0x2221] = "\\measuredangle",
        [0x25B3] = "\\triangle",
        [0x2206] = "\\Delta",
        [0x25A1] = "\\square",
        [0x25CA] = "\\lozenge",
        [0x2032] = "'",
        [0x2033] = "''",
        [0x2034] = "'''",
        [0x2026] = "\\ldots",
        [0x22EF] = "\\cdots",
        [0x22EE] = "\\vdots",
        [0x22F1] = "\\ddots",
        [0x2202] = "\\partial",
        [0x2207] = "\\nabla",
        [0x211D] = "\\mathbb{R}",
        [0x2115] = "\\mathbb{N}",
        [0x2124] = "\\mathbb{Z}",
        [0x211A] = "\\mathbb{Q}",
        [0x2102] = "\\mathbb{C}",
        [0x2016] = "\\|",
        [0x27E8] = "\\langle",
        [0x27E9] = "\\rangle",
        [0x2329] = "\\langle",
        [0x232A] = "\\rangle",
        [0x221A] = "\\surd",
        [0x2211] = "\\sum",
        [0x220F] = "\\prod",
        [0x222B] = "\\int",
        [0x222C] = "\\iint",
        [0x222D] = "\\iiint",
        [0x222E] = "\\oint",
        [0x2223] = "\\mid",
        [0x2224] = "\\nmid",
        [0x2299] = "\\odot",
        [0x2295] = "\\oplus",
        [0x2297] = "\\otimes",
        [0x2234] = "\\therefore",
        [0x2235] = "\\because",
        [0x2113] = "\\ell",
        [0x210F] = "\\hbar",
        [0x2135] = "\\aleph",
        [0x22C2] = "\\bigcap",
        [0x22C3] = "\\bigcup",
        [0x2216] = "\\setminus",
        [0x2215] = "/",
        [0x2044] = "/",
        [0x00A0] = "\\ ",
        [0x2009] = "\\,",
        [0x200A] = "\\,",
        [0x2002] = "\\enspace",
        [0x2003] = "\\quad",
        [0x2005] = "\\;",
        [0x2006] = "\\,",
        [0x2061] = string.Empty,
        [0x2062] = string.Empty,
        [0x2063] = ",",
        [0x200B] = string.Empty,
        [0x00AD] = string.Empty,
        [0x2018] = "`",
        [0x2019] = "'",
        [0x201C] = "``",
        [0x201D] = "''",
        // Chữ Hy Lạp
        [0x03B1] = "\\alpha",
        [0x03B2] = "\\beta",
        [0x03B3] = "\\gamma",
        [0x03B4] = "\\delta",
        [0x03B5] = "\\varepsilon",
        [0x03F5] = "\\epsilon",
        [0x03B6] = "\\zeta",
        [0x03B7] = "\\eta",
        [0x03B8] = "\\theta",
        [0x03D1] = "\\vartheta",
        [0x03B9] = "\\iota",
        [0x03BA] = "\\kappa",
        [0x03BB] = "\\lambda",
        [0x03BC] = "\\mu",
        [0x00B5] = "\\mu",
        [0x03BD] = "\\nu",
        [0x03BE] = "\\xi",
        [0x03BF] = "o",
        [0x03C0] = "\\pi",
        [0x03D6] = "\\varpi",
        [0x03C1] = "\\rho",
        [0x03F1] = "\\varrho",
        [0x03C2] = "\\varsigma",
        [0x03C3] = "\\sigma",
        [0x03C4] = "\\tau",
        [0x03C5] = "\\upsilon",
        [0x03C6] = "\\varphi",
        [0x03D5] = "\\phi",
        [0x03C7] = "\\chi",
        [0x03C8] = "\\psi",
        [0x03C9] = "\\omega",
        [0x0393] = "\\Gamma",
        [0x0394] = "\\Delta",
        [0x0398] = "\\Theta",
        [0x039B] = "\\Lambda",
        [0x039E] = "\\Xi",
        [0x03A0] = "\\Pi",
        [0x03A3] = "\\Sigma",
        [0x03A5] = "\\Upsilon",
        [0x03A6] = "\\Phi",
        [0x03A8] = "\\Psi",
        [0x03A9] = "\\Omega",
        [0x2126] = "\\Omega",
        [0x0391] = "A",
        [0x0392] = "B",
        [0x0395] = "E",
        [0x0396] = "Z",
        [0x0397] = "H",
        [0x0399] = "I",
        [0x039A] = "K",
        [0x039C] = "M",
        [0x039D] = "N",
        [0x039F] = "O",
        [0x03A1] = "P",
        [0x03A4] = "T",
        [0x03A7] = "X",
    };

    /// <summary>Tên hàm viết thẳng (sin, cos…) có lệnh LaTeX riêng.</summary>
    private static readonly HashSet<string> Functions =
    [
        "sin", "cos", "tan", "cot", "sec", "csc", "arcsin", "arccos", "arctan", "sinh", "cosh", "tanh", "coth", "log", "ln", "lg",
        "exp", "lim", "max", "min", "sup", "inf", "det", "dim", "deg", "gcd", "ker", "arg", "hom", "Pr",
    ];

    /// <summary>"tg", "cotg" của sách Việt Nam.</summary>
    private static readonly Dictionary<string, string> VietnameseFunctions = new(StringComparer.Ordinal)
    {
        ["tg"] = "\\tan",
        ["cotg"] = "\\cot",
        ["arctg"] = "\\arctan",
        ["arccotg"] = "\\operatorname{arccot}",
    };

    /// <summary>Một ký tự trong chế độ toán. <paramref name="isText"/> true: chữ thường (tiếng Việt) cần bọc \text.</summary>
    public static string Symbol(int code, out bool isText)
    {
        isText = false;
        if (Map.TryGetValue(code, out var mapped))
        {
            return mapped;
        }

        if (code is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            return ((char)code).ToString();
        }

        switch (code)
        {
            case '{': return "\\{";
            case '}': return "\\}";
            case '%': return "\\%";
            case '#': return "\\#";
            case '&': return "\\&";
            case '_': return "\\_";
            case '$': return "\\$";
            case '\\': return "\\backslash";
            case '~': return "\\sim";
            case '^': return "\\hat{}";
            case ' ': return "\\ ";
        }

        if (code is < 0x20 or (>= 0xE000 and <= 0xF8FF) or (>= 0x0300 and <= 0x036F) or (>= 0x20D0 and <= 0x20FF))
        {
            // Ký tự điều khiển, vùng riêng (glyph trang trí của MathType), dấu kết hợp: bỏ
            return string.Empty;
        }

        if (code < 0x80)
        {
            return ((char)code).ToString();
        }

        var text = char.ConvertFromUtf32(code);
        if (char.IsLetter(text, 0))
        {
            isText = true;
        }

        return text;
    }

    /// <summary>Lệnh LaTeX cho tên hàm (sin → \sin), null nếu không phải tên hàm quen thuộc.</summary>
    public static string? Function(string name) =>
        Functions.Contains(name) ? "\\" + name : VietnameseFunctions.GetValueOrDefault(name);

    /// <summary>Chuỗi trong \text{...}: thoát ký tự đặc biệt của LaTeX.</summary>
    public static string EscapeText(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text.Normalize(NormalizationForm.FormC))
        {
            sb.Append(ch switch
            {
                '\\' => "\\textbackslash{}",
                '{' => "\\{",
                '}' => "\\}",
                '$' => "\\$",
                '%' => "\\%",
                '#' => "\\#",
                '&' => "\\&",
                '_' => "\\_",
                '^' => "\\^{}",
                '~' => "\\~{}",
                '\u00A0' => " ",
                _ => ch.ToString(),
            });
        }

        return sb.ToString();
    }

    /// <summary>Nối token, chèn dấu cách khi lệnh (\alpha) đứng ngay trước chữ cái để không thành \alphax.</summary>
    public static void Append(StringBuilder sb, string token)
    {
        if (token.Length == 0)
        {
            return;
        }

        if (char.IsAsciiLetter(token[0]) && TrailingCommand().IsMatch(sb.ToString(Math.Max(0, sb.Length - 32), Math.Min(32, sb.Length))))
        {
            sb.Append(' ');
        }

        sb.Append(token);
    }

    /// <summary>Bỏ dấu cách thừa, cặp {} rỗng ở hai đầu.</summary>
    public static string Tidy(string latex) => MultiSpace().Replace(latex, " ").Trim();

    public static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"\\[A-Za-z]+$")]
    private static partial Regex TrailingCommand();

    [GeneratedRegex(@" {2,}")]
    private static partial Regex MultiSpace();
}
