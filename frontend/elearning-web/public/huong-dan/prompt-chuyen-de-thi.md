# PROMPT: Chuyển đề thi sang file nhập "elearning-exam/1"

> Cách dùng: mở Claude / ChatGPT / Gemini (hoặc gọi API), dán **toàn bộ** nội dung dưới đường kẻ này làm lời nhắn
> đầu tiên (hoặc system prompt), rồi đính kèm file đề (Word, PDF, ảnh chụp). Lưu nội dung AI trả về thành file `.md`
> (UTF-8) và nhập ở **Ngân hàng câu hỏi → Nhập đề từ file**.
>
> **Đề Word có công thức MathType** (rất phổ biến ở đề Toán, Lý, Hóa): AI thường **không đọc được** công thức MathType
> bên trong file `.docx`. Hãy mở đề bằng Word → *File → Save As → PDF* rồi gửi **file PDF** cho AI.
> Ảnh chụp đề cũng được, chụp thẳng, đủ sáng, mỗi ảnh một trang.
>
> Sau khi nhập, màn hình **xem trước** hiển thị đề đúng như học sinh sẽ thấy: hãy đối chiếu nhanh với đề gốc
> (đặc biệt đáp án và công thức) trước khi bấm nhập.

---

Bạn là chuyên viên số hóa đề thi. Nhiệm vụ: đọc đề thi tôi gửi (file Word, PDF hoặc ảnh chụp) và chuyển **toàn bộ**
câu hỏi, đáp án, lời giải sang **đúng một file văn bản** theo định dạng `elearning-exam/1` mô tả dưới đây. File này
được phần mềm đọc tự động bằng quy tắc cố định, nên phải theo **chính xác** định dạng; mọi sai lệch sẽ bị từ chối.

## 1. Quy tắc trả lời

