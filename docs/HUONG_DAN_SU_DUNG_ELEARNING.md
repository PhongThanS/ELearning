# HƯỚNG DẪN SỬ DỤNG HỆ THỐNG THI TRỰC TUYẾN & ĐÀO TẠO ELEARNING
*(Tài liệu hướng dẫn thao tác toàn diện dành cho Học viên, Giáo viên và Quản trị viên)*

---

## 📌 THÔNG TIN TÀI LIỆU
- **Tên phần mềm:** Hệ thống Thi Trực Tuyến & Đào tạo ELearning
- **Phiên bản:** 2.0 (PostgreSQL 17, .NET 10, React 19 / TypeScript)
- **File Word (.docx):** Đã được tạo tại thư mục gốc: `HUONG_DAN_SU_DUNG_ELEARNING.docx` và thư mục `docs/HUONG_DAN_SU_DUNG_ELEARNING.docx`

---

## MỤC LỤC
1. [Phần 1: Giới thiệu Tổng quan & Thông tin Đăng nhập Hệ thống](#phần-1-giới-thiệu-tổng-quan--thông-tin-đăng-nhập-hệ-thống)
2. [Phần 2: Hướng dẫn dành cho Học viên (Student Manual)](#phần-2-hướng-dẫn-dành-cho-học-viên-student-manual)
3. [Phần 3: Hướng dẫn dành cho Quản trị viên & Giáo viên (Admin & Teacher Manual)](#phần-3-hướng-dẫn-dành-cho-quản-trị-viên--giáo-viên-admin--teacher-manual)
4. [Phần 4: Cơ chế Bảo mật & Đảm bảo Tính Toàn vẹn Dữ liệu](#phần-4-cơ-chế-bảo-mật--đảm-bảo-tính-toàn-vẹn-dữ-liệu)
5. [Phần 5: Câu hỏi Thường gặp (FAQ) & Xử lý Sự cố](#phần-5-câu-hỏi-thường-gặp-faq--xử-lý-sự-cố)
6. [Phần 6: Phụ lục Hướng dẫn Cài đặt & Khởi động Hệ thống](#phần-6-phụ-lục-hướng-dẫn-cài-đặt--khởi-động-hệ-thống)

---

## PHẦN 1: GIỚI THIỆU TỔNG QUAN & ĐĂNG NHẬP HỆ THỐNG

### 1.1. Giới thiệu Nền tảng ELearning
ELearning là hệ thống thi trực tuyến và đào tạo số chuyên nghiệp, được xây dựng trên nền tảng công nghệ:
- **Backend:** .NET Core Web API (Clean Architecture).
- **Cơ sở dữ liệu:** PostgreSQL 17.
- **Frontend:** React, TypeScript, Vite, Bootstrap 5.

**Các tính năng nổi bật:**
- **Tự động lưu bài (Autosave):** Lưu câu trả lời tức thời về máy chủ sau mỗi thay đổi, chống mất dữ liệu khi mất mạng hoặc sập nguồn.
- **Chống gian lận:** Phát hiện và ghi nhận sự kiện chuyển tab, mất focus cửa sổ.
- **Đa dạng dạng câu hỏi:** Trắc nghiệm chọn 1, Trắc nghiệm nhiều lựa chọn (hỗ trợ chấm từng phần), Đúng / Sai, Điền từ ngữ / Số học, và Tự luận (chấm tay).
- **Kho Video bài giảng:** Nhúng YouTube an toàn (YouTube-NoCookie), phân loại theo chuyên đề và lớp học.
- **Đề thi nhiều phiên bản (Multi-version):** Hỗ trợ xuất bản phiên bản mới mà vẫn bảo lưu lịch sử thi cũ.

### 1.2. Hướng dẫn Đăng nhập & Đổi mật khẩu
1. Mở trình duyệt Web (Chrome, Edge, Safari...).
2. Truy cập địa chỉ hệ thống (Mặc định: `http://localhost:5173`).
3. Điền **Tên đăng nhập** và **Mật khẩu** -> Bấm **Đăng nhập**.

> **Tài khoản mặc định thử nghiệm:**
> - Quản trị viên: `admin` / Mật khẩu: `Admin@123456`
> - Học viên: `student01` hoặc `student02` / Mật khẩu: `Student@123456`

**Đổi mật khẩu:**
- Bấm vào tên người dùng ở góc trên bên phải -> Chọn **Đổi mật khẩu** (`/change-password`).
- Nhập mật khẩu cũ, mật khẩu mới (tối thiểu 8 ký tự) -> Bấm **Lưu thay đổi**.

---

## PHẦN 2: HƯỚNG DẪN DÀNH CHO HỌC VIÊN (STUDENT MANUAL)

### 2.1. Các Phân hệ Chính
- **Kỳ thi & Bài kiểm tra (`/student/exams`):** Xem các đề thi được giao hoặc đề công khai.
- **Lớp học của tôi (`/student/classes`):** Tra cứu lớp học, giáo viên phụ trách và bạn cùng lớp.
- **Video bài giảng (`/student/videos`):** Xem video học liệu theo chuyên đề/lớp học.
- **Lịch sử thi (`/student/history`):** Xem lại toàn bộ các lượt thi và điểm số.

### 2.2. Quy trình Tham gia Thi Trực tuyến
1. **Chọn đề thi:** Tại trang `Kỳ thi`, bấm **Xem chi tiết** để đọc thể lệ -> Bấm **Bắt đầu làm bài**.
2. **Giao diện làm bài (Exam Player):**
   - **Đồng hồ đếm ngược:** Đếm lùi thời gian làm bài, chuyển màu đỏ khi sắp hết giờ.
   - **Danh sách câu hỏi:** Đổi màu trạng thái để biết câu nào đã trả lời, câu nào chưa làm.
   - **Đánh dấu xem lại (Bookmark):** Đánh dấu câu phân vân để kiểm tra lại trước khi nộp.
3. **Thao tác theo từng dạng câu hỏi:**
   - *Chọn 1 đáp án:* Chọn nút tròn radio.
   - *Chọn nhiều đáp án:* Tick chọn các ô vuông checkbox.
   - *Đúng / Sai:* Chọn "Đúng" hoặc "Sai".
   - *Điền từ / Số:* Nhập trực tiếp vào ô input.
   - *Tự luận:* Nhập bài làm vào khung soạn thảo văn bản.
4. **Nộp bài:**
   - Bấm **Nộp bài** -> Kiểm tra bảng tóm tắt -> Bấm **Xác nhận nộp bài**.
   - Khi hết giờ (00:00:00), hệ thống sẽ tự động khóa và nộp bài giúp học sinh.

### 2.3. Xem Kết quả & Tra cứu Lời giải
- **Điểm số:** Vòng tròn điểm %, tổng điểm đạt được, trạng thái Đạt / Chưa đạt.
- **Chính sách xem lại đề:**
  - Nếu giáo viên đang khóa: Hiển thị thông báo 🔒 *"Kết quả bài thi đã được ghi nhận. Chi tiết câu hỏi và đáp án sẽ hiển thị sau khi giáo viên mở quyền xem lại."*
  - Khi giáo viên mở quyền: Hiển thị chi tiết từng câu kèm icon trực quan: Badge xanh cho câu làm đúng, Badge đỏ cho câu làm sai và đáp án chuẩn của đề.

---

## PHẦN 3: HƯỚNG DẪN DÀNH CHO QUẢN TRỊ VIÊN & GIÁO VIÊN

### 3.1. Bảng điều khiển (Dashboard)
- Xem thống kê tổng quan: Tổng học sinh, đề thi, lượt làm bài, điểm trung bình.
- Biểu đồ phân bổ điểm số và danh sách các bài thi tự luận đang chờ chấm điểm.

### 3.2. Quản lý Người dùng & Lớp học
- **Người dùng (`/admin/users`):**
  - **Bật / Tắt tính năng tự đăng ký:** Ngay đầu trang có thẻ "Cấu hình Đăng ký tài khoản học sinh" với nút **"Mở tự đăng ký"** hoặc **"Khóa tự đăng ký"** chỉ với 1 click chuột tức thì mà không cần khởi động lại máy chủ.
  - Thêm mới tài khoản học sinh, khóa/mở khóa tài khoản, reset mật khẩu nhanh chóng.
- **Lớp học (`/admin/classes`):** Tạo lớp học, thêm học sinh vào lớp để phân quyền giao bài thi và video.

### 3.3. Quản lý Ngân hàng Câu hỏi
- **Chuyên đề (`/admin/categories`):** Phân chia môn học / chủ đề.
- **Tạo câu hỏi (`/admin/questions/create`):**
  - Hỗ trợ Markdown, khối mã nguồn (code snippet).
  - Nút **Chèn ảnh** từ máy tính (tự động nén và bảo mật theo chuẩn SHA-256).
  - Tùy chọn **Chấm từng phần** (Partial Scoring) cho câu chọn nhiều.
- **Nhập từ Excel (Import Excel):**
  - Tải file mẫu -> Nhập dữ liệu câu hỏi -> Chạy kiểm tra trước (Dry Run) -> Bấm Nhập hàng loạt.

### 3.4. Quản lý Kho Video Bài giảng
- Truy cập `/admin/videos` -> Bấm **Thêm video**.
- Dán đường dẫn YouTube -> Hệ thống tự bóc tách Video ID và lấy ảnh bìa HD tự động.
- Chọn Chuyên đề và Lớp học áp dụng -> Bấm **Lưu**.

### 3.5. Quản lý Đề thi & Xuất bản
1. **Tạo đề thi mới (`/admin/exams`):** Đặt thời lượng, điểm đạt, số lượt thi, chế độ Public hoặc Assigned, tùy chọn xáo câu hỏi và xáo đáp án.
2. **Trình tạo phiên bản (Version Builder):** Chọn câu hỏi từ ngân hàng câu hỏi, thiết lập điểm số từng câu -> Bấm **Xuất bản đề thi** (Publish).
3. **Mở / Khóa quyền xem lại chỉ với 1 click:**
   - Tại trang chi tiết đề thi, bấm **"Mở cho học sinh xem chi tiết"** hoặc **"Khóa xem lại chi tiết"** tức thì.

### 3.6. Giám sát Phòng thi, Chấm điểm & Báo cáo
- **Giám sát thời gian thực:** Xem danh sách thí sinh đang làm bài, gia hạn thêm thời gian cho thí sinh gặp sự cố, hoặc buộc nộp bài thí sinh vi phạm quy chế.
- **Chấm bài tự luận (Manual Grading):** Xem bài làm tự luận của học sinh, nhập điểm và ghi nhận xét của giáo viên.
- **Sửa đáp án & Chấm lại tự động (Regrade):** Nếu đề thi phát hiện câu hỏi bị lỗi đáp án, cho phép sửa đáp án hoặc hủy câu (cho điểm tối đa) và quét chấm lại toàn bộ lượt thi chỉ trong vài giây.
- **Xuất kết quả Excel:** Bấm **Xuất Excel** để tải báo cáo điểm số chi tiết phục vụ lưu trữ và đánh giá.

---

## PHẦN 4: CƠ CHẾ BẢO MẬT & TÍNH TOÀN VẸN
1. **Server-side Validation:** Điểm số, thời gian, đáp án đúng chỉ được lưu và xử lý tại máy chủ. Trình duyệt học sinh hoàn toàn không chứa đáp án trong suốt quá trình làm bài.
2. **Snapshot bất biến:** Khi đề thi được xuất bản, toàn bộ câu hỏi được đóng băng thành bản snapshot. Mọi thay đổi ở ngân hàng câu hỏi sau này không làm sai lệch đề thi đã xuất bản.
3. **Audit Log:** Ghi vết vĩnh viễn mọi hành động của quản trị viên (sửa đề, chấm lại, đổi quyền xem điểm...) để phục vụ thanh tra.

---

## PHẦN 5: CÂU HỎI THƯỜNG GẶP (FAQ)
- **Hỏi: Thí sinh mất mạng hoặc tắt máy đột ngột khi đang thi thì sao?**  
  *Đáp:* Mở máy hoặc đổi sang máy khác, đăng nhập lại và bấm làm bài tiếp. Hệ thống đã tự động lưu toàn bộ các câu trả lời trước đó.
- **Hỏi: Đề thi đã hết số lượt làm nhưng giáo viên phát hành đề mới?**  
  *Đáp:* Hệ thống hỗ trợ đề thi nhiều phiên bản. Khi giáo viên xuất bản phiên bản mới, lượt làm bài của học sinh sẽ được tính mới cho phiên bản đó.
- **Hỏi: Video YouTube báo lỗi không phát được?**  
  *Đáp:* Đảm bảo video trên YouTube được cài đặt quyền "Công khai" (Public) hoặc "Không công khai" (Unlisted).

---

## PHẦN 6: PHỤ LỤC KHỞI ĐỘNG HỆ THỐNG (KỸ THUẬT)
- **Khởi động Backend:**
  ```bash
  cd backend
  dotnet ef database update -p src/ELearning.Infrastructure -s src/ELearning.Api
  dotnet run --project src/ELearning.Api --launch-profile http
  ```
- **Khởi động Frontend:**
  ```bash
  cd frontend/elearning-web
  npm install
  npm run dev
  ```
- **Hoặc khởi động toàn bộ bằng Docker Compose:**
  ```bash
  docker compose up -d --build
  ```
