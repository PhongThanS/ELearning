using System.Buffers.Binary;
using System.Text;

namespace ELearning.Application.Questions;

/// <summary>
/// Đổi công thức MathType (OLE "Equation.DSMT4…", dữ liệu MTEF phiên bản 5 trong stream "Equation Native") sang LaTeX.
/// Đủ cho công thức của đề phổ thông: phân số, căn, mũ / chỉ số, ngoặc, vectơ, góc, hệ phương trình, ma trận, tích phân,
/// tổng, giới hạn. Không đọc được → null (người dùng thấy [?] và tự sửa).
/// </summary>
public static class MathTypeLatex
{
    private const int MaxDepth = 64;

    /// <summary>File OLE nhúng trong .docx (word/embeddings/oleObjectN.bin).</summary>
    public static string? FromOleObject(byte[] ole)
    {
        var native = CompoundFile.ReadStream(ole, "Equation Native");
        return native is null ? null : FromEquationNative(native);
    }

    /// <summary>Stream "Equation Native": header 28 byte (EQNOLEFILEHDR) rồi dữ liệu MTEF.</summary>
    public static string? FromEquationNative(byte[] data)
    {
        if (data.Length < 8)
        {
            return null;
        }

        var headerLength = BinaryPrimitives.ReadUInt16LittleEndian(data);
        if (headerLength >= data.Length)
        {
            return null;
        }

        return FromMtef(data.AsSpan(headerLength).ToArray());
    }

    public static string? FromMtef(byte[] mtef)
    {
        try
        {
            var root = new Reader(mtef).ReadEquation();
            if (root is null)
            {
                return null;
            }

            var latex = LatexSymbols.Tidy(Writer.Render(root));
            return latex.Length == 0 ? null : latex;
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException or InvalidDataException)
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ Cây công thức

    private abstract record Node;

    private sealed record Line(List<Node> Items, bool IsNull) : Node;

    private sealed record Char(int Typeface, int Code, List<int> Embellishments, bool FunctionStart) : Node;

    private sealed record Template(int Selector, int Variation, List<Node> Items) : Node
    {
        public List<Line> Slots { get; } = Items.OfType<Line>().ToList();

        public List<Char> Symbols { get; } = Items.OfType<Char>().ToList();
    }

    private sealed record Pile(int HAlign, List<Node> Items) : Node;

    private sealed record Matrix(int Rows, int Cols, int HJust, List<Node> Items) : Node;

    // ------------------------------------------------------------------ Đọc MTEF 5

    private sealed class Reader(byte[] b)
    {
        private int _p;
        private int _depth;

        public Node? ReadEquation()
        {
            if (b.Length < 6 || b[0] != 5)
            {
                // Chỉ MTEF 5 (MathType 4 trở lên); Equation Editor 3.0 cũ dùng MTEF 3
                return null;
            }

            _p = 5;
            while (b[_p] != 0)
            {
                _p++; // application key
            }

            _p += 2; // 0 kết thúc + equation options
            var items = ReadList();
            return items.FirstOrDefault(n => n is Line or Pile);
        }

        private List<Node> ReadList()
        {
            if (++_depth > MaxDepth)
            {
                throw new InvalidDataException("Công thức lồng quá sâu.");
            }

            var items = new List<Node>();
            while (_p < b.Length)
            {
                var tag = b[_p++];
                switch (tag)
                {
                    case 0:
                        _depth--;
                        return items;
                    case 1:
                        items.Add(ReadLine());
                        break;
                    case 2:
                        items.Add(ReadChar());
                        break;
                    case 3:
                        items.Add(ReadTemplate());
                        break;
                    case 4:
                        items.Add(ReadPile());
                        break;
                    case 5:
                        items.Add(ReadMatrix());
                        break;
                    case 6:
                        ReadEmbellishment();
                        break;
                    default:
                        Skip(tag);
                        break;
                }
            }

            _depth--;
            return items;
        }

        private Line ReadLine()
        {
            var options = b[_p++];
            Nudge(options);
            if ((options & 0x04) != 0)
            {
                _p += 2; // line spacing
            }

            if ((options & 0x02) != 0)
            {
                Ruler();
            }

            return (options & 0x01) != 0 ? new Line([], true) : new Line(ReadList(), false);
        }

        private Char ReadChar()
        {
            var options = b[_p++];
            Nudge(options);
            var typeface = b[_p++] - 128;
            int? code = null;
            if ((options & 0x20) == 0)
            {
                code = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(_p));
                _p += 2;
            }

            if ((options & 0x04) != 0)
            {
                code ??= b[_p];
                _p++;
            }

            if ((options & 0x10) != 0)
            {
                code ??= BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(_p));
                _p += 2;
            }

            var embellishments = new List<int>();
            if ((options & 0x01) != 0)
            {
                while (_p < b.Length && b[_p] != 0)
                {
                    var tag = b[_p++];
                    if (tag == 6)
                    {
                        embellishments.Add(ReadEmbellishment());
                    }
                    else
                    {
                        Skip(tag);
                    }
                }

                _p++;
            }

            return new Char(typeface, code ?? 0, embellishments, (options & 0x02) != 0);
        }

