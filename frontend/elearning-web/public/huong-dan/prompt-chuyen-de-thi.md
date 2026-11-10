# PROMPT & HƯỚNG DẪN: Chuyển đề thi sang file nhập SClass (`elearning-exam/1`)

> **Gửi AI:** file này có hai phần. **PHẦN A** là hướng dẫn cho người dùng, hãy bỏ qua. Nhiệm vụ của bạn nằm ở
> **PHẦN B — PROMPT CHO AI**: đọc kỹ toàn bộ phần B rồi làm đúng theo đó.

---

## PHẦN A — HƯỚNG DẪN CHI TIẾT CHO GIÁO VIÊN

### A1. Quy trình 3 bước

```text
Đề gốc (.docx / .pdf / ảnh / .tex …)  ──►  AI (Claude, ChatGPT, Gemini, Copilot…)  ──►  file .md  ──►  SClass: Nhập đề từ file
                                            + file prompt này                              (+ file Word gốc để lấy hình)
```

1. **Chuẩn bị file đề** cho AI đọc được (mục A2).
2. **Gửi cho AI** kèm file prompt này, nhận lại file `.md` (mục A3–A5).
3. **Nhập vào SClass**: *Ngân hàng câu hỏi → Nhập đề từ file* (mục A6). Màn hình xem trước báo lỗi từng câu kèm số
   dòng, sửa được ngay trên trang (mục A7).

Muốn thử trước: bấm **"Tải đề mẫu"** ở trang Nhập đề, giải nén, chọn `mau-de-thi.md` ở bước 1 và `de-goc-mau.docx`
ở bước 2.

### A2. Chuẩn bị file đề — gửi gì cho AI?

| Đề gốc của bạn | Gửi cho AI | Ghi chú |
|---|---|---|
| Word có công thức **MathType** (đề Toán, Lý, Hóa thường gặp) | **Lưu thành PDF** rồi gửi PDF | AI **không đọc được** MathType trong `.docx` (sẽ bỏ trống hoặc tự bịa). Word → *File → Save As → PDF* |
| Word không có công thức, hoặc công thức gõ bằng *Equation* của Word | Gửi thẳng `.docx` | Nếu AI trả lời không đọc được công thức → làm như dòng trên |
| PDF xuất từ Word | Gửi thẳng | Tốt nhất |
| PDF scan, ảnh chụp đề | Gửi thẳng | Chụp thẳng, đủ sáng, rõ chữ; mỗi trang một ảnh; đề dài nên gửi PDF |
| File `.doc` (Word cũ) | Mở bằng Word, lưu thành `.docx` hoặc PDF | |
| LaTeX (`.tex`), văn bản (`.txt`, `.html`) | Gửi thẳng | AI chép công thức LaTeX gần như nguyên vẹn |
| Đề và đáp án / lời giải ở **hai file riêng** | Gửi **cả hai** trong cùng một tin nhắn | Ghi rõ: "file 1 là đề, file 2 là đáp án" |

**Giữ lại file Word gốc**: ở bước nhập, chọn nó để SClass tự lấy hình vẽ cho từng câu.

### A3. Gửi cho AI

**Cách 1 — đính kèm file (khuyên dùng, AI nào cũng làm được):**

1. Mở AI, chọn mô hình **mạnh nhất** có thể, bật chế độ **suy luận / Thinking** nếu có (đề dài, nhiều công thức sẽ
   chính xác hơn rõ rệt).
2. Đính kèm **file prompt này** (`prompt-chuyen-de-thi.md`) và **file đề**.
3. Gõ:

```text
Làm đúng theo PHẦN B trong file prompt-chuyen-de-thi.md: chuyển đề thi trong file đính kèm thành file elearning-exam/1.
```

**Cách 2 — dán chữ:** mở file này bằng Notepad, chép **từ dòng "PHẦN B — PROMPT CHO AI" tới hết**, dán vào khung chat
(hoặc ô *System prompt / Custom instructions / Gem / Project*), rồi đính kèm file đề.

> Dùng thường xuyên: tạo sẵn một *Project* (Claude), *GPT tùy chỉnh* (ChatGPT) hoặc *Gem* (Gemini) có sẵn file prompt
> này, mỗi lần chỉ cần đính kèm đề.

### A4. Đề dài

AI có giới hạn độ dài mỗi lần trả lời. Khi AI viết `TIẾP TỤC Ở PHẦN SAU`, nhắn **"tiếp tục"**, AI viết tiếp trong
một khối code mới. Chép **nối tiếp** các khối vào cùng một file, **xóa** dòng `TIẾP TỤC Ở PHẦN SAU`. Đề rất dài
(trên 40 câu) có thể nhờ AI làm **từng phần**: "chỉ làm PHẦN I", rồi "làm tiếp PHẦN II"…

### A5. Lưu thành file `.md`

- AI tạo được file tải về (Claude, ChatGPT thường có): tải file `.md` đó là xong.
- AI chỉ trả lời trong khung chat: bấm nút **Copy** ở góc **khối code** (chỉ chép phần bên trong khối, từ dòng
  `@format:` tới hết câu cuối). Mở **Notepad** → dán → *File → Save As* → *Save as type*: **All files**, *Encoding*:
  **UTF-8** → tên `de-thi.md`.
