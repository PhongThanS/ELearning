# HƯỚNG DẪN SỬ DỤNG HỆ THỐNG THI TRỰC TUYẾN & ĐÀO TẠO SỐ ELEARNING
*(Tài liệu hướng dẫn chi tiết toàn bộ các tính năng dành cho Học viên, Giáo viên và Quản trị viên)*

---

## 📌 THÔNG TIN TÀI LIỆU
- **Tên hệ thống:** Hệ thống Thi Trực Tuyến & Đào tạo Số ELearning
- **Phiên bản:** 2.0 (PostgreSQL 17, .NET 10 Web API, React 19 / TypeScript / Vite)
- **Tài liệu Word (.docx):**
  - Bản đặt tại thư mục gốc dự án: [`HUONG_DAN_SU_DUNG_ELEARNING.docx`](file:///d:/Eleaning/ELearning/HUONG_DAN_SU_DUNG_ELEARNING.docx)
  - Bản lưu trữ trong thư mục tài liệu: [`docs/HUONG_DAN_SU_DUNG_ELEARNING.docx`](file:///d:/Eleaning/ELearning/docs/HUONG_DAN_SU_DUNG_ELEARNING.docx)
- **Ngày ban hành:** Tháng 10 / 2026

---

## MỤC LỤC TÀI LIỆU
1. [Phần 1: Giới thiệu Tổng quan, Đăng ký & Đăng nhập Hệ thống](#phần-1-giới-thiệu-tổng-quan-đăng-ký--đăng-nhập-hệ-thống)
2. [Phần 2: Cẩm nang Sử dụng Toàn diện dành cho Học viên (Student Guide)](#phần-2-cẩm-nang-sử-dụng-toàn-diện-dành-cho-học-viên-student-guide)
3. [Phần 3: Cẩm nang Nghiệp vụ dành cho Giáo viên & Quản trị viên (Admin & Teacher Guide)](#phần-3-cẩm-nang-nghiệp-vụ-dành-cho-giáo-viên--quản-trị-viên-admin--teacher-guide)
4. [Phần 4: Cơ chế Bảo mật, Chống gian lận & Đảm bảo Tính Toàn vẹn Dữ liệu](#phần-4-cơ-chế-bảo-mật-chống-gian-lận--đảm-bảo-tính-toàn-vẹn-dữ-liệu)
5. [Phần 5: Bảng Tổng hợp Tính năng & Phím tắt Thao tác Nhanh](#phần-5-bảng-tổng-hợp-tính-năng--phím-tắt-thao-tác-nhanh)
6. [Phần 6: Các Câu hỏi Thường gặp (FAQ) & Xử lý Sự cố](#phần-6-các-câu-hỏi-thường-gặp-faq--xử-lý-sự-cố)
7. [Phần 7: Phụ lục Hướng dẫn Cài đặt & Khởi chạy Nhanh (Dành cho Kỹ thuật viên)](#phần-7-phụ-lục-hướng-dẫn-cài-đặt--khởi-chạy-nhanh-dành-cho-kỹ-thuật-viên)

---

## PHẦN 1: GIỚI THIỆU TỔNG QUAN, ĐĂNG KÝ & ĐĂNG NHẬP HỆ THỐNG

### 1.1. Giới thiệu Nền tảng ELearning
ELearning là giải pháp thi trực tuyến và đào tạo số chuyên nghiệp, được xây dựng theo chuẩn kiến trúc Clean Architecture hiện đại: Backend .NET Core API hiệu năng cao kết hợp cơ sở dữ liệu quan hệ PostgreSQL 17 và giao diện người dùng React / TypeScript / Vite mượt mà, phản hồi tức thì.

**Các đặc tính nổi bật:**
- **Tự động lưu bài tức thời (Autosave):** Từng câu trả lời của thí sinh được lưu ngầm về máy chủ ngay lập tức, chống mất dữ liệu khi mất mạng hoặc sập nguồn điện.
- **Chống gian lận & Giám sát làm bài:** Tự động phát hiện và ghi nhận các sự kiện thí sinh chuyển tab, mất tiêu điểm hoặc thoát cửa sổ làm bài.
- **Hỗ trợ đa dạng loại câu hỏi:** Trắc nghiệm 1 đáp án, Trắc nghiệm nhiều lựa chọn (hỗ trợ chấm từng phần Partial Scoring), Đúng / Sai, Điền từ ngữ / Số học (hỗ trợ sai số dung sai), và Tự luận (chấm tay kèm nhận xét).
- **Kho Video bài giảng đa nguồn:** Tích hợp phát YouTube (chuẩn, Shorts, Live), Google Drive preview và Video tệp trực tiếp MP4/WebM không quảng cáo.
- **Đề thi nhiều phiên bản (Multi-version):** Cho phép xuất bản phiên bản mới mà vẫn bảo lưu nguyên vẹn lịch sử kết quả của các phiên bản cũ.
- **Quản trị linh hoạt:** Bật/tắt tự đăng ký học viên, mở/khóa xem chi tiết đáp án chỉ bằng 1 cú nhấp chuột.

---

### 1.2. Hướng dẫn Đăng ký Tài khoản (Dành cho Học viên mới)
Khi tính năng tự đăng ký được Quản trị viên kích hoạt:
1. Mở trình duyệt Web, truy cập liên kết: `http://localhost:5173/register` (hoặc bấm **Đăng ký ngay** tại trang Đăng nhập).
2. Điền đầy đủ các thông tin:
   - **Tên đăng nhập (Username):** Viết liền không dấu, từ 3 ký tự trở lên (Ví dụ: `nguyenvanan`).
   - **Họ và tên đầy đủ:** Nhập tên có dấu (Ví dụ: `Nguyễn Văn An`).
   - **Địa chỉ Email:** Nhập email cá nhân đang hoạt động để nhận thông báo.
   - **Mật khẩu:** Nhập mật khẩu tối thiểu 8 ký tự (nên gồm cả chữ và số).
   - **Xác nhận mật khẩu:** Nhập lại chính xác mật khẩu vừa đặt.
3. Nhấn nút **Đăng ký tài khoản**. Hệ thống sẽ tự động tạo tài khoản và đưa bạn về màn hình Đăng nhập để bắt đầu sử dụng.

> ⚠️ **Lưu ý khi tính năng Tự đăng ký bị khóa:**  
> Nếu Quản trị viên đang tắt tính năng tự đăng ký, trang Đăng ký sẽ xuất hiện thông báo màu vàng: *"Tính năng tự đăng ký tài khoản hiện đang tạm khóa. Vui lòng liên hệ quản trị viên hoặc giáo viên để được cấp tài khoản."* Đồng thời nút Đăng ký sẽ bị vô hiệu hóa để bảo đảm an toàn dữ liệu trường học.

---

### 1.3. Hướng dẫn Đăng nhập Hệ thống
1. Truy cập địa chỉ hệ thống trên trình duyệt: `http://localhost:5173/login`.
2. Nhập **Tên đăng nhập** và **Mật khẩu** được cấp.
3. Bấm nút **Đăng nhập**. Hệ thống sẽ tự động chuyển hướng theo đúng vai trò:
   - **Tài khoản Học viên (Student):** Tự động chuyển tới trang Danh sách Kỳ thi (`/student/exams`).
   - **Tài khoản Quản trị / Giáo viên (Admin):** Tự động chuyển tới Bảng điều khiển Quản trị (`/admin/dashboard`).

> 💡 **Tài khoản thử nghiệm sẵn có trên hệ thống:**  
> - **Quản trị viên (Admin):** `admin` / Mật khẩu: `Admin@123456`  
> - **Học viên mẫu (Student):** `student01` hoặc `student02` / Mật khẩu: `Student@123456`

---

### 1.4. Đổi Mật khẩu & Quản lý Thông tin Cá nhân
Để bảo vệ tài khoản, người dùng nên đổi mật khẩu ngay sau lần đăng nhập đầu tiên:
1. Bấm vào **Tên người dùng** hoặc Avatar ở góc trên cùng bên phải thanh menu -> Chọn **Đổi mật khẩu** (`/change-password`).
2. Nhập Mật khẩu hiện tại, sau đó nhập Mật khẩu mới và Xác nhận lại.
3. Bấm **Lưu thay đổi**. Hệ thống sẽ cập nhật ngay lập tức.
4. Khi cần kết thúc phiên làm việc trên máy tính công cộng, bấm menu Avatar -> Chọn **Đăng xuất** để xóa phiên đăng nhập an toàn.

---

## PHẦN 2: CẨM NANG SỬ DỤNG DÀNH CHO HỌC VIÊN (STUDENT GUIDE)

### 2.1. Thanh Điều Hướng & Các Phân Hệ Chính
Sau khi đăng nhập, học viên có thanh menu trên cùng với các phân hệ chức năng:
- **Kỳ thi & Bài kiểm tra (`/student/exams`):** Nơi tập trung toàn bộ đề thi mà bạn có quyền tham gia.
- **Lớp học của tôi (`/student/classes`):** Tra cứu lớp học, giáo viên phụ trách và danh sách bạn cùng lớp.
- **Video bài giảng (`/student/videos`):** Kho học liệu số với các bài giảng video phong phú theo từng chuyên đề.
- **Lịch sử làm bài (`/student/history`):** Nhật ký lưu trữ toàn bộ các bài thi đã làm kèm điểm số và thống kê.

---

### 2.2. Phân Hệ Kỳ Thi & Bài Kiểm Tra
Tại trang `/student/exams`, học viên có thể xem danh sách tất cả các bài thi khả dụng:
- **Thẻ thông tin đề thi:** Hiển thị Mã đề, Tên đề thi, Thời lượng làm bài (phút), Điểm đạt yêu cầu (%), Thời hạn làm bài (bắt đầu - kết thúc) và Số lượt thi còn lại (Ví dụ: Còn 1 lượt, hoặc Thi tự do không giới hạn).
- **Phân loại đề thi:** Đề *Công khai* (mọi học sinh đều được làm) và Đề *Chỉ định* (chỉ dành riêng cho lớp hoặc học sinh cụ thể).
- **Trạng thái đề thi:** `SẮP DIỄN RA` (chưa đến giờ mở), `ĐANG MỞ` (nút Bắt đầu sáng màu xanh), `ĐÃ KẾT THÚC` (hết hạn thi).
- **Xem thể lệ & Hướng dẫn:** Bấm nút **Xem chi tiết** để đọc kỹ quy chế và yêu cầu của giáo viên trước khi vào thi.

---

### 2.3. Quy Trình Làm Bài Thi Trực Tuyến Từ A Đến Z (Exam Player)
Khi bấm **Bắt đầu làm bài**, học viên sẽ bước vào giao diện phòng thi chuyên nghiệp:

#### 1. Các thành phần trên màn hình phòng thi
- **Đồng hồ đếm ngược (Exam Timer):** Luôn hiển thị ở góc trên màn hình. Đồng hồ chuyển màu vàng cam khi còn dưới 5 phút, và chuyển màu đỏ nhấp nháy khi còn dưới 1 phút.
- **Tiến độ làm bài:** Hiển thị số câu đã hoàn thành (Ví dụ: *Đã trả lời 18/40 câu*).
- **Bảng danh sách câu hỏi (Question Navigation Panel):** Đánh số từ câu 1 đến câu N. Mã màu trực quan:
  - *Màu xám:* Câu hỏi chưa trả lời.
  - *Màu xanh dương:* Câu hỏi đã chọn hoặc đã điền đáp án thành công.
  - *Biểu tượng lá cờ vàng:* Câu hỏi được bạn đánh dấu xem lại.
- **Nút "Đánh dấu xem lại" (Bookmark):** Cho phép bạn gắn cờ những câu phân vân để dễ dàng click quay lại kiểm tra trước khi nộp bài.

#### 2. Hướng dẫn thao tác theo từng dạng câu hỏi

| Dạng câu hỏi | Thao tác của Học sinh | Đặc điểm tính điểm |
| :--- | :--- | :--- |
| **Trắc nghiệm chọn 1 (Single Choice)** | Click vào nút tròn (Radio) của 1 phương án duy nhất (A, B, C hoặc D). Muốn đổi đáp án chỉ cần click phương án khác. | Đúng nhận trọn điểm, sai nhận 0 điểm. |
| **Trắc nghiệm nhiều đáp án (Multiple Choice)** | Click vào ô vuông (Checkbox) của các phương án đúng. Câu hỏi có thể có 2, 3 hoặc nhiều đáp án đúng. | Hỗ trợ Chấm từng phần: Chọn đúng một phần vẫn được cộng điểm tương ứng. |
| **Đúng / Sai (True / False)** | Click chọn nút "Đúng" hoặc nút "Sai" theo nhận định của đề bài. | Chấm điểm chuẩn xác theo đáp án. |
| **Điền từ ngữ / Số học (Fill In)** | Nhập trực tiếp câu trả lời vào ô trống. Với câu hỏi số học, có thể nhập số nguyên hoặc thập phân (hỗ trợ dấu chấm hoặc dấu phẩy). | Hỗ trợ dung sai sai số (Tolerance) và không phân biệt chữ hoa/thường. |
| **Tự luận (Essay)** | Nhập bài làm vào khung soạn thảo văn bản. Có thể gõ nhiều đoạn văn, trình bày chi tiết các bước giải. | Bài thi sẽ được giáo viên chấm tay và ghi nhận xét sau khi nộp. |

> 💡 **Cơ chế Lưu bài tự động (Autosave):**  
> Mỗi khi bạn chọn đáp án hoặc gõ một ký tự, hệ thống sẽ gửi tín hiệu lưu ngầm về máy chủ trong vài mili-giây. Bạn không cần lo lắng về sự cố mất mạng, tắt máy hay sập nguồn. Khi đăng nhập lại trên bất kỳ thiết bị nào, toàn bộ câu trả lời đã làm sẽ được khôi phục nguyên vẹn 100%!

#### 3. Cảnh báo chống gian lận trong khi làm bài
- **Phát hiện chuyển tab hoặc thu nhỏ trình duyệt:** Khi bạn rời khỏi màn hình làm bài sang tab khác để tra cứu, hệ thống sẽ lập tức hiện hộp thoại cảnh báo và ghi nhận sự kiện vào nhật ký thi.
- **Vi phạm nhiều lần:** Quản trị viên trong phòng giám sát có thể áp dụng hình thức cảnh cáo hoặc buộc nộp bài ngay lập tức.

#### 4. Nộp bài thi
- **Nộp bài chủ động:** Sau khi làm xong và kiểm tra lại danh sách câu hỏi, bấm nút **Nộp bài** ở góc trên bên phải. Hệ thống sẽ mở bảng tóm tắt: Đã làm bao nhiêu câu, còn sót câu nào chưa làm, các câu đã bookmark. Bấm **Xác nhận nộp bài** để hoàn tất.
- **Tự động nộp bài khi hết giờ:** Nếu đồng hồ đếm ngược chạm mốc `00:00:00`, bài thi sẽ tự động khóa và gửi toàn bộ các câu trả lời đã lưu lên máy chủ. Bạn không cần thao tác gì thêm và không bao giờ bị mất bài làm.

---

### 2.4. Tra Cứu Kết Quả & Xem Lời Giải Chi Tiết
Sau khi nộp bài, màn hình kết quả sẽ lập tức hiển thị:
- **Biểu đồ điểm số:** Điểm đạt được trên thang điểm tối đa (Ví dụ: 8.5 / 10.0) và tỉ lệ phần trăm.
- **Đánh giá kết quả:** Hiển thị huy hiệu **ĐẠT** (Màu xanh lá) nếu đạt trên điểm chuẩn quy định, hoặc **CHƯA ĐẠT** (Màu đỏ) nếu dưới điểm chuẩn.
- **Thống kê chi tiết:** Thời gian làm bài thực tế, số câu trả lời đúng, số câu trả lời sai.
- **Chính sách xem lại đề thi (Review Policy):**
  - *Khi Giáo viên đang khóa xem lại:* Hiển thị icon ổ khóa 🔒 kèm thông báo *"Kết quả bài thi đã được ghi nhận. Chi tiết câu hỏi và đáp án sẽ hiển thị sau khi giáo viên / quản trị viên mở quyền xem lại."*
  - *Khi Giáo viên mở quyền xem lại:* Bạn sẽ xem được toàn bộ danh sách câu hỏi trong đề, với: Phương án bạn đã chọn (Badge xanh nếu đúng, Badge đỏ nếu sai), Đáp án chuẩn xác của đề thi và Lời giải thích chi tiết của giáo viên.

---

### 2.5. Kho Video Bài Giảng Đa Nguồn (`/student/videos`)
Hệ thống trang bị kho video bài giảng hiện đại, hỗ trợ nhiều nguồn học liệu phong phú:
- **YouTube tiêu chuẩn & YouTube Shorts:** Hỗ trợ cả video bài giảng thông thường và các video ngắn (Shorts) với định dạng `youtube.com/shorts/...` chạy cực kỳ mượt mà.
- **YouTube Live stream:** Xem các buổi livestream giảng bài trực tiếp.
- **Google Drive preview:** Xem video lưu trữ trên Google Drive trường học mà không cần cấp quyền tải về máy.
- **Video trực tiếp (MP4 / WebM):** Trình phát HTML5 native chất lượng cao, có đầy đủ công cụ tua thời gian, đổi tốc độ phát (0.75x, 1x, 1.25x, 1.5x) và xem toàn màn hình.
- **Lọc theo Chuyên đề & Lớp học:** Dễ dàng tìm nhanh bài giảng theo môn Toán, Lý, Hóa, Ngoại ngữ hoặc chuyên đề riêng của lớp mình.

---

### 2.6. Lớp Học Của Tôi & Lịch Sử Làm Bài
- **Lớp học của tôi (`/student/classes`):** Xem danh sách các lớp bạn đã được phân công, thông tin giáo viên chủ nhiệm / bộ môn và danh sách bạn bè cùng lớp.
- **Lịch sử làm bài (`/student/history`):** Lưu trữ danh sách đầy đủ tất cả các lượt thi bạn đã từng tham gia từ trước đến nay, ngày giờ nộp bài, điểm số và trạng thái Đạt/Chưa đạt để theo dõi tiến độ tiến bộ.

---

## PHẦN 3: CẨM NANG NGHIỆP VỤ DÀNH CHO GIÁO VIÊN & QUẢN TRỊ VIÊN (ADMIN & TEACHER GUIDE)

### 3.1. Bảng Điều Khiển Trung Tâm (Dashboard)
Trang Dashboard (`/admin/dashboard`) cung cấp góc nhìn toàn cảnh về tình hình vận hành:
- **4 thẻ chỉ số thống kê (KPIs):** Tổng số học sinh, Tổng số đề thi đang hoạt động, Tổng số lượt làm bài, Điểm trung bình toàn trường.
- **Biểu đồ phân bổ điểm số:** Biểu thị tỉ lệ học sinh đạt điểm Giỏi (9-10), Khá (7-8), Trung bình (5-6) và Chưa đạt (<5).
- **Danh sách bài tự luận chờ chấm:** Cảnh báo nhắc nhở giáo viên các bài thi có câu tự luận cần chấm điểm thủ công.

---

### 3.2. Quản Lý Người Dùng & Điều Khiển Tự Đăng Ký (`/admin/users`)

#### 1. Bật / Tắt tính năng tự đăng ký học viên chỉ với 1 click
Ngay đầu trang Quản lý Người dùng có thẻ **"Cấu hình Đăng ký tài khoản học sinh"**:
- **Nếu đang MỞ:** Nút bấm hiển thị **"Khóa tự đăng ký"** (Màu đỏ). Học sinh bên ngoài có thể tự vào đăng ký tài khoản.
- **Nếu đang KHÓA:** Nút bấm hiển thị **"Mở tự đăng ký"** (Màu xanh lá). Học sinh bên ngoài không thể tự đăng ký, bảo đảm an toàn dữ liệu.
- **Thao tác:** Quản trị viên chỉ cần click 1 lần vào nút này. Cấu hình sẽ được lưu vào cơ sở dữ liệu và có hiệu lực tức thì mà không cần khởi động lại máy chủ.

#### 2. Quản lý tài khoản học sinh và giáo viên
- **Thêm người dùng mới:** Nhấn nút **Thêm người dùng**, nhập Username, Họ và tên, Email, Mật khẩu ban đầu và chọn Vai trò (`Student`, `Teacher`, `Admin`).
- **Khóa / Kích hoạt tài khoản:** Khi cần tạm khóa một học sinh, chỉ cần gạt switch trạng thái sang *Đã khóa*. Khi mở lại chỉ cần click lại.
- **Đặt lại mật khẩu (Reset Password):** Hỗ trợ học sinh trong trường hợp quên mật khẩu chỉ bằng 1 thao tác nhanh.

---

### 3.3. Quản Lý Lớp Học & Phân Phối Học Viên (`/admin/classes`)
1. **Tạo lớp học mới:** Nhập Tên lớp (Ví dụ: `Lớp 12A1`), Mã lớp (`12A1-2026`), Niên khóa, Ngày bắt đầu và Ngày kết thúc.
2. **Thêm học viên vào lớp:** Tìm kiếm học sinh theo tên hoặc mã và thêm vào lớp học.
3. **Tác dụng của Lớp học:** Dùng để phân quyền giao bài thi chỉ định (chỉ học sinh trong lớp mới được thi) và chia sẻ video bài giảng cho riêng lớp đó.

---

### 3.4. Quản Lý Ngân Hàng Câu Hỏi (Question Bank)

#### 1. Phân loại Chuyên đề môn học (`/admin/categories`)
Tạo các chuyên đề để nhóm câu hỏi theo chủ đề (Ví dụ: `Toán - Giải tích`, `Lập trình C#`, `Tiếng Anh - Ngữ pháp`...).

#### 2. Tạo câu hỏi mới (`/admin/questions/create`)
Trình soạn thảo câu hỏi hỗ trợ các tính năng cao cấp:
- **Soạn thảo Markdown & Code block:** Hỗ trợ in đậm, in nghiêng, danh sách, công thức và khối mã nguồn có syntax highlighting.
- **Nút "Chèn ảnh" trực quan:** Bạn có thể chọn file ảnh từ máy tính (hỗ trợ JPG, PNG, GIF, WebP). Hệ thống sẽ tự động tối ưu hóa, lưu trữ an toàn theo mã băm SHA-256 và chèn thẻ ảnh Markdown vào đúng vị trí con trỏ.
- **Cấu hình Chấm từng phần (Partial Scoring):** Đối với câu trắc nghiệm nhiều đáp án, bật tính năng này để học sinh chọn đúng 2/3 đáp án vẫn được 66.7% số điểm của câu.
- **Giải thích chi tiết:** Soạn lời giải thích để học sinh đọc hiểu sau khi được mở quyền xem lại bài thi.

#### 3. Nhập câu hỏi hàng loạt từ Excel (Import Excel)
Để tiết kiệm thời gian khi đưa hàng trăm câu hỏi vào hệ thống:
1. Tại trang Ngân hàng câu hỏi, nhấn nút **Tải file mẫu Excel** (`Template_Import_CauHoi.xlsx`).
2. Điền dữ liệu vào các cột quy định: Mã câu hỏi, Nội dung, Mã chuyên đề, Dạng câu, Các phương án A, B, C, D, Đáp án đúng, Điểm mặc định, Độ khó, Lời giải.
3. Nhấn nút **Nhập từ Excel** -> Chọn file vừa điền -> Bấm **Kiểm tra trước** (Dry Run).
4. Hệ thống sẽ quét toàn bộ dữ liệu. Nếu có dòng nào thiếu thông tin hoặc sai mã chuyên đề, hệ thống sẽ báo chính xác số dòng lỗi để bạn sửa. Khi kết quả báo *Hợp lệ 100%*, nhấn **Thực hiện nhập** để hoàn tất.

---

### 3.5. Xây Dựng, Xuất Bản & Quản Trị Đề Thi (`/admin/exams`)

#### 1. Tạo đề thi mới
Bấm nút **Tạo đề thi** và thiết lập các thông số:
- **Tên đề thi & Mã đề:** Ví dụ `Kiểm tra Giữa kỳ 1 - Lập trình C#` (Mã: `CS-MID-01`).
- **Thời lượng:** Số phút làm bài (Ví dụ: 45 phút, 60 phút, 90 phút).
- **Điểm đạt yêu cầu:** Tỉ lệ phần trăm để xếp loại Đạt (Ví dụ: 50% hoặc 60%).
- **Khung giờ thi:** Đặt ngày giờ Bắt đầu và Kết thúc mở đề.
- **Số lượt thi tối đa:** Đặt 1 lượt (cho kỳ thi chính thức) hoặc nhiều lượt (cho đề ôn tập).
- **Chế độ truy cập (Access Mode):**
  - `PUBLIC`: Mọi học viên đều nhìn thấy và được làm bài.
  - `ASSIGNED`: Chỉ những Lớp học hoặc Học viên được phân công mới có quyền vào thi.
- **Bảo mật đề:** Tick chọn *Xáo trộn thứ tự câu hỏi* và *Xáo trộn thứ tự phương án* để mỗi học sinh có một mã đề hiển thị khác nhau, hạn chế nhìn bài nhau.

#### 2. Trình dựng đề thi (Version Builder)
1. Nhấn nút **Thêm câu hỏi từ ngân hàng**: Lọc theo chuyên đề, độ khó và tick chọn câu hỏi đưa vào đề.
2. **Phân bổ điểm số:** Có thể điều chỉnh điểm số từng câu (Ví dụ câu dễ 0.25đ, câu khó 1.0đ, tự luận 2.0đ).
3. Bấm **Xuất bản đề thi** (Publish).

> 💡 **Cơ chế Đóng băng Snapshot Đề thi:**  
> Khi bạn bấm *Xuất bản*, hệ thống sẽ tạo một bản snapshot độc lập lưu trữ toàn bộ nội dung câu hỏi và đáp án của đề thi tại thời điểm đó. Dù sau này bạn có chỉnh sửa hoặc xóa câu hỏi trong Ngân hàng câu hỏi, đề thi đã xuất bản vẫn được bảo toàn nguyên vẹn 100%, không bao giờ bị sai lệch kết quả!

#### 3. Bật / Tắt Quyền xem kết quả chỉ với 1 Click
Tại trang chi tiết đề thi (`/admin/exams/:id`), quản trị viên có thẻ điều khiển **Quyền xem lại kết quả bài thi**:
- **Nút "Mở cho học sinh xem chi tiết":** Khi ca thi kết thúc, bấm nút này để toàn bộ thí sinh được xem chi tiết bài làm, đáp án đúng và lời giải thích.
- **Nút "Khóa xem lại chi tiết":** Khóa quyền xem lại tức thì nếu cần bảo mật đề cho các ca thi tiếp theo.

---

### 3.6. Giám Sát Phòng Thi, Chấm Tự Luận & Xuất Báo Cáo

#### 1. Giám sát phòng thi thời gian thực (`/admin/exams/:id/results`)
- **Theo dõi thí sinh:** Xem danh sách học sinh đang trong trạng thái `ĐANG LÀM` (IN_PROGRESS), thời gian còn lại và tiến độ trả lời.
- **Gia hạn thời gian (Add time):** Nếu một học sinh gặp sự cố máy móc hoặc mạng, giáo viên có thể cộng thêm số phút làm bài cho riêng học sinh đó ngay trên màn hình giám sát.
- **Buộc nộp bài (Force submit):** Buộc kết thúc lượt thi của thí sinh cố tình vi phạm quy chế.

#### 2. Chấm bài tự luận (Manual Grading)
1. Vào tab **Chấm tự luận** trong kết quả đề thi.
2. Chọn bài làm của học sinh: Màn hình hiển thị nội dung câu hỏi tự luận, đáp án gợi ý của giáo viên và bài làm thực tế của học sinh.
3. Nhập số điểm chấm (từ 0 đến điểm tối đa của câu) và nhập **Nhận xét của giáo viên**.
4. Bấm **Lưu điểm chấm**. Hệ thống sẽ tự động cập nhật lại tổng điểm và xếp loại cho học sinh.

#### 3. Sửa đáp án & Chấm lại tự động (Regrade)
Trong trường hợp hy hữu đề thi có một câu hỏi bị nhầm đáp án sau khi học sinh đã nộp bài:
1. Vào chi tiết đề thi -> Bấm **Sửa đáp án / Chấm lại**.
2. Chọn câu hỏi cần sửa: Cập nhật lại đáp án đúng chuẩn hoặc chọn **Hủy câu** (mọi học sinh đều được trọn điểm câu này).
3. Nhập lý do thay đổi và bấm **Chấm lại toàn bộ lượt thi**.
4. Hệ thống sẽ tự động quét lại toàn bộ hàng trăm bài làm của học sinh và tính lại điểm số chuẩn xác chỉ trong vài giây, đồng thời ghi lại nhật ký kiểm toán (Audit Log).

#### 4. Xuất Báo cáo Kết quả ra file Excel
Nhấn nút **Xuất Excel** tại trang kết quả đề thi. Hệ thống sẽ tạo file Excel hoàn chỉnh gồm: Mã học viên, Họ và tên, Lớp học, Điểm thi, Tỉ lệ phần trăm, Số câu đúng/sai, Thời gian bắt đầu, Thời gian nộp bài và Xếp loại (Đạt/Chưa đạt).

---

### 3.7. Quản Lý Kho Video Bài Giảng Đa Nguồn (`/admin/videos`)
Quản trị viên có thể thêm bài giảng từ nhiều nguồn khác nhau:
- **YouTube thông thường & YouTube Shorts:** Dán link YouTube (dạng `watch?v=...` hoặc `shorts/...`). Hệ thống sẽ tự động bóc tách Video ID và lấy ảnh bìa đại diện (Thumbnail HD).
- **Google Drive video:** Dán liên kết chia sẻ Google Drive (chú ý đặt quyền *"Bất kỳ ai có đường liên kết đều có thể xem"*).
- **File video trực tiếp (MP4 / WebM):** Dán URL trực tiếp tới file video.
- **Phân quyền:** Gán Chuyên đề môn học và chọn Lớp học được phép xem video.

---

## PHẦN 4: CƠ CHẾ BẢO MẬT & ĐẢM BẢO TÍNH TOÀN VẸN

### 4.1. Nguyên Tắc "Máy Chủ Là Nguồn Dữ Liệu Chuẩn" (Server-side Validation)
- Toàn bộ việc tính giờ, kiểm soát thời hạn nộp bài và chấm điểm đều thực hiện 100% tại máy chủ backend .NET 10.
- **Bảo mật đề thi:** Khi học sinh nhận đề thi, dữ liệu gửi về trình duyệt chỉ bao gồm nội dung câu hỏi và các phương án lựa chọn. Đáp án đúng và lời giải thích TUYỆT ĐỐI KHÔNG tồn tại trên máy học sinh trong suốt quá trình làm bài, loại bỏ triệt để nguy cơ soi mã nguồn (Inspect Element) để gian lận.
- **Chống nộp trùng lặp (Idempotent):** Dù học sinh có bấm nộp bài nhiều lần liên tiếp do mạng lag, máy chủ chỉ ghi nhận và chấm điểm đúng một lần duy nhất.

### 4.2. Nhật Ký Giám Sát Hệ Thống (Audit Logs)
Mọi hành động quan trọng trên hệ thống đều được lưu vết vĩnh viễn trong Nhật ký giám sát (`/admin/audit-logs`):
- Ai đã tạo, chỉnh sửa hoặc xuất bản đề thi?
- Ai đã mở quyền hoặc khóa quyền xem lại kết quả bài thi?
- Ai đã gia hạn thời gian, sửa đáp án hoặc chấm lại bài thi?
- Ai đã bật/tắt tính năng đăng ký tài khoản học sinh?

---

## PHẦN 5: BẢNG TỔNG HỢP TÍNH NĂNG & PHÍM TẮT THAO TÁC

### 5.1. Ma Trận Phân Quyền Tính Năng Theo Vai Trò

| Chức năng / Phân hệ | Học viên (Student) | Giáo viên (Teacher) | Quản trị viên (Admin) |
| :--- | :---: | :---: | :---: |
| **Xem danh sách đề thi & Làm bài** | Có (đề khả dụng) | Có | Có |
| **Xem kết quả & Điểm số cá nhân** | Có | Có | Có |
| **Xem chi tiết lời giải đề thi** | Khi được mở quyền | Luôn xem được | Luôn xem được |
| **Xem video bài giảng đa nguồn** | Có (theo phân quyền) | Có | Có |
| **Bật/tắt tự đăng ký học viên** | Không | Không | Có (1 Click) |
| **Soạn câu hỏi & Chèn ảnh** | Không | Có | Có |
| **Nhập câu hỏi từ Excel mẫu** | Không | Có | Có |
| **Tạo & Xuất bản đề thi** | Không | Có | Có |
| **Mở/Khóa xem đáp án chi tiết** | Không | Có (đề của mình) | Có (1 Click) |
| **Giám sát phòng thi & Thêm giờ** | Không | Có | Có |
| **Chấm bài thi tự luận** | Không | Có | Có |
| **Sửa đáp án & Chấm lại (Regrade)** | Không | Có | Có |
| **Xuất bảng điểm ra file Excel** | Không | Có | Có |
| **Quản lý người dùng & Đổi quyền** | Không | Không | Có |

---

## PHẦN 6: CÂU HỎI THƯỜNG GẶP (FAQ) & XỬ LÝ SỰ CỐ

| Tình huống / Câu hỏi | Nguyên nhân & Hướng dẫn xử lý |
| :--- | :--- |
| **Học sinh bị mất mạng hoặc sập nguồn điện khi đang thi?** | Học sinh chỉ cần mở lại máy tính hoặc chuyển sang thiết bị khác, đăng nhập tài khoản và bấm vào đề thi để làm tiếp. Toàn bộ các câu trả lời trước đó đã được hệ thống lưu tự động qua cơ chế Autosave. |
| **Học sinh thi xong nhưng không thấy câu nào đúng, câu nào sai?** | Do Giáo viên đang đặt chế độ bảo mật "Khóa xem lại chi tiết". Điểm tổng quan của bạn đã được lưu an toàn. Khi đợt thi kết thúc và giáo viên bấm "Mở cho học sinh xem chi tiết", bạn sẽ xem được toàn bộ lời giải. |
| **Trang đăng ký báo "Tính năng tự đăng ký hiện đang tạm khóa"?** | Do Quản trị viên đang khóa tính năng tự do đăng ký để quản lý danh sách học sinh chặt chẽ. Hãy liên hệ với Quản trị viên hoặc Giáo viên để được cấp tài khoản chính thức. |
| **Đề thi bị hết số lượt làm nhưng giáo viên đã cập nhật đề mới?** | Hệ thống hỗ trợ Đề thi nhiều phiên bản (Multi-version). Khi giáo viên xuất bản phiên bản mới (v2), số lượt làm của học sinh sẽ được làm mới cho phiên bản đó, học sinh chỉ cần bấm "Bắt đầu làm bài" phiên bản mới. |
| **Nhập file Excel câu hỏi bị báo lỗi dòng?** | Bấm xem chi tiết thông báo lỗi: Kiểm tra lại tên sheet là `CauHoi`, các cột không được đổi tên. Hãy đảm bảo Mã chuyên đề trùng khớp với chuyên đề đã tạo trong hệ thống và các phương án lựa chọn được điền liên tục từ A, B, C. |
| **Video YouTube hoặc Google Drive báo lỗi không phát được?** | • **YouTube:** Video phải để ở chế độ "Công khai" (Public) hoặc "Không công khai" (Unlisted). Không để chế độ "Riêng tư" (Private).<br>• **Google Drive:** Đường link chia sẻ phải được bật quyền "Bất kỳ ai có đường liên kết đều có thể xem". |

---

## PHẦN 7: PHỤ LỤC HƯỚNG DẪN KHỞI CHẠY (DÀNH CHO KỸ THUẬT)

### 7.1. Yêu Cầu Môi Trường Kỹ Thuật
- **Hệ điều hành:** Windows 10/11, Windows Server hoặc Linux (Ubuntu 22.04+).
- **Cơ sở dữ liệu:** PostgreSQL 16 hoặc 17.
- **Môi trường thực thi:** .NET SDK 10 (hoặc .NET 9) và Node.js phiên bản 20+.

### 7.2. Các Lệnh Khởi Động Nhanh

#### 1. Chạy Backend API (.NET 10)
Mở cửa sổ Terminal tại thư mục dự án:
```bash
cd backend
dotnet ef database update -p src/ELearning.Infrastructure -s src/ELearning.Api
dotnet run --project src/ELearning.Api --launch-profile http
```
-> Backend API chạy tại: `http://localhost:5136` (Swagger UI: `http://localhost:5136/swagger`)

#### 2. Chạy Giao Diện Web Frontend (React Vite)
Mở cửa sổ Terminal thứ hai:
```bash
cd frontend/elearning-web
npm install
npm run dev
```
-> Giao diện Web chạy tại: `http://localhost:5173`

#### 3. Hoặc chạy toàn bộ bằng Docker Compose
```bash
docker compose up -d --build
```
-> Toàn bộ ứng dụng sẵn sàng tại địa chỉ: `http://localhost:8080`

---
*Tài liệu được cập nhật tự động đồng bộ giữa định dạng Word (.docx) và Markdown (.md).*