        private Template ReadTemplate()
        {
            var options = b[_p++];
            Nudge(options);
            var selector = b[_p++];
            int variation = b[_p++];
            if ((variation & 0x80) != 0)
            {
                variation = (variation & 0x7F) | (b[_p++] << 8);
            }

            _p++; // template-specific options
            return new Template(selector, variation, ReadList());
        }

        private Pile ReadPile()
        {
            var options = b[_p++];
            Nudge(options);
            var hAlign = b[_p++];
            _p++; // valign
            if ((options & 0x02) != 0)
            {
                Ruler();
            }

            return new Pile(hAlign, ReadList());
        }

        private Matrix ReadMatrix()
        {
            var options = b[_p++];
            Nudge(options);
            _p++; // valign
            var hJust = b[_p++];
            _p++; // v_just
            var rows = b[_p++];
            var cols = b[_p++];
            _p += ((2 * (rows + 1)) + 7) / 8; // kiểu đường kẻ giữa các hàng
            _p += ((2 * (cols + 1)) + 7) / 8;
            return new Matrix(rows, cols, hJust, ReadList());
        }

        private int ReadEmbellishment()
        {
            var options = b[_p++];
            Nudge(options);
            return b[_p++];
        }

        /// <summary>Bản ghi không ảnh hưởng nội dung (font, cỡ chữ, màu, tùy chọn): bỏ qua đúng số byte.</summary>
        private void Skip(byte tag)
        {
            switch (tag)
            {
                case 7:
                    _p--;
                    Ruler();
                    break;
                case 8:
                    _p += 2;
                    break;
                case 9:
                    var size = b[_p++];
                    _p += size switch { 101 => 2, 100 => 3, _ => 1 };
                    break;
                case >= 10 and <= 14:
                    break;
                case 15:
                    _p++;
                    break;
                case 16:
                    var colorOptions = b[_p++];
                    _p += ((colorOptions & 0x01) != 0 ? 4 : 3) * 2;
                    if ((colorOptions & 0x04) != 0)
                    {
                        CString();
                    }

                    break;
                case 17:
                    _p++;
                    CString();
                    break;
                case 18:
                    _p++;
                    Dimensions();
                    Dimensions();
                    var styles = b[_p++];
                    for (var i = 0; i < styles; i++)
                    {
                        if (b[_p++] != 0)
                        {
                            _p++;
                        }
                    }

                    break;
                case 19:
                    CString();
                    break;
                case >= 100:
                    var length = BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(_p));
                    _p += 2 + length;
                    break;
                default:
                    throw new InvalidDataException($"Bản ghi MTEF lạ: {tag}.");
            }
        }

        private void Nudge(byte options)
        {
            if ((options & 0x08) == 0)
            {
                return;
            }

            var dx = b[_p++];
            var dy = b[_p++];
            if (dx == 128 || dy == 128)
            {
                _p += 4;
            }
        }

        private void Ruler()
        {
            if (b[_p] != 7)
            {
                throw new InvalidDataException("Thiếu bản ghi RULER.");
            }

            _p++;
            var stops = b[_p++];
            _p += stops * 3;
        }

        private void CString()
        {
            while (b[_p++] != 0)
            {
            }
        }

        /// <summary>Mảng kích thước dạng nibble của EQN_PREFS: mỗi giá trị kết thúc bằng nibble 0xF.</summary>
        private void Dimensions()
        {
            var count = b[_p++];
            var nibble = 0;
            int Next()
            {
                var value = nibble % 2 == 0 ? b[_p] >> 4 : b[_p++] & 0x0F;
                nibble++;
                return value;
            }

            for (var i = 0; i < count; i++)
            {
                Next(); // đơn vị
                while (Next() != 0x0F)
                {
                }
            }

            if (nibble % 2 == 1)
            {
                _p++;
            }
        }
    }

    // ------------------------------------------------------------------ Viết LaTeX

    private static class Writer
    {
        private const int TextFace = 1;
        private const int FunctionFace = 2;
        private const int VectorFace = 7;
        private const int SpaceFace = 24;

        public static string Render(Node node) => node switch
        {
            Line line => RenderItems(line.Items),
            Pile pile => RenderPile(pile),
            _ => RenderItems([node]),
        };

        private static string RenderItems(List<Node> items)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < items.Count; i++)
            {
                switch (items[i])
                {
                    case Char { Typeface: TextFace } when IsTextRun(items, i):
                        var text = new StringBuilder();
                        while (i < items.Count && items[i] is Char { Typeface: TextFace } t)
                        {
                            text.Append(char.ConvertFromUtf32(Math.Clamp(t.Code, 0x20, 0x10FFFF)));
                            i++;
                        }

                        i--;
                        LatexSymbols.Append(sb, $"\\text{{{LatexSymbols.EscapeText(text.ToString())}}}");
                        break;
                    case Char { Typeface: FunctionFace } c when char.IsAsciiLetter((char)c.Code):
                        var word = new StringBuilder();
                        while (i < items.Count && items[i] is Char { Typeface: FunctionFace } f && f.Code < 0x80 && char.IsAsciiLetter((char)f.Code))
                        {
                            word.Append((char)f.Code);
                            i++;
                        }

                        i--;
                        var name = word.ToString();
                        LatexSymbols.Append(sb, LatexSymbols.Function(name) ?? (name.Length == 1 ? name : $"\\operatorname{{{name}}}"));
                        break;
                    case Char c:
                        LatexSymbols.Append(sb, RenderChar(c));
                        break;
                    case Template t:
                        LatexSymbols.Append(sb, RenderTemplate(t));
                        break;
                    case Pile p:
                        LatexSymbols.Append(sb, RenderPile(p));
                        break;
                    case Matrix m:
                        LatexSymbols.Append(sb, RenderMatrix(m));
                        break;
                    case Line l when !l.IsNull:
                        LatexSymbols.Append(sb, RenderItems(l.Items));
                        break;
                }
            }

            return sb.ToString();
        }

        /// <summary>Chữ kiểu Text: gom thành \text{...} nếu có chữ cái (tiếng Việt, từ thường); chỉ dấu câu thì để toán.</summary>
        private static bool IsTextRun(List<Node> items, int start)
        {
            for (var i = start; i < items.Count && items[i] is Char { Typeface: TextFace } c; i++)
            {
                if (c.Code > 0x20 && c.Code <= 0x10FFFF && char.IsLetter(char.ConvertFromUtf32(c.Code), 0))
                {
                    return true;
                }
            }

            return false;
        }

        private static string RenderChar(Char c)
        {
            if (c.Typeface == SpaceFace || (c.Code is >= 0xEF00 and <= 0xEF0F))
            {
                return c.Code is 0xEF04 or 0xEF05 ? "\\;" : c.Code is 0xEF00 or 0xEF01 ? string.Empty : "\\,";
            }

            var latex = LatexSymbols.Symbol(c.Code, out var isText);
            if (isText)
            {
                latex = $"\\text{{{LatexSymbols.EscapeText(latex)}}}";
            }
            else if (c.Typeface == VectorFace && latex.Length == 1 && char.IsAsciiLetter(latex[0]))
            {
                latex = $"\\mathbf{{{latex}}}";
            }

            foreach (var e in c.Embellishments)
            {
                latex = Embellish(latex, e);
            }

            return latex;
        }

        private static string Embellish(string x, int type) => type switch
        {
            2 => $"\\dot{{{x}}}",
            3 => $"\\ddot{{{x}}}",
            4 => $"\\dddot{{{x}}}",
            5 => x + "'",
            6 => x + "''",
            7 => "`" + x,
            8 => $"\\tilde{{{x}}}",
            9 => $"\\hat{{{x}}}",
            10 => $"\\not{x}",
            11 => $"\\vec{{{x}}}",
            12 => $"\\overleftarrow{{{x}}}",
            13 => $"\\overleftrightarrow{{{x}}}",
            14 => $"\\overrightharpoon{{{x}}}",
            15 => $"\\overleftharpoon{{{x}}}",
            16 or 17 => $"\\bar{{{x}}}",
            18 => x + "'''",
            19 => $"\\overset{{\\frown}}{{{x}}}",
            20 => $"\\overset{{\\smile}}{{{x}}}",
            24 => $"\\ddddot{{{x}}}",
            25 or 26 or 27 or 28 => $"\\underset{{.}}{{{x}}}",
            29 => $"\\underline{{{x}}}",
            30 => $"\\utilde{{{x}}}",
            31 => $"\\underset{{\\frown}}{{{x}}}",
            32 => $"\\underset{{\\smile}}{{{x}}}",
            33 => $"\\underrightarrow{{{x}}}",
            34 => $"\\underleftarrow{{{x}}}",
            35 => $"\\underleftrightarrow{{{x}}}",
            _ => x,
        };

        private static string RenderTemplate(Template t)
        {
            string Slot(int index) => index < t.Slots.Count && !t.Slots[index].IsNull ? RenderItems(t.Slots[index].Items) : string.Empty;
            bool Has(int index) => index < t.Slots.Count && !t.Slots[index].IsNull && t.Slots[index].Items.Count > 0;
            string Limits(int lower, int upper) => (Has(lower) ? $"_{{{Slot(lower)}}}" : string.Empty) + (Has(upper) ? $"^{{{Slot(upper)}}}" : string.Empty);

            switch (t.Selector)
            {
                case >= 0 and <= 8:
                    var (left, right) = t.Selector switch
                    {
                        0 => ("\\langle", "\\rangle"),
                        1 => ("(", ")"),
                        2 => ("\\{", "\\}"),
                        3 => ("[", "]"),
                        4 => ("|", "|"),
                        5 => ("\\|", "\\|"),
                        6 => ("\\lfloor", "\\rfloor"),
                        7 => ("\\lceil", "\\rceil"),
                        _ => ("[\\![", "]\\!]"),
                    };
                    var hasLeft = (t.Variation & 0x01) != 0;
                    var hasRight = (t.Variation & 0x02) != 0;
                    if (t.Selector == 8)
                    {
                        return $"{(hasLeft ? left : string.Empty)}{Slot(0)}{(hasRight ? right : string.Empty)}";
                    }

                    return $"\\left{(hasLeft ? left : ".")} {Slot(0)} \\right{(hasRight ? right : ".")}";
                case 9:
                    // Khoảng: hai ký tự ngoặc thật nằm sau ô nội dung, ví dụ [a; b) hay (a; b]
                    var fences = t.Symbols.Select(s => FenceChar(s.Code)).ToList();
                    return $"\\left{fences.ElementAtOrDefault(0) ?? "("} {Slot(0)} \\right{fences.ElementAtOrDefault(1) ?? ")"}";
                case 10:
                    return t.Variation == 0 || !Has(1) ? $"\\sqrt{{{Slot(0)}}}" : $"\\sqrt[{Slot(1)}]{{{Slot(0)}}}";
                case 11:
                    return (t.Variation & 0x02) != 0 ? $"{{{Slot(0)}}}/{{{Slot(1)}}}" : $"\\frac{{{Slot(0)}}}{{{Slot(1)}}}";
                case 12:
                    return $"\\underline{{{Slot(0)}}}";
                case 13:
                    return $"\\overline{{{Slot(0)}}}";
                case 14:
                    var arrow = (t.Variation & 0x06) switch
                    {
                        0x02 => (t.Variation & 0x01) != 0 ? "\\xLeftarrow" : "\\xleftarrow",
                        0x06 => (t.Variation & 0x01) != 0 ? "\\xLeftrightarrow" : "\\xleftrightarrow",
                        _ => (t.Variation & 0x01) != 0 ? "\\xRightarrow" : "\\xrightarrow",
                    };
                    return $"{arrow}{(Has(1) ? $"[{Slot(1)}]" : string.Empty)}{{{Slot(0)}}}";
                case 15:
                    var integral = (t.Variation & 0x0C) != 0 ? "\\oint" : (t.Variation & 0x03) switch { 2 => "\\iint", 3 => "\\iiint", _ => "\\int" };
                    return $"{integral}{Limits(1, 2)} {Slot(0)}";
                case >= 16 and <= 20:
                    var op = t.Selector switch { 16 => "\\sum", 17 => "\\prod", 18 => "\\coprod", 19 => "\\bigcup", _ => "\\bigcap" };
                    return $"{op}{Limits(1, 2)} {Slot(0)}";
                case 21 or 22:
                    var symbol = t.Symbols.Count > 0 ? LatexSymbols.Symbol(t.Symbols[0].Code, out _) : t.Selector == 21 ? "\\int" : "\\sum";
                    return $"{symbol}{Limits(1, 0)}";
                case 23:
                    return RenderLimit(t, Slot, Has);
                case 24 or 25:
                    return (t.Variation & 0x01) != 0
                        ? $"\\overbrace{{{Slot(0)}}}{(Has(1) ? $"^{{{Slot(1)}}}" : string.Empty)}"
                        : $"\\underbrace{{{Slot(0)}}}{(Has(1) ? $"_{{{Slot(1)}}}" : string.Empty)}";
                case 26:
                    return $"{Slot(1)}\\overline{{\\smash{{)}}{Slot(0)}}}";
                case 27:
                    return $"_{{{Slot(0)}}}";
                case 28:
                    return $"^{{{Slot(1)}}}";
                case 29:
                    return Limits(0, 1);
                case 30:
                    return $"\\langle {Slot(0)} \\vert {Slot(1)} \\rangle";
                case 31:
                    var under = (t.Variation & 0x04) != 0;
                    var vector = (t.Variation & 0x03) switch
                    {
                        0x01 => "leftarrow",
                        0x03 => "leftrightarrow",
                        _ => (t.Variation & 0x08) != 0 ? "rightharpoon" : "rightarrow",
                    };
                    return $"\\{(under ? "under" : "over")}{vector}{{{Slot(0)}}}";
                case 32:
                    return $"\\widetilde{{{Slot(0)}}}";
                case 33:
                    return $"\\widehat{{{Slot(0)}}}";
                case 34:
                    return $"\\overset{{\\frown}}{{{Slot(0)}}}";
                case 36:
                    return $"\\cancel{{{Slot(0)}}}";
                case 37:
                    return $"\\boxed{{{Slot(0)}}}";
                default:
                    return string.Concat(t.Slots.Where(s => !s.IsNull).Select(s => RenderItems(s.Items)));
            }
        }

        /// <summary>
        /// Mẫu "giới hạn": thân + ô dưới + ô trên. Thân là tên hàm (lim, max) → \lim_{...}; còn lại là ký hiệu đặt trên /
        /// dưới, ví dụ góc Â (ô trên là dấu ^), cung AB (ô trên là ⌢).
        /// </summary>
        private static string RenderLimit(Template t, Func<int, string> slot, Func<int, bool> has)
        {
            var body = slot(0);
            if (body.StartsWith('\\') && body.Length > 1 && char.IsAsciiLetter(body[1]) && body.All(ch => ch == '\\' || char.IsAsciiLetter(ch)))
            {
                return body + (has(1) ? $"_{{{slot(1)}}}" : string.Empty) + (has(2) ? $"^{{{slot(2)}}}" : string.Empty);
            }

            var result = body;
            if (has(2))
            {
                var upper = t.Slots[2].Items;
                result = upper is [Char accent] ? Accent(accent.Code, result) ?? $"\\overset{{{slot(2)}}}{{{result}}}" : $"\\overset{{{slot(2)}}}{{{result}}}";
            }

            if (has(1))
            {
                result = $"\\underset{{{slot(1)}}}{{{result}}}";
            }

            return result;
        }

        private static string? Accent(int code, string body) => code switch
        {
            0x2C6 or '^' or 0x302 => $"\\widehat{{{body}}}",
            0x2DC or '~' or 0x303 => $"\\widetilde{{{body}}}",
            0xAF or 0x203E or 0x304 or 0x305 => $"\\overline{{{body}}}",
            0x2322 or 0x2312 or 0x23DC => $"\\overset{{\\frown}}{{{body}}}",
            0x2192 or 0x20D7 => $"\\overrightarrow{{{body}}}",
            0x2D9 or 0x307 or '.' => $"\\dot{{{body}}}",
            _ => null,
        };

        private static string? FenceChar(int code) => code switch
        {
            '(' => "(",
            ')' => ")",
            '[' => "[",
            ']' => "]",
            '{' => "\\{",
            '}' => "\\}",
            '|' => "|",
            0x27E8 or 0x2329 => "\\langle",
            0x27E9 or 0x232A => "\\rangle",
            _ => null,
        };

        private static string RenderPile(Pile pile)
        {
            var align = pile.HAlign switch { 2 => "c", 3 => "r", _ => "l" };
            var rows = pile.Items.OfType<Line>().Select(l => RenderItems(l.Items));
            return $"\\begin{{array}}{{{align}}} {string.Join(" \\\\ ", rows)} \\end{{array}}";
        }

        private static string RenderMatrix(Matrix matrix)
        {
            var cols = Math.Max(1, (int)matrix.Cols);
            var align = matrix.HJust switch { 1 => "l", 3 => "r", _ => "c" };
            var cells = matrix.Items.OfType<Line>().Select(l => l.IsNull ? string.Empty : RenderItems(l.Items)).ToList();
            var rows = cells.Chunk(cols).Select(r => string.Join(" & ", r));
            return $"\\begin{{array}}{{{string.Concat(Enumerable.Repeat(align, cols))}}} {string.Join(" \\\\ ", rows)} \\end{{array}}";
        }
    }
}