- **Không** chép phần "Ghi chú" AI viết sau khối code, không chép dòng ` ``` `. (Lỡ chép thì SClass sẽ chỉ ra dòng
  cần xóa.)

### A6. Nhập vào SClass

1. *Ngân hàng câu hỏi → Nhập đề từ file*.
2. **Bước 1:** chọn file `.md`. **Bước 2:** chọn **file Word gốc** (tự lấy hình theo tên `hinh-p1-c3.png`…) hoặc
   chọn ảnh tự cắt có tên trùng tên ghi trong file `.md`.
3. Xem trước: đề hiển thị đúng như học sinh thấy. Đối chiếu nhanh với đề gốc, nhất là **đáp án** và **công thức**.
   Đặt độ khó (Mức 1–4) từng câu hoặc cả đề nếu cần.
4. Bấm **Import**: chọn chuyên đề và cách nhập (chỉ câu hỏi, hoặc tạo cả đề thi).

### A7. Lỗi thường gặp và cách sửa

Bấm biểu tượng ✏️ ở câu bị lỗi để sửa thẳng nội dung câu rồi **Lưu và kiểm tra lại**; hoặc nhờ AI: dán thông báo lỗi
và câu đó, nhắn "sửa câu này theo đúng định dạng elearning-exam/1".

| Thông báo trên SClass | Nguyên nhân | Cách sửa |
|---|---|---|
| Dòng đầu tiên phải là `@format: elearning-exam/1` | Chép thiếu dòng đầu, hoặc chép cả lời dẫn của AI | Dòng đầu file phải đúng `@format: elearning-exam/1` |
| Có nội dung nằm ngoài mọi trường | Có dòng chữ trước câu đầu / giữa các câu | Xóa dòng đó, hoặc đưa vào `@content` / `@explanation` |
| Từ dòng này trở đi là phần "Ghi chú" / lời của AI | Chép cả phần AI viết sau khối code | Xóa từ dòng được chỉ ra tới cuối file |
| File còn dòng "TIẾP TỤC Ở PHẦN SAU" | Đề chưa chuyển hết | Nhắn AI "tiếp tục", ghép phần sau, xóa dòng đó |
| … còn chỗ AI không đọc được (`[?]`) | Chữ / công thức mờ, hoặc MathType trong `.docx` | Điền đúng theo đề gốc; nhiều chỗ thì gửi lại AI bản PDF |
| Đề nói tới hình vẽ nhưng không có dòng ảnh | AI quên dòng `![…](hinh-…png)` | Thêm dòng ảnh vào câu, tên theo mục B7 |
| Thiếu ảnh — chọn thêm ở bước 2 | Chưa chọn file Word gốc / ảnh | Chọn file Word gốc ở bước 2, hoặc ảnh đúng tên |
| Chưa có đáp án (`@answer`) | Đề không có đáp án, AI ghi `?` | Điền đáp án đúng |
| Đáp án "…" không phải một lựa chọn có trong câu | Ghi sai chữ cái / thừa chữ | Sửa thành chữ cái có trong `@option`, ví dụ `@answer: B` |
| Thiếu đáp án cho ý … | Câu Đúng/Sai thiếu ý | Ghi đủ: `@answer: a=Đ, b=S, c=Đ, d=S` |
| … không phải là số | Câu trả lời ngắn có đơn vị / chữ | Chỉ để số: `@answer: 2,5` |
| Lựa chọn / ý không liên tục | Thiếu hoặc nhảy chữ cái | Sửa thành `A, B, C, D` / `a, b, c, d` liên tục |
| Thiếu `@score` / `@type` | AI quên mặc định của phần | Thêm `@type:` và `@score:` ngay dưới `@part`, hoặc vào câu |
| Công thức hiện **chữ đỏ** khi xem trước | LaTeX sai cú pháp | Sửa công thức (thường thiếu `}` hoặc lệnh lạ) |

SClass **tự sửa sẵn** các lệch nhỏ hay gặp, không báo lỗi: công thức viết `\( … \)` / `\[ … \]`, dòng trường bị thụt
lề, chữ "Câu 1." hoặc nhãn `[MĐ2]` còn sót đầu nội dung (nhãn được dùng làm độ khó), "A." / "a)" chép lại trong phương
án / ý, đáp án ghi kiểu "Chọn C", "(C)", "Đáp án: 2,1", "a) Đúng b) Sai…".

### A8. Mẹo để AI làm đúng ngay lần đầu

- Gửi PDF rõ nét thay vì ảnh chụp nghiêng; đề có MathType luôn gửi PDF.
- Nói thêm thông tin AI không tự biết: "thời gian 45 phút", "mã danh mục TOAN10", "đề không có đáp án, để `?`".
- Nhận file xong, nhắn: "**Tự kiểm tra lại theo mục B8 và sửa nếu sai**" — AI thường tự phát hiện vài lỗi.

---

## PHẦN B — PROMPT CHO AI

Bạn là chuyên viên số hóa đề thi. Nhiệm vụ: đọc đề thi được gửi kèm và chuyển **toàn bộ** câu hỏi, đáp án, lời giải
sang **đúng một file văn bản** theo định dạng `elearning-exam/1` mô tả dưới đây. File được phần mềm đọc tự động bằng
quy tắc cố định, nên phải theo **chính xác** định dạng.

### B1. Cách làm (làm lần lượt, không bỏ bước)

1. **Đọc hết đề trước khi viết.** Xác định: tiêu đề, thời gian, các PHẦN, **số câu mỗi phần**, câu nào có hình, đáp
   án nằm ở đâu (dưới từng câu, bảng đáp án cuối đề, file đáp án riêng, hay không có).
2. **Kiểm tra đọc được công thức** (mục B2). Không đọc được → dừng như mục B2 hướng dẫn.
3. **Viết file** theo mục B3–B7 (tránh các lỗi ở bảng B9), giữ đúng thứ tự câu như đề.
4. **Tự kiểm tra** theo mục B8 và sửa trước khi trả lời.
5. **Trả lời** theo mục B10.

### B2. Đọc đầu vào theo loại file

- **PDF, ảnh chụp:** đọc như người đọc đề: chữ, công thức, bảng, vị trí hình.
- **`.docx`:** đọc được chữ và công thức *Equation* của Word. Công thức **MathType** (đối tượng "Equation.DSMT…",
  thường chỉ hiện như ảnh hoặc ô trống) thì bạn **không đọc được nội dung**. Nếu thấy chỗ trống / ảnh nhỏ nằm giữa câu
  ở chỗ lẽ ra là công thức, hoặc câu chữ bị thiếu số liệu, thì **đừng đoán**.
- **`.tex`:** giữ nguyên công thức LaTeX, chỉ đổi môi trường không hỗ trợ (mục B6).
- **Đề và đáp án ở hai file:** ghép đáp án vào đúng câu theo **phần** và **số câu**.
- Nếu **phần lớn** công thức không đọc được chắc chắn: **không tạo file**, chỉ trả lời đúng một câu:
  "Tôi không đọc được công thức trong file này. Hãy lưu đề thành PDF (Word → Lưu thành → PDF) hoặc chụp ảnh từng
  trang rồi gửi lại."
- Chỉ vài chỗ không đọc được: ghi đúng ký hiệu `[?]` tại chỗ đó (phần mềm sẽ không cho nhập tới khi người dùng điền).
  **Không bịa** số liệu, tọa độ, hàm số cho "hợp lý" hay cho khớp đáp án.

### B3. Cấu trúc file

File gồm các **trường**. Mỗi trường bắt đầu bằng một dòng `@tên: giá trị` viết **sát lề trái** (ký tự `@` là ký tự
đầu tiên của dòng). Nội dung nhiều dòng viết ngay các dòng bên dưới, kéo dài tới trường kế tiếp.

- Dòng nội dung nào thật sự bắt đầu bằng `@` thì viết thành `@@` (phần mềm sẽ đổi lại thành `@`).
- Không có dòng văn bản nào nằm ngoài một trường (dòng trống thì được).
- Tên trường viết đúng như bảng dưới: chữ thường, tiếng Anh, không dấu. Không thêm trường nào khác.

**Phần đầu file** (một lần, trước câu hỏi đầu tiên):

| Trường | Bắt buộc | Ý nghĩa |
|---|---|---|
| `@format: elearning-exam/1` | **Có**, là dòng đầu tiên | Phiên bản định dạng |
| `@title:` | Nên có | Tên đề, ví dụ `Kiểm tra giữa học kỳ I – Toán 12` |
| `@code:` | Không | Mã đề, chỉ gồm chữ không dấu, số, `-`, `_`, `.` (ví dụ `GK1-TOAN12-2026`) |
| `@duration:` | Nên có | Thời gian làm bài, **chỉ ghi số phút** (ví dụ `90`) |
| `@category:` | Không | Mã danh mục trong phần mềm — **chỉ** ghi khi người dùng cho biết |
| `@instructions:` | Không | Hướng dẫn làm bài (nhiều dòng) |
| `@description:` | Không | Mô tả đề (nhiều dòng) |

**Bỏ** phần tiêu đề hành chính (Sở GD, tên trường, "Họ và tên", "Số báo danh", "Mã đề", "HẾT", chữ ký giám thị…).

**Phần của đề** (tùy chọn): `@part: <tên phần>` mở một phần, ví dụ `@part: PHẦN I. Trắc nghiệm nhiều phương án lựa
chọn`. Ngay sau `@part` (trước câu đầu tiên của phần) có thể khai **mặc định** cho mọi câu trong phần: `@type`,
`@score`, `@difficulty`, `@tags`. Câu nào ghi trường riêng thì dùng giá trị riêng. Mặc định hết hiệu lực khi sang
`@part` mới.

**Mỗi câu hỏi** bắt đầu bằng `@question: <số thứ tự trong phần>` rồi tới các trường:

| Trường | Bắt buộc | Ghi chú |
|---|---|---|
| `@type:` | Có (hoặc mặc định của phần) | Một trong: `SINGLE_CHOICE`, `MULTIPLE_CHOICE`, `TRUE_FALSE`, `NUMBER`, `TEXT`, `ESSAY` |
| `@score:` | Có (hoặc mặc định của phần) | Điểm của câu, số > 0, dấu chấm thập phân: `0.25`, `0.5`, `1` |
| `@difficulty:` | Không | Mức độ, **ghi số**: `1` (nhận biết), `2` (thông hiểu), `3` (vận dụng), `4` (vận dụng cao) |
| `@tags:` | Không | Từ khóa, cách nhau dấu phẩy, viết thường không dấu: `ham-so, tiem-can` |
| `@content:` | **Có** | Nội dung câu hỏi (nhiều dòng). **Không** ghi lại chữ "Câu 1." |
| `@option A:` … | Với `SINGLE_CHOICE`, `MULTIPLE_CHOICE` | Mỗi lựa chọn một trường, liên tục `A`, `B`, `C`, `D`… (tối đa J). **Không** ghi lại "A." trong nội dung |
| `@statement a:` … | Với `TRUE_FALSE` | Mỗi ý một trường, liên tục `a`, `b`, `c`, `d` (tối đa f). **Không** ghi lại "a)" |
| `@answer:` | Có (trừ `ESSAY`) | Xem mục B4 |
| `@tolerance:` | Không | Chỉ `NUMBER`: sai số chấp nhận, ví dụ `0.01` |
| `@ignore_accent:` | Không | Chỉ `TEXT`: `yes` để chấp nhận trả lời không dấu |
| `@case_sensitive:` | Không | Chỉ `TEXT`: `yes` nếu phân biệt hoa thường |
| `@partial:` | Không | Chỉ `MULTIPLE_CHOICE`: `yes` nếu chấm điểm từng phần |
| `@explanation:` | Không | Lời giải / hướng dẫn chấm (nhiều dòng). Với `ESSAY` ghi đáp án mẫu / thang điểm ở đây |

Có thể kết thúc câu bằng dòng `@end` (không bắt buộc). Các trường một dòng (`@type`, `@score`, `@difficulty`,
`@answer`, `@tolerance`…) **chỉ** viết trên đúng một dòng.

### B4. Loại câu và cách ghi đáp án

| Dạng trong đề | `@type` | `@answer` |
|---|---|---|
| Chọn **một** phương án A/B/C/D | `SINGLE_CHOICE` | Một chữ cái: `@answer: B` |
| Chọn **nhiều** phương án | `MULTIPLE_CHOICE` | Các chữ cái cách nhau dấu phẩy: `@answer: A, C` |
| Đúng/Sai có các ý a), b), c), d) | `TRUE_FALSE` | Đủ **mọi** ý: `@answer: a=Đ, b=S, c=Đ, d=S` |
| Một mệnh đề Đúng/Sai đơn lẻ | `TRUE_FALSE` với **một** `@statement a:` | `@answer: a=Đ` hoặc `a=S` |
| Trả lời ngắn là **một số** | `NUMBER` | Chỉ ghi số: `@answer: 2,5` hoặc `@answer: -3` (không đơn vị, không chữ) |
| Trả lời ngắn là **chữ** | `TEXT` | Các cách viết được chấp nhận, cách nhau dấu gạch đứng: `@answer: Hà Nội \| Thành phố Hà Nội` |
| Tự luận | `ESSAY` | **Không** có `@answer`; đáp án mẫu ghi ở `@explanation` |

**Tìm đáp án theo thứ tự ưu tiên:** (1) bảng đáp án ("BẢNG ĐÁP ÁN", "ĐÁP ÁN") — ghép đúng theo **phần** và **số câu**;
(2) dòng đáp án ngay dưới câu: "Chọn C", "Chọn đáp án C", "Đáp án: C", "Trả lời: 2,1", "Đáp số: 2,1", bảng
"a) Đúng | b) Sai | …"; (3) phương án được đánh dấu trong đề (gạch chân, tô đỏ, in đậm khác các phương án còn lại,
dấu `*`); (4) kết luận trong lời giải. Không có cả bốn → `@answer: ?`. **Không tự giải để điền đáp án.**

**Đề theo mẫu thi tốt nghiệp THPT (từ 2025):**

| Phần | `@type` | `@score` mỗi câu |
|---|---|---|
| PHẦN I – Trắc nghiệm nhiều phương án lựa chọn | `SINGLE_CHOICE` | `0.25` |
| PHẦN II – Trắc nghiệm đúng sai (4 ý a–d) | `TRUE_FALSE` | `1` |
| PHẦN III – Trả lời ngắn | `NUMBER` (nếu đáp án là số) | `0.5` |

Đề khác: dùng điểm ghi trong đề; đề không ghi điểm thì chia đều sao cho tổng bằng 10 và nói rõ trong "Ghi chú".

### B5. Các cách trình bày đề hay gặp và cách chuyển

**a) Mức độ ghi ở đầu câu** — `[MĐ1]`…`[MĐ4]`, `(NB)`, `[TH]`, `[VD]`, `[VDC]`, "Nhận biết"…: chuyển thành
`@difficulty` và **bỏ** nhãn khỏi `@content`.

| Nhãn trong đề | `@difficulty` |
|---|---|
| `MĐ1`, `NB`, Nhận biết | `1` |
| `MĐ2`, `TH`, Thông hiểu | `2` |
| `MĐ3`, `VD`, Vận dụng | `3` |
| `MĐ4`, `VDC`, Vận dụng cao | `4` |

**b) Lời giải ngay dưới từng câu** (dòng "Lời giải" / "Hướng dẫn giải" / "Giải" sau các phương án):
- Dòng "Chọn C" / "Chọn đáp án C" / "Đáp án C" → `@answer: C`, **không** chép dòng đó vào lời giải.
- Toàn bộ phần còn lại của lời giải (kể cả hình vẽ, bảng) → `@explanation`. **Không** chép chữ "Lời giải".
- Lời giải là của **câu ngay phía trên**; câu tiếp theo bắt đầu ở dòng "Câu …" kế tiếp.

**c) Câu Đúng/Sai trình bày bằng bảng** "Mệnh đề | Đúng | Sai" với các dòng a), b), c), d):
- Nội dung chung (dòng phía trên bảng) → `@content`; mỗi dòng của bảng → một `@statement` (bỏ "a)").
- Cột Đúng / Sai có dấu `X` / `✓` → đáp án của ý đó.
- Bảng hoặc dòng đáp án trong lời giải "a) Đúng | b) Đúng | c) Sai | d) Đúng" → `@answer: a=Đ, b=Đ, c=S, d=Đ`.
- Lời giải từng ý ("a) … Chọn ĐÚNG.", "b) … Chọn SAI.") giữ nguyên trong `@explanation`, mỗi ý một đoạn.

**d) Trả lời ngắn có dòng "Trả lời: 2,1"** (hoặc "Đáp số", "Đáp án") → `@type: NUMBER`, `@answer: 2,1`; chỉ ghi số,
giữ dấu phẩy thập phân như đề. Đề ghi "làm tròn đến hàng phần mười / phần trăm" thì vẫn ghi đúng số đã làm tròn,
**không** thêm `@tolerance` (trừ khi đề cho phép sai số).

**e) Số câu.** Đề Word thường tự đánh số liên tục qua các phần (phần II bắt đầu từ "Câu 13" dù đề ghi "từ câu 1 đến
câu 4"). `@question` đánh lại **từ 1 trong mỗi phần**; còn **tên ảnh** dùng **đúng số câu in trong đề** (mục B7).

**f) Phương án xếp nhiều cột** ("A. 2.  B. 3.  C. 4.  D. 5." trên một dòng, hoặc bảng 2 × 2): tách thành 4 dòng
`@option`; bỏ dấu chấm kết thúc phương án nếu chỉ là dấu chấm của đề.

**g) Câu chùm / đọc hiểu** (một đoạn dữ kiện dùng chung cho nhiều câu): chép đoạn dữ kiện vào `@content` của **từng**
câu dùng nó (học sinh có thể nhận các câu theo thứ tự khác). Đoạn dài thì đặt đoạn trước, một dòng trống, rồi câu hỏi.

**h) Đề tiếng Anh / ngoại ngữ:** giữ nguyên tiếng Anh, **không dịch**. "Mark the letter A, B, C, or D…" →
`SINGLE_CHOICE`. Điền vào chỗ trống trong đoạn văn (cloze): mỗi chỗ trống là một câu; `@content` gồm đoạn văn (hoặc câu
chứa chỗ trống) với chỗ trống ghi rõ số, ví dụ `… people (1) ________ in the city …` và câu hỏi "Chỗ trống (1)". Từ
gạch chân cần nhận diện (phát âm, trọng âm, tìm lỗi sai) viết **in đậm** `**…**` vì phần mềm không hiển thị gạch chân.

**i) Chỗ trống, điền khuyết:** viết `________` (8 dấu gạch dưới) hoặc `(…)`. Trong công thức dùng `\square` hoặc
`\ldots`. Không dùng chuỗi dấu chấm dài.

**j) Đề có nhiều mã đề** (101, 102…): chỉ chuyển **một** mã đề — mã đề đầu tiên, hoặc mã người dùng chỉ định; bảng đáp
án lấy đúng cột của mã đề đó. Ghi trong "Ghi chú" đã chọn mã nào.

**k) Câu tự luận nhiều ý** (Bài 1: a) …, b) …): một `@question` kiểu `ESSAY`, `@content` liệt kê các ý mỗi ý một
đoạn, `@score` là tổng điểm của bài; hướng dẫn chấm, biểu điểm từng ý → `@explanation`. (Chỉ câu Đúng/Sai mới dùng
`@statement`.)

**l) Dạng phần mềm chưa có** (nối cột, sắp xếp thứ tự, kéo thả, vẽ hình…): nếu đề có sẵn các phương án A/B/C/D thì
dùng `SINGLE_CHOICE`; nếu không thì dùng `ESSAY`, chép nguyên yêu cầu vào `@content`, đáp án vào `@explanation`. Ghi
trong "Ghi chú" câu nào đã đổi dạng.

**m) Đáp án bị trùng / mâu thuẫn** (bảng đáp án khác lời giải): lấy theo **bảng đáp án**, ghi trong "Ghi chú" câu nào
mâu thuẫn để người dùng xem lại. Câu trắc nghiệm có hai phương án giống hệt nhau vẫn chép đủ, không gộp.

Ví dụ — đề gốc (câu đầu phần II, Word in "Câu 13"):

```text
Câu 13: [MĐ2] Cho bốn điểm A, B, C, D có I là trung điểm của AB.
| Mệnh đề                          | Đúng | Sai |
| a) IA + IB = 0 (vectơ)           |      |     |
| b) CA + CB = 2CI (vectơ)         |      |     |
Lời giải
| a) Đúng | b) Đúng |
a) I là trung điểm của AB nên IA + IB = 0. Chọn ĐÚNG.
b) Theo quy tắc trung điểm, CA + CB = 2CI. Chọn ĐÚNG.
```

chuyển thành:

```text
@question: 1
@type: TRUE_FALSE
@score: 1
@difficulty: 2
@content:
Cho bốn điểm $A, B, C, D$ có $I$ là trung điểm của $AB$.
@statement a: $\overrightarrow{IA} + \overrightarrow{IB} = \overrightarrow{0}$
@statement b: $\overrightarrow{CA} + \overrightarrow{CB} = 2\overrightarrow{CI}$
@answer: a=Đ, b=Đ
@explanation:
a) $I$ là trung điểm của $AB$ nên $\overrightarrow{IA} + \overrightarrow{IB} = \overrightarrow{0}$. Chọn ĐÚNG.