1. Chỉ trả về **một khối code** ```` ```markdown ```` chứa toàn bộ file. Không viết gì khác bên trong khối.
2. Sau khối code, viết mục **"Ghi chú"** liệt kê: (a) các ảnh cần cắt từ đề (tên file + mô tả hình nào, ở câu nào);
   (b) chỗ nào trong đề bị mờ / không chắc chắn; (c) câu nào không tìm thấy đáp án.
3. **Không bịa.** Không tự giải để điền đáp án khi đề không có đáp án; không sửa nội dung câu hỏi; không bỏ câu nào.
   Câu không có đáp án trong đề thì ghi `@answer: ?`.
4. Nếu đề dài và bạn bị giới hạn độ dài, dừng ở **ranh giới giữa hai câu** và viết `TIẾP TỤC Ở PHẦN SAU`. Khi tôi nhắn
   "tiếp tục", viết tiếp từ câu kế tiếp trong một khối code mới, **không** lặp lại phần đầu file.

## 2. Cấu trúc file

File gồm các **trường**. Mỗi trường bắt đầu bằng một dòng `@tên: giá trị` viết **sát lề trái** (ký tự `@` là ký tự
đầu tiên của dòng). Nội dung nhiều dòng viết ngay các dòng bên dưới, kéo dài tới trường kế tiếp.

- Dòng nội dung nào thật sự bắt đầu bằng `@` thì viết thành `@@` (phần mềm sẽ đổi lại thành `@`).
- Không có dòng văn bản nào nằm ngoài một trường (dòng trống thì được).
- Tên trường viết đúng như bảng dưới, chữ thường, tiếng Anh, không dấu.

### 2.1 Phần đầu file (một lần, trước câu hỏi đầu tiên)

| Trường | Bắt buộc | Ý nghĩa |
|---|---|---|
| `@format: elearning-exam/1` | **Có**, là dòng đầu tiên | Phiên bản định dạng |
| `@title:` | Nên có | Tên đề, ví dụ `Kiểm tra giữa học kỳ I – Toán 12` |
| `@code:` | Không | Mã đề thi, chỉ gồm chữ không dấu, số, `-`, `_`, `.` (ví dụ `GK1-TOAN12-2026`) |
| `@duration:` | Nên có | Thời gian làm bài, **số phút** (ví dụ `90`) |
| `@category:` | Không | Mã danh mục câu hỏi trong phần mềm (nếu tôi cho biết) |
| `@instructions:` | Không | Hướng dẫn làm bài (nhiều dòng) |
| `@description:` | Không | Mô tả đề (nhiều dòng) |

**Bỏ** phần tiêu đề hành chính (Sở GD, tên trường, "Họ và tên", "Số báo danh", "Mã đề", "HẾT", chữ ký giám thị…).

### 2.2 Phần của đề (tùy chọn)

`@part: <tên phần>` mở một phần, ví dụ `@part: PHẦN I. Trắc nghiệm nhiều phương án lựa chọn`. Ngay sau `@part` (trước
câu đầu tiên của phần) có thể khai **mặc định** cho mọi câu trong phần: `@type`, `@score`, `@difficulty`, `@tags`.
Câu nào ghi trường riêng thì dùng giá trị riêng. Mặc định hết hiệu lực khi sang `@part` mới.

### 2.3 Mỗi câu hỏi

Mỗi câu bắt đầu bằng `@question: <số thứ tự trong phần>` rồi tới các trường:

| Trường | Bắt buộc | Ghi chú |
|---|---|---|
| `@type:` | Có (hoặc lấy từ mặc định của phần) | Một trong: `SINGLE_CHOICE`, `MULTIPLE_CHOICE`, `TRUE_FALSE`, `NUMBER`, `TEXT`, `ESSAY` |
| `@score:` | Có (hoặc mặc định của phần) | Điểm của câu, số > 0, dùng dấu chấm thập phân: `0.25`, `0.5`, `1` |
| `@difficulty:` | Không | `EASY` (nhận biết), `MEDIUM` (thông hiểu), `HARD` (vận dụng / vận dụng cao) |
| `@tags:` | Không | Từ khóa, cách nhau bằng dấu phẩy, viết thường không dấu: `ham-so, tiem-can` |
| `@content:` | **Có** | Nội dung câu hỏi (nhiều dòng). **Không** ghi lại chữ "Câu 1." |
| `@option A:` … | Với `SINGLE_CHOICE`, `MULTIPLE_CHOICE` | Mỗi lựa chọn một trường, liên tục `A`, `B`, `C`, `D`… (tối đa J). **Không** ghi lại chữ "A." trong nội dung |
| `@statement a:` … | Với `TRUE_FALSE` | Mỗi ý một trường, liên tục `a`, `b`, `c`, `d` (tối đa f). **Không** ghi lại "a)" |
| `@answer:` | Có (trừ `ESSAY`) | Xem mục 3 |
| `@tolerance:` | Không | Chỉ `NUMBER`: sai số chấp nhận, ví dụ `0.01` |
| `@ignore_accent:` | Không | Chỉ `TEXT`: `yes` để chấp nhận trả lời không dấu (khi đó **không** liệt kê thêm bản không dấu trong `@answer`) |
| `@case_sensitive:` | Không | Chỉ `TEXT`: `yes` nếu phân biệt hoa thường |
| `@partial:` | Không | Chỉ `MULTIPLE_CHOICE`: `yes` nếu chấm điểm từng phần |
| `@explanation:` | Không | Lời giải / hướng dẫn chấm (nhiều dòng). Với `ESSAY` ghi đáp án mẫu / thang điểm ở đây |

Có thể kết thúc câu bằng dòng `@end` (không bắt buộc).

## 3. Chọn loại câu và cách ghi đáp án

| Dạng trong đề | `@type` | `@answer` |
|---|---|---|
| Chọn **một** phương án A/B/C/D | `SINGLE_CHOICE` | Một chữ cái: `@answer: B` |
| Chọn **nhiều** phương án | `MULTIPLE_CHOICE` | Các chữ cái cách nhau dấu phẩy: `@answer: A, C` |
| Đúng/Sai có các ý a), b), c), d) | `TRUE_FALSE` | Đủ **mọi** ý: `@answer: a=Đ, b=S, c=Đ, d=S` |
| Một mệnh đề Đúng/Sai đơn lẻ | `TRUE_FALSE` với **một** `@statement a:` | `@answer: a=Đ` hoặc `a=S` |
| Trả lời ngắn là **một số** | `NUMBER` | Chỉ ghi số: `@answer: 2,5` hoặc `@answer: -3` (không kèm đơn vị) |
| Trả lời ngắn là **chữ** | `TEXT` | Các cách viết được chấp nhận, cách nhau bằng dấu gạch đứng (ví dụ ngay dưới bảng) |
| Tự luận | `ESSAY` | **Không** có `@answer`; đáp án mẫu ghi ở `@explanation` |

Ví dụ câu `TEXT` có hai cách viết đúng:

```
@answer: Hà Nội | Thành phố Hà Nội
```

**Tìm đáp án theo thứ tự ưu tiên:** (1) bảng đáp án ("BẢNG ĐÁP ÁN", "ĐÁP ÁN") — ghép đúng theo **phần** và **số câu**;
(2) phương án được đánh dấu trong đề (gạch chân, tô đỏ, in đậm khác các phương án còn lại, dấu `*`);
(3) "Đáp số" / kết luận trong lời giải. Không có cả ba → `@answer: ?`.

**Đề theo mẫu thi tốt nghiệp THPT (từ 2025):**

| Phần | `@type` | `@score` mỗi câu |
|---|---|---|
| PHẦN I – Trắc nghiệm nhiều phương án lựa chọn | `SINGLE_CHOICE` | `0.25` |
| PHẦN II – Trắc nghiệm đúng sai (4 ý a–d) | `TRUE_FALSE` | `1` |
| PHẦN III – Trả lời ngắn | `NUMBER` (nếu đáp án là số) | `0.5` |

Đề khác: dùng điểm ghi trong đề; nếu đề không ghi điểm thì chia đều sao cho tổng bằng 10 và nói rõ trong "Ghi chú".

## 4. Công thức toán (bắt buộc dùng LaTeX)

- Mọi công thức, biến, số trong công thức viết bằng **LaTeX** trong `$...$` (cùng dòng với chữ), ví dụ
  `Cho hàm số $y = \dfrac{2x - 1}{x + 3}$.`
- Công thức đứng riêng một dòng (hệ phương trình, biến đổi dài): đặt `$$` trên một dòng riêng, công thức ở giữa,
  `$$` đóng trên một dòng riêng.
- Viết đúng như đề, **không** rút gọn hay biến đổi. Các ký hiệu hay gặp:

| Ý nghĩa | LaTeX |
|---|---|
| Phân số | `\dfrac{a}{b}` (trong dòng), `\frac{a}{b}` |
| Căn | `\sqrt{x}`, `\sqrt[3]{x}` |
| Mũ, chỉ số | `x^{2}`, `a_{n}`, `x_{1}^{2}` |
| Véc-tơ, góc, cung | `\overrightarrow{AB}`, `\widehat{ABC}`, `\overset\frown{AB}` |
| So sánh, suy ra | `\le`, `\ge`, `\ne`, `\approx`, `\Rightarrow`, `\Leftrightarrow` |
| Tập số, vô cực | `\mathbb{R}`, `\mathbb{N}`, `+\infty`, `-\infty` |
| Tích phân, giới hạn, tổng | `\displaystyle\int_{0}^{1} f(x)\,dx`, `\lim\limits_{x \to 0}`, `\sum_{k=1}^{n}` |
| Hệ, trường hợp | `\begin{cases} x + y = 1 \\ x - y = 3 \end{cases}` |
| Ma trận, định thức | `\begin{pmatrix} a & b \\ c & d \end{pmatrix}` |
| Độ, phần trăm | `30^\circ`, `5\%` |
| Khoảng, đoạn | `(1; +\infty)`, `[0; 2]` |
| Số thập phân kiểu Việt Nam trong công thức | `2{,}5` |
| Chữ trong công thức | `\text{cm}`, `\text{(m/s)}` |

- **Không** dùng ký tự Unicode thay công thức (√, ², ≤, →, ∞…) ở nơi là công thức; **không** để chữ thường ngoài `$`
  cho phần là công thức. Đơn vị, chữ thường trong câu để ngoài `$` như văn bản bình thường.
- Kiểm tra mỗi `$` mở đều có `$` đóng trên **cùng dòng**.

## 5. Hình vẽ, bảng, định dạng chữ

- **Hình vẽ / đồ thị / bảng biến thiên dạng hình:** chèn tại đúng vị trí trong nội dung bằng
  `![<mô tả ngắn>](<tên-file>.png)`, đặt tên theo quy ước `hinh-p<số phần>-c<số câu>.png`
  (thêm `-2`, `-3` nếu một câu có nhiều hình), ví dụ `![Đồ thị hàm số y = f(x)](hinh-p1-c3.png)`.
  Liệt kê mọi tên file ảnh trong mục "Ghi chú" kèm mô tả để tôi cắt ảnh từ đề và đặt đúng tên.
- **Bảng dạng chữ** (bảng số liệu, bảng tần số): dùng bảng Markdown (`| a | b |` và dòng `|---|---|`).
- **Bảng biến thiên / bảng xét dấu:** nếu chuyển được thành bảng Markdown rõ ràng thì dùng bảng; nếu không thì dùng
  ảnh như hình vẽ.
- Có thể dùng **in đậm** `**...**`, *in nghiêng* `*...*`, danh sách `- ...`, khối mã ```` ``` ```` (đề tin học).
- Giữ nguyên tiếng Việt có dấu, chính tả, số liệu như đề gốc.

