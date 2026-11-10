using System.Text;
using System.Xml.Linq;

namespace ELearning.Application.Questions;

/// <summary>Đổi công thức Word (Office Math, <c>m:oMath</c>) sang LaTeX.</summary>
public static class OmmlLatex
{
    public static readonly XNamespace M = "http://schemas.openxmlformats.org/officeDocument/2006/math";
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public static string Convert(XElement math) => LatexSymbols.Tidy(Children(math));

    private static string Children(XElement? parent)
    {
        if (parent is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var el in parent.Elements())
        {
            LatexSymbols.Append(sb, Element(el));
        }

        return sb.ToString();
    }

    private static string Arg(XElement el, string name) => Children(el.Element(M + name));

    private static string? Prop(XElement el, string pr, string name) =>
        (string?)el.Element(M + pr)?.Element(M + name)?.Attribute(M + "val");

    private static string Element(XElement el)
    {
        if (el.Name.Namespace != M)
        {
            // w:r chứa m:oMath hiếm gặp; các phần tử Word khác bên trong công thức (định dạng) bỏ qua
            return el.Name == W + "r" ? Children(el) : string.Empty;
        }

        switch (el.Name.LocalName)
        {
            case "oMath" or "oMathPara" or "e" or "num" or "den" or "sub" or "sup" or "deg" or "lim" or "fName" or "box":
                return Children(el);
            case "r":
                return Run(el);
            case "f":
                return Prop(el, "fPr", "type") switch
                {
                    "lin" => $"{{{Arg(el, "num")}}}/{{{Arg(el, "den")}}}",
                    "noBar" => $"\\genfrac{{}}{{}}{{0pt}}{{}}{{{Arg(el, "num")}}}{{{Arg(el, "den")}}}",
                    _ => $"\\frac{{{Arg(el, "num")}}}{{{Arg(el, "den")}}}",
                };
            case "sSup":
                return $"{{{Arg(el, "e")}}}^{{{Arg(el, "sup")}}}";
            case "sSub":
                return $"{{{Arg(el, "e")}}}_{{{Arg(el, "sub")}}}";
            case "sSubSup":
                return $"{{{Arg(el, "e")}}}_{{{Arg(el, "sub")}}}^{{{Arg(el, "sup")}}}";
            case "sPre":
                return $"{{}}_{{{Arg(el, "sub")}}}^{{{Arg(el, "sup")}}}{Arg(el, "e")}";
            case "rad":
                var degree = Arg(el, "deg");
                return Prop(el, "radPr", "degHide") is "1" or "on" or "true" || degree.Length == 0
                    ? $"\\sqrt{{{Arg(el, "e")}}}"
                    : $"\\sqrt[{degree}]{{{Arg(el, "e")}}}";
            case "d":
                return Delimiter(el);
            case "acc":
                return Accent(Prop(el, "accPr", "chr") ?? "\u0302", Arg(el, "e"));
            case "bar":
                return Prop(el, "barPr", "pos") == "top" ? $"\\overline{{{Arg(el, "e")}}}" : $"\\underline{{{Arg(el, "e")}}}";
            case "groupChr":
                var chr = Prop(el, "groupChrPr", "chr") ?? "\u23DF";
                var top = Prop(el, "groupChrPr", "pos") == "top";
                return chr switch
                {
                    "\u23DE" => $"\\overbrace{{{Arg(el, "e")}}}",
                    "\u23DF" => $"\\underbrace{{{Arg(el, "e")}}}",
                    "\u2192" => top ? $"\\xrightarrow{{{Arg(el, "e")}}}" : $"\\xrightarrow[{Arg(el, "e")}]{{}}",
                    "\u2190" => top ? $"\\xleftarrow{{{Arg(el, "e")}}}" : $"\\xleftarrow[{Arg(el, "e")}]{{}}",
                    "\u21D2" => $"\\xRightarrow{{{Arg(el, "e")}}}",
                    _ => top ? $"\\overset{{{Sym(chr)}}}{{{Arg(el, "e")}}}" : $"\\underset{{{Sym(chr)}}}{{{Arg(el, "e")}}}",
                };
            case "nary":
                return Nary(el);
            case "func":
                return $"{Arg(el, "fName")} {Arg(el, "e")}";
            case "limLow":
                var lowBody = Arg(el, "e");
                return IsOperator(lowBody) ? $"{lowBody}_{{{Arg(el, "lim")}}}" : $"\\underset{{{Arg(el, "lim")}}}{{{lowBody}}}";
            case "limUpp":
                var uppBody = Arg(el, "e");
                return IsOperator(uppBody) ? $"{uppBody}^{{{Arg(el, "lim")}}}" : $"\\overset{{{Arg(el, "lim")}}}{{{uppBody}}}";
            case "eqArr":
                return $"\\begin{{array}}{{l}} {string.Join(" \\\\ ", el.Elements(M + "e").Select(Children))} \\end{{array}}";
            case "m":
                var rows = el.Elements(M + "mr").Select(r => r.Elements(M + "e").Select(Children).ToList()).ToList();
                var cols = Math.Max(1, rows.Count == 0 ? 1 : rows.Max(r => r.Count));
                return $"\\begin{{array}}{{{new string('c', cols)}}} {string.Join(" \\\\ ", rows.Select(r => string.Join(" & ", r)))} \\end{{array}}";
            case "borderBox":
                return $"\\boxed{{{Arg(el, "e")}}}";
            case "phant":
                return Arg(el, "e");
            default:
                return el.Name.LocalName.EndsWith("Pr", StringComparison.Ordinal) ? string.Empty : Children(el);
        }
    }