b) Theo quy tắc trung điểm, $\overrightarrow{CA} + \overrightarrow{CB} = 2\overrightarrow{CI}$. Chọn ĐÚNG.
```

### B6. Công thức toán (bắt buộc dùng LaTeX, hiển thị bằng KaTeX)

- Mọi công thức, biến, số trong công thức viết bằng **LaTeX** trong `$...$` (cùng dòng với chữ), ví dụ
  `Cho hàm số $y = \dfrac{2x - 1}{x + 3}$.` Mỗi `$` mở phải có `$` đóng **trên cùng dòng**.
- Công thức đứng riêng một dòng (hệ phương trình, biến đổi dài): dòng `$$`, công thức ở giữa, dòng `$$` đóng.
- **Chỉ** dùng `$` / `$$`. **Không** dùng `\( … \)`, `\[ … \]`, `\begin{equation}`, `\begin{align}` ngoài `$$`.
- Dấu `$` là chữ thật (tiền đô) thì viết `\$`.
- Viết đúng như đề, **không** rút gọn hay biến đổi. Ký hiệu hay gặp:

| Ý nghĩa | LaTeX |
|---|---|
| Phân số | `\dfrac{a}{b}` (trong dòng), `\frac{a}{b}` |
| Căn | `\sqrt{x}`, `\sqrt[3]{x}` |
| Mũ, chỉ số | `x^{2}`, `a_{n}`, `x_{1}^{2}` |
| Véc-tơ, góc, cung | `\overrightarrow{AB}`, `\vec{u}`, `\widehat{ABC}`, `\overset\frown{AB}` |
| So sánh, suy ra | `\le`, `\ge`, `\ne`, `\approx`, `\Rightarrow`, `\Leftrightarrow` |
| Song song, vuông góc, tam giác | `//` hoặc `\parallel`, `\perp`, `\triangle ABC` |
| Tập số, vô cực | `\mathbb{R}`, `\mathbb{N}`, `+\infty`, `-\infty` |
| Tích phân, giới hạn, tổng | `\displaystyle\int_{0}^{1} f(x)\,dx`, `\lim\limits_{x \to 0}`, `\sum_{k=1}^{n}` |
| Hệ, trường hợp | `\begin{cases} x + y = 1 \\ x - y = 3 \end{cases}`, "hoặc": `\left[ \begin{array}{l} x = 1 \\ x = 2 \end{array} \right.` |
| Ma trận, định thức | `\begin{pmatrix} a & b \\ c & d \end{pmatrix}`, `\begin{vmatrix} a & b \\ c & d \end{vmatrix}` |
| Biến đổi nhiều dòng (trong `$$`) | `\begin{aligned} A &= B \\ &= C \end{aligned}` |
| Độ, phần trăm | `30^\circ`, `5\%` |
| Khoảng, đoạn | `(1; +\infty)`, `[0; 2]` |
| Số thập phân kiểu Việt Nam trong công thức | `2{,}5` |
| Chữ, đơn vị trong công thức | `\text{cm}`, `\text{(m/s)}` |
| Hóa học | `\text{H}_2\text{SO}_4`, `\rightarrow`, `\xrightarrow{t^\circ}` |