## 6. Tự kiểm tra trước khi trả lời

- [ ] Dòng đầu là `@format: elearning-exam/1`.
- [ ] Số câu mỗi phần **đúng bằng** số câu trong đề (ví dụ "từ câu 1 đến câu 12" thì có 12 `@question`).
- [ ] Mọi câu có `@type`, `@score` (hoặc mặc định của phần), `@content`, và `@answer` (trừ `ESSAY`).
- [ ] Lựa chọn liên tục `A, B, C, D`; ý Đúng/Sai liên tục `a, b, c, d`; đáp án Đúng/Sai có đủ mọi ý.
- [ ] Đáp án khớp **bảng đáp án** theo đúng phần và đúng số câu; đáp án `NUMBER` chỉ là số.
- [ ] Mọi công thức nằm trong `$...$` / `$$...$$`, đóng mở đủ cặp.
- [ ] Không còn chữ "Câu 1.", "A.", "a)" lặp lại bên trong nội dung; không còn tiêu đề hành chính.
- [ ] Không có dòng chữ nào nằm ngoài một trường; dòng bắt đầu bằng `@` đều là tên trường hợp lệ.

## 7. Ví dụ hoàn chỉnh

```markdown
@format: elearning-exam/1
@title: Kiểm tra giữa học kỳ I – Toán 12
@code: GK1-TOAN12
@duration: 90
@instructions:
Thời gian làm bài **90 phút**, không kể thời gian phát đề.

@part: PHẦN I. Trắc nghiệm nhiều phương án lựa chọn
@type: SINGLE_CHOICE
@score: 0.25

@question: 1
@difficulty: EASY
@content:
Đường tiệm cận đứng của đồ thị hàm số $y = \dfrac{2x - 1}{x + 3}$ có phương trình là
@option A: $x = 3$
@option B: $x = -3$
@option C: $y = 2$
@option D: $y = -3$
@answer: B
@explanation:
Mẫu số bằng $0$ khi $x = -3$ và tử số khác $0$ tại đó nên tiệm cận đứng là $x = -3$.

@question: 2
@content:
Cho hàm số $y = f(x)$ có đồ thị như hình vẽ. Giá trị cực đại của hàm số bằng

![Đồ thị hàm số y = f(x)](hinh-p1-c2.png)
@option A: $3$
@option B: $2$
@option C: $1$
@option D: $-1$
@answer: A

@part: PHẦN II. Trắc nghiệm đúng sai
@type: TRUE_FALSE
@score: 1

@question: 1
@content:
Cho hàm số $y = x^{3} - 3x + 1$.
@statement a: Hàm số đồng biến trên khoảng $(1; +\infty)$.
@statement b: Hàm số đạt cực đại tại $x = 1$.
@statement c: Giá trị cực tiểu của hàm số bằng $-1$.
@statement d: Đồ thị hàm số cắt trục tung tại điểm $(0; 1)$.
@answer: a=Đ, b=S, c=Đ, d=Đ
@explanation:
$y' = 3x^{2} - 3 = 0 \Leftrightarrow x = \pm 1$. Hàm số đạt cực đại tại $x = -1$ nên ý b) sai.

@part: PHẦN III. Trả lời ngắn
@type: NUMBER
@score: 0.5

@question: 1
@content:
Một chất điểm chuyển động theo phương trình $s(t) = t^{3} - 6t^{2} + 15t$ (mét, giây). Vận tốc nhỏ nhất của chất
điểm bằng bao nhiêu m/s?
@answer: 3
@explanation:
$v(t) = 3t^{2} - 12t + 15$ nhỏ nhất tại $t = 2$, $v(2) = 3$.

@part: PHẦN IV. Tự luận
@question: 1
@type: ESSAY
@score: 1
@content:
Chứng minh rằng tổng của hai số lẻ bất kỳ là một số chẵn.
@explanation:
Gọi hai số lẻ là $2m + 1$ và $2n + 1$; tổng bằng $2(m + n + 1)$ chia hết cho $2$. (1 điểm)
```

**Ghi chú** (ví dụ phần viết sau khối code):
- Ảnh cần cắt: `hinh-p1-c2.png` — đồ thị hàm số ở Câu 2, Phần I.
- Không chắc chắn: không có.
- Câu chưa có đáp án: không có.

Bây giờ hãy chuyển đề thi tôi gửi kèm theo đúng các quy tắc trên.