    /// <summary>Chữ trong công thức: kiểu thường (m:nor / chữ tiếng Việt) bọc \text, tên hàm đổi thành \sin…</summary>
    private static string Run(XElement run)
    {
        var text = string.Concat(run.Elements(M + "t").Select(t => t.Value));
        if (text.Length == 0)
        {
            return string.Empty;
        }

        var normal = run.Element(M + "rPr")?.Element(M + "nor") is not null;
        if (normal || (text.Length > 1 && text.Contains(' ', StringComparison.Ordinal) && text.Any(char.IsLetter)))
        {
            return $"\\text{{{LatexSymbols.EscapeText(text)}}}";
        }

        if (LatexSymbols.Function(text.Trim()) is { } function)
        {
            return function;
        }

        var sb = new StringBuilder();
        var pendingText = new StringBuilder();
        void FlushText()
        {
            if (pendingText.Length > 0)
            {
                LatexSymbols.Append(sb, $"\\text{{{LatexSymbols.EscapeText(pendingText.ToString())}}}");
                pendingText.Clear();
            }
        }

        for (var i = 0; i < text.Length; i++)
        {
            var code = char.ConvertToUtf32(text, i);
            if (char.IsHighSurrogate(text[i]))
            {
                i++;
            }

            var latex = LatexSymbols.Symbol(code, out var isText);
            if (isText)
            {
                pendingText.Append(latex);
                continue;
            }

            FlushText();
            LatexSymbols.Append(sb, latex);
        }

        FlushText();
        return sb.ToString();
    }

    private static string Delimiter(XElement el)
    {
        var pr = el.Element(M + "dPr");
        string Chr(string name, string fallback) => (string?)pr?.Element(M + name)?.Attribute(M + "val") ?? fallback;
        var begin = Fence(Chr("begChr", "("), left: true);
        var end = Fence(Chr("endChr", ")"), left: false);
        var separator = Chr("sepChr", "|");
        var parts = el.Elements(M + "e").Select(Children);
        return $"\\left{begin} {string.Join(separator == "|" ? " \\middle| " : $" {Sym(separator)} ", parts)} \\right{end}";
    }

    private static string Fence(string chr, bool left) => chr switch
    {
        "" => ".",
        "{" => "\\{",
        "}" => "\\}",
        "\u27E8" or "\u2329" => "\\langle",
        "\u27E9" or "\u232A" => "\\rangle",
        "\u2016" => "\\|",
        "\u230A" => "\\lfloor",
        "\u230B" => "\\rfloor",
        "\u2308" => "\\lceil",
        "\u2309" => "\\rceil",
        "(" or ")" or "[" or "]" or "|" => chr,
        _ => left ? "(" : ")",
    };

    private static string Accent(string chr, string body) => chr switch
    {
        "\u20D7" or "\u2192" => body.Length > 1 ? $"\\overrightarrow{{{body}}}" : $"\\vec{{{body}}}",
        "\u20D6" or "\u2190" => $"\\overleftarrow{{{body}}}",
        "\u20E1" or "\u2194" => $"\\overleftrightarrow{{{body}}}",
        "\u0303" or "~" or "\u02DC" => body.Length > 1 ? $"\\widetilde{{{body}}}" : $"\\tilde{{{body}}}",
        "\u0304" or "\u0305" or "\u00AF" or "\u203E" => body.Length > 1 ? $"\\overline{{{body}}}" : $"\\bar{{{body}}}",
        "\u0307" or "\u02D9" => $"\\dot{{{body}}}",
        "\u0308" => $"\\ddot{{{body}}}",
        "\u0311" or "\u2322" or "\u23DC" or "\u2312" => $"\\overset{{\\frown}}{{{body}}}",
        _ => body.Length > 1 ? $"\\widehat{{{body}}}" : $"\\hat{{{body}}}",
    };

    private static string Nary(XElement el)
    {
        var chr = Prop(el, "naryPr", "chr") ?? "\u222B";
        var op = chr switch
        {
            "\u2211" => "\\sum",
            "\u220F" => "\\prod",
            "\u2210" => "\\coprod",
            "\u222C" => "\\iint",
            "\u222D" => "\\iiint",
            "\u222E" => "\\oint",
            "\u22C3" => "\\bigcup",
            "\u22C2" => "\\bigcap",
            _ => "\\int",
        };
        var sub = Prop(el, "naryPr", "subHide") is "1" or "on" ? string.Empty : Arg(el, "sub");
        var sup = Prop(el, "naryPr", "supHide") is "1" or "on" ? string.Empty : Arg(el, "sup");
        return $"{op}{(sub.Length > 0 ? $"_{{{sub}}}" : string.Empty)}{(sup.Length > 0 ? $"^{{{sup}}}" : string.Empty)} {Arg(el, "e")}";
    }

    private static bool IsOperator(string latex) => latex is "\\lim" or "\\max" or "\\min" or "\\sup" or "\\inf";

    private static string Sym(string chr) => chr.Length == 0 ? string.Empty : LatexSymbols.Symbol(char.ConvertToUtf32(chr, 0), out _);
}