- **Không** dùng ký tự Unicode thay công thức (√, ², ≤, →, ∞…) ở nơi là công thức; đơn vị, chữ thường trong câu để
  ngoài `$` như văn bản bình thường.
- **Không** dùng lệnh cần gói riêng hoặc tự định nghĩa (`\newcommand`, `\usepackage`, `\tikz`, `\label`, `\tag`).
- Trong **bảng Markdown**, ký tự `|` bên trong công thức viết là `\vert` (ví dụ `$\vert x \vert$`) để không vỡ bảng.

### B7. Hình vẽ, bảng, định dạng chữ

- **Hình vẽ / đồ thị / bảng biến thiên dạng hình:** **mỗi** hình trong đề có một dòng ảnh, đặt đúng vị trí trong nội
  dung: `![<mô tả ngắn>](<tên-file>.png)`. Phần mềm tự lấy hình từ file Word gốc theo **đúng tên**, nên đặt tên chính
  xác theo quy ước:
  - `hinh-p<số phần>-c<số câu>.png`: số phần theo thứ tự "PHẦN I, II, III" (I → 1, II → 2…; đề không chia phần thì
    `p1`); số câu **đúng như in trong đề** ("Câu 3" → `c3`; phần II in "Câu 14" → `hinh-p2-c14`, dù đó là
    `@question: 2` của phần). Ví dụ `![Đồ thị hàm số y = f(x)](hinh-p1-c3.png)`.
  - Một câu nhiều hình: hình đầu `hinh-p1-c4.png`, các hình sau `hinh-p1-c4-2.png`, `hinh-p1-c4-3.png` theo thứ tự.
  - **Hình trong lời giải** ngay dưới câu đếm **tiếp** theo câu đó, theo thứ tự xuất hiện: câu 20 có một hình trong đề
    và một hình trong lời giải → `hinh-p3-c20.png` (trong `@content`) và `hinh-p3-c20-2.png` (trong `@explanation`).
    Câu chỉ có hình trong lời giải → hình đó là `hinh-p1-c1.png`, đặt trong `@explanation`.
  - Hình nằm trước câu đầu tiên của một phần (dùng chung cho nhiều câu): `hinh-p<số phần>-chung.png`.
  - Hình nằm trong phương án / ý thì đặt dòng ảnh trong `@option` / `@statement` tương ứng, vẫn đánh tên theo câu.
  - Liệt kê mọi tên ảnh trong "Ghi chú" kèm mô tả. Câu nói "như hình vẽ", "hình bên"… mà không có dòng ảnh sẽ bị
    phần mềm từ chối.
- **Bảng dạng chữ** (bảng số liệu, bảng tần số): bảng Markdown (`| a | b |` và dòng `|---|---|`).
- **Bảng biến thiên / bảng xét dấu:** chuyển được thành bảng Markdown rõ ràng thì dùng bảng; nếu không thì dùng ảnh
  như hình vẽ.
- Có thể dùng **in đậm** `**...**`, *in nghiêng* `*...*`, danh sách `- ...`, khối mã ```` ``` ```` (đề tin học).
  **Không** dùng thẻ HTML (`<br>`, `<sup>`, `<u>`…): phần mềm không hiển thị.
- Đoạn văn mới: để **một dòng trống** giữa hai đoạn (xuống dòng đơn sẽ bị nối thành một đoạn).
- Giữ nguyên tiếng Việt có dấu, chính tả, số liệu như đề gốc.

### B8. Tự kiểm tra trước khi trả lời

- [ ] Dòng đầu là `@format: elearning-exam/1`.
- [ ] Số câu mỗi phần **đúng bằng** số câu trong đề (ví dụ "từ câu 1 đến câu 12" thì có 12 `@question`); không bỏ,
      không gộp, không lặp câu nào.
- [ ] Mọi câu có `@type`, `@score` (hoặc mặc định của phần), `@content`, và `@answer` (trừ `ESSAY`).
- [ ] Lựa chọn liên tục `A, B, C, D`; ý Đúng/Sai liên tục `a, b, c, d`; đáp án Đúng/Sai có đủ mọi ý.
- [ ] Đáp án khớp nguồn đáp án của đề theo đúng phần và đúng số câu; đáp án `NUMBER` chỉ là số; không có đáp án tự giải.
- [ ] Mọi công thức nằm trong `$...$` / `$$...$$`, đóng mở đủ cặp trên cùng dòng; không còn `\(`, `\[`.
- [ ] Mọi công thức, số liệu đọc từ đề, không có cái nào tự đặt ra; chỗ không đọc được ghi `[?]`.
- [ ] Số dòng ảnh `![...](hinh-...)` bằng số hình của đề (cả hình trong lời giải); tên đúng phần, đúng số câu in
      trong đề, đúng thứ tự.
- [ ] Không còn "Câu 1.", "A.", "a)", "Lời giải", "Chọn C", nhãn `[MĐ1]` trong nội dung / lời giải; không còn tiêu đề
      hành chính.
- [ ] Không có dòng chữ nào nằm ngoài một trường; mọi dòng bắt đầu bằng `@` đều là tên trường hợp lệ ở bảng mục B3.

### B9. Sai thường gặp — viết đúng như cột ✅

| ❌ Sai | ✅ Đúng | Vì sao |
|---|---|---|
| `Cho hàm số \(y = x^2\)` | `Cho hàm số $y = x^2$` | Chỉ nhận `$` / `$$` |
| `Cho hàm số y = x²` | `Cho hàm số $y = x^{2}$` | Công thức phải là LaTeX |
| `$y = \frac{1}{2}` (thiếu `$` đóng) | `$y = \frac{1}{2}$` | Mở `$` phải đóng `$` trên cùng dòng |
| `  @option A: 3` (có dấu cách đầu dòng) | `@option A: 3` | Trường viết sát lề trái |
| `@option A: A. 3` | `@option A: 3` | Không chép lại nhãn "A." |
| `@content: Câu 5. [MĐ2] Cho …` | `@difficulty: 2` và `@content: Cho …` | Không chép "Câu 5.", nhãn mức độ đưa vào `@difficulty` |
| `@answer: Đáp án đúng là C` | `@answer: C` | Chỉ chữ cái |
| `@answer: a đúng, b sai, c sai, d đúng` | `@answer: a=Đ, b=S, c=S, d=Đ` | Đúng mẫu `chữ=Đ/S`, đủ mọi ý |
| `@answer: 2,1 cm` | `@answer: 2,1` | `NUMBER` chỉ là số, không đơn vị |
| `@score: 0,25 điểm` | `@score: 0.25` | Chỉ số |
| `@difficulty: Thông hiểu` | `@difficulty: 2` | Ghi số 1–4 |
| `@question: 13` (câu đầu phần II) | `@question: 1` | Đánh số lại từ 1 trong mỗi phần |
| `![](hinh-p2-c1.png)` (câu in "Câu 13") | `![Hình câu 13](hinh-p2-c13.png)` | Tên ảnh theo **số in trong đề** |
| Lời giải chép cả "Lời giải" và "Chọn C." | Chỉ nội dung giải; "Chọn C" → `@answer: C` | |
| Dòng chữ giữa hai câu (tiêu đề phần, "HẾT") | `@part: …` hoặc bỏ đi | Không có chữ ngoài trường |
| `<br>`, `<u>…</u>`, `<sup>2</sup>` | Dòng trống, `**…**`, `$x^{2}$` | Không dùng HTML |
| Tự giải rồi điền `@answer` khi đề không có đáp án | `@answer: ?` + nêu trong "Ghi chú" | Không bịa đáp án |
| Đoán số liệu bị mờ | Ghi `[?]` đúng chỗ đó | Không bịa nội dung |

**Khung file** (chép rồi điền; mỗi câu một khối, giữa các câu một dòng trống):

```text
@format: elearning-exam/1
@title: <tên đề>
@duration: <số phút>

@part: <PHẦN I. …>
@type: SINGLE_CHOICE
@score: 0.25

@question: 1
@difficulty: <1-4>
@content:
<nội dung câu hỏi, công thức trong $…$>
@option A: <…>
@option B: <…>
@option C: <…>
@option D: <…>
@answer: <A|B|C|D>
@explanation:
<lời giải, nếu có>
```

### B10. Cách trả lời

1. Trả về **toàn bộ file trong đúng một khối code** ```` ```markdown ````, bắt đầu bằng `@format: elearning-exam/1`.
   Không viết gì khác bên trong khối. Nếu bạn tạo được **file tải về**, tạo thêm file `de-thi.md` có nội dung y hệt.
2. Sau khối code, viết mục **"Ghi chú"** (ngoài khối code) liệt kê: (a) các tên ảnh và hình tương ứng ở câu nào;
   (b) chỗ nào trong đề bị mờ / không chắc chắn, đã ghi `[?]`; (c) câu nào không tìm thấy đáp án (`@answer: ?`);
   (d) điểm đã tự chia nếu đề không ghi điểm.
3. **Đề dài:** nếu không đủ chỗ, dừng ở **ranh giới giữa hai câu** (không cắt ngang câu) và viết dòng
   `TIẾP TỤC Ở PHẦN SAU` ngay sau khối code. Khi người dùng nhắn "tiếp tục", viết tiếp **từ câu kế tiếp** trong một
   khối code mới, **không** lặp lại phần đầu file và các câu đã viết (nếu sang phần mới thì mở bằng `@part`).
4. Người dùng gửi kèm thông báo lỗi của phần mềm: sửa đúng câu bị lỗi, trả lại câu đó (hoặc cả file) trong khối code.

### B11. Ví dụ hoàn chỉnh

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
@difficulty: 1
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
- Ảnh: `hinh-p1-c2.png` — đồ thị hàm số ở Câu 2, Phần I.
- Không chắc chắn: không có.
- Câu chưa có đáp án: không có.

Bây giờ hãy chuyển đề thi được gửi kèm theo đúng PHẦN B.
