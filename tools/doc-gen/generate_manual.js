const fs = require('fs');
const path = require('path');
const {
  Document,
  Packer,
  Paragraph,
  TextRun,
  Table,
  TableRow,
  TableCell,
  HeadingLevel,
  AlignmentType,
  BorderStyle,
  WidthType,
  ShadingType,
  PageBreak,
  Header,
  Footer,
  PageNumber,
  NumberFormat
} = require('docx');

// Palette màu thương hiệu chuyên nghiệp (Navy Blue & Slate)
const PRIMARY = "1E3A8A";    // Deep Navy Blue (Tiêu đề chính)
const SECONDARY = "2563EB";  // Royal Blue (Tiêu đề mục con)
const ACCENT = "0D9488";     // Teal (Điểm nhấn)
const TEXT_DARK = "1E293B";  // Slate Dark (Văn bản chính)
const MUTED = "64748B";      // Gray Slate (Ghi chú, header/footer)
const BG_HEADER = "1E3A8A";  // Nền header bảng
const BG_ZEBRA = "F8FAFC";   // Nền dòng chẵn bảng
const BG_CALLOUT = "EFF6FF"; // Nền thông báo xanh
const BG_WARN = "FEF3C7";    // Nền cảnh báo vàng
const BG_TIP = "F0FDF4";     // Nền mẹo hữu ích xanh lá
const BORDER_COLOR = "CBD5E1";

// Helper định dạng tiêu đề và đoạn văn
function titleHeading(text) {
  return new Paragraph({
    spacing: { before: 260, after: 120 },
    children: [
      new TextRun({
        text,
        bold: true,
        size: 32, // 16pt
        color: PRIMARY,
        font: "Calibri"
      })
    ]
  });
}

function subHeading(text) {
  return new Paragraph({
    spacing: { before: 200, after: 90 },
    children: [
      new TextRun({
        text,
        bold: true,
        size: 26, // 13pt
        color: SECONDARY,
        font: "Calibri"
      })
    ]
  });
}

function subSubHeading(text) {
  return new Paragraph({
    spacing: { before: 150, after: 70 },
    children: [
      new TextRun({
        text,
        bold: true,
        size: 23, // 11.5pt
        color: "334155",
        font: "Calibri"
      })
    ]
  });
}

function para(text, bold = false) {
  return new Paragraph({
    spacing: { before: 60, after: 60, line: 280 },
    children: [
      new TextRun({
        text,
        bold,
        size: 22, // 11pt
        color: TEXT_DARK,
        font: "Calibri"
      })
    ]
  });
}

function bullet(text, boldPrefix = "", level = 0) {
  const children = [];
  if (boldPrefix) {
    children.push(new TextRun({
      text: boldPrefix + " ",
      bold: true,
      size: 22,
      color: TEXT_DARK,
      font: "Calibri"
    }));
  }
  children.push(new TextRun({
    text,
    size: 22,
    color: TEXT_DARK,
    font: "Calibri"
  }));

  return new Paragraph({
    bullet: { level },
    spacing: { before: 40, after: 40, line: 260 },
    children
  });
}

function callout(title, content, type = "info") {
  let bg = BG_CALLOUT;
  let borderColor = SECONDARY;
  let icon = "ℹ️ ";
  let titleColor = SECONDARY;

  if (type === "warn" || type === true) {
    bg = BG_WARN;
    borderColor = "D97706";
    icon = "⚠️ ";
    titleColor = "B45309";
  } else if (type === "tip" || type === "success") {
    bg = BG_TIP;
    borderColor = "16A34A";
    icon = "💡 ";
    titleColor = "15803D";
  } else if (type === "note") {
    bg = "F8FAFC";
    borderColor = "64748B";
    icon = "📌 ";
    titleColor = "334155";
  }

  const cell = new TableCell({
    shading: { fill: bg, type: ShadingType.CLEAR },
    margins: { top: 120, bottom: 120, left: 200, right: 200 },
    borders: {
      top: { style: BorderStyle.NONE },
      bottom: { style: BorderStyle.NONE },
      right: { style: BorderStyle.NONE },
      left: { style: BorderStyle.SINGLE, size: 24, color: borderColor }
    },
    children: [
      new Paragraph({
        spacing: { before: 0, after: 40 },
        children: [
          new TextRun({
            text: icon + title,
            bold: true,
            size: 22,
            color: titleColor,
            font: "Calibri"
          })
        ]
      }),
      new Paragraph({
        spacing: { before: 0, after: 0, line: 260 },
        children: [
          new TextRun({
            text: content,
            size: 21,
            color: TEXT_DARK,
            font: "Calibri"
          })
        ]
      })
    ]
  });

  return new Table({
    width: { size: 100, type: WidthType.PERCENTAGE },
    rows: [
      new TableRow({
        children: [cell]
      })
    ],
    margins: { top: 100, bottom: 100 }
  });
}

function createTable(headers, rowsData, colWidths = null) {
  const headerCells = headers.map((h, i) => new TableCell({
    width: colWidths ? { size: colWidths[i], type: WidthType.PERCENTAGE } : undefined,
    shading: { fill: BG_HEADER, type: ShadingType.CLEAR },
    margins: { top: 100, bottom: 100, left: 120, right: 120 },
    borders: {
      top: { style: BorderStyle.SINGLE, size: 4, color: PRIMARY },
      bottom: { style: BorderStyle.SINGLE, size: 8, color: PRIMARY },
      left: { style: BorderStyle.SINGLE, size: 4, color: BORDER_COLOR },
      right: { style: BorderStyle.SINGLE, size: 4, color: BORDER_COLOR },
    },
    children: [
      new Paragraph({
        alignment: AlignmentType.CENTER,
        children: [
          new TextRun({
            text: h,
            bold: true,
            color: "FFFFFF",
            size: 21,
            font: "Calibri"
          })
        ]
      })
    ]
  }));

  const tableRows = [
    new TableRow({
      tableHeader: true,
      children: headerCells
    })
  ];

  rowsData.forEach((row, idx) => {
    const isZebra = idx % 2 === 1;
    const cells = row.map((cellText, cellIdx) => new TableCell({
      width: colWidths ? { size: colWidths[cellIdx], type: WidthType.PERCENTAGE } : undefined,
      shading: isZebra ? { fill: BG_ZEBRA, type: ShadingType.CLEAR } : undefined,
      margins: { top: 80, bottom: 80, left: 120, right: 120 },
      borders: {
        top: { style: BorderStyle.SINGLE, size: 2, color: BORDER_COLOR },
        bottom: { style: BorderStyle.SINGLE, size: 2, color: BORDER_COLOR },
        left: { style: BorderStyle.SINGLE, size: 2, color: BORDER_COLOR },
        right: { style: BorderStyle.SINGLE, size: 2, color: BORDER_COLOR },
      },
      children: [
        new Paragraph({
          alignment: cellIdx === 0 && row.length > 2 ? AlignmentType.CENTER : AlignmentType.LEFT,
          children: [
            new TextRun({
              text: cellText,
              size: 20,
              color: TEXT_DARK,
              font: "Calibri"
            })
          ]
        })
      ]
    }));

    tableRows.push(new TableRow({ children: cells }));
  });

  return new Table({
    width: { size: 100, type: WidthType.PERCENTAGE },
    rows: tableRows
  });
}

async function generate() {
  const doc = new Document({
    styles: {
      default: {
        document: {
          run: {
            font: "Calibri",
            size: 22,
            color: TEXT_DARK
          }
        }
      }
    },
    sections: [
      // ==========================================
      // TRANG BÌA
      // ==========================================
      {
        properties: {},
        children: [
          new Paragraph({ spacing: { before: 1000, after: 0 } }),
          new Paragraph({
            alignment: AlignmentType.CENTER,
            spacing: { before: 180, after: 180 },
            children: [
              new TextRun({
                text: "HỆ THỐNG THI TRỰC TUYẾN & ĐÀO TẠO SỐ",
                bold: true,
                size: 26,
                color: MUTED,
                font: "Calibri"
              })
            ]
          }),
          new Paragraph({
            alignment: AlignmentType.CENTER,
            spacing: { before: 100, after: 260 },
            children: [
              new TextRun({
                text: "ELEARNING SYSTEM",
                bold: true,
                size: 54, // 27pt
                color: PRIMARY,
                font: "Calibri"
              })
            ]
          }),
          new Paragraph({
            alignment: AlignmentType.CENTER,
            spacing: { before: 100, after: 500 },
            children: [
              new TextRun({
                text: "TÀI LIỆU HƯỚNG DẪN SỬ DỤNG PHẦN MỀM\n(USER MANUAL & FEATURE GUIDE)",
                bold: true,
                size: 32, // 16pt
                color: SECONDARY,
                font: "Calibri"
              })
            ]
          }),
          new Paragraph({
            alignment: AlignmentType.CENTER,
            spacing: { before: 100, after: 1000 },
            children: [
              new TextRun({
                text: "Cẩm nang hướng dẫn sử dụng toàn diện tất cả tính năng dành cho Học viên, Giáo viên và Quản trị viên",
                italics: true,
                size: 24,
                color: TEXT_DARK,
                font: "Calibri"
              })
            ]
          }),

          // Box thông tin phiên bản
          new Table({
            width: { size: 75, type: WidthType.PERCENTAGE },
            alignment: AlignmentType.CENTER,
            rows: [
              new TableRow({
                children: [
                  new TableCell({
                    shading: { fill: "F1F5F9", type: ShadingType.CLEAR },
                    margins: { top: 180, bottom: 180, left: 240, right: 240 },
                    borders: {
                      top: { style: BorderStyle.SINGLE, size: 6, color: PRIMARY },
                      bottom: { style: BorderStyle.SINGLE, size: 6, color: PRIMARY },
                      left: { style: BorderStyle.SINGLE, size: 6, color: PRIMARY },
                      right: { style: BorderStyle.SINGLE, size: 6, color: PRIMARY }
                    },
                    children: [
                      new Paragraph({
                        spacing: { before: 40, after: 40 },
                        children: [
                          new TextRun({ text: "• Phiên bản phát hành: ", bold: true, size: 21, color: PRIMARY }),
                          new TextRun({ text: "Phiên bản 2.0 (PostgreSQL 17 + .NET 10 + React 19)", size: 21 })
                        ]
                      }),
                      new Paragraph({
                        spacing: { before: 40, after: 40 },
                        children: [
                          new TextRun({ text: "• Nền tảng công nghệ: ", bold: true, size: 21, color: PRIMARY }),
                          new TextRun({ text: "Clean Architecture Web API, Docker Compose, TypeScript", size: 21 })
                        ]
                      }),
                      new Paragraph({
                        spacing: { before: 40, after: 40 },
                        children: [
                          new TextRun({ text: "• Đối tượng áp dụng: ", bold: true, size: 21, color: PRIMARY }),
                          new TextRun({ text: "Học sinh/Sinh viên, Giáo viên bộ môn, Quản trị viên hệ thống", size: 21 })
                        ]
                      }),
                      new Paragraph({
                        spacing: { before: 40, after: 40 },
                        children: [
                          new TextRun({ text: "• Ngày ban hành: ", bold: true, size: 21, color: PRIMARY }),
                          new TextRun({ text: "Tháng 10 / 2026", size: 21 })
                        ]
                      }),
                      new Paragraph({
                        spacing: { before: 40, after: 40 },
                        children: [
                          new TextRun({ text: "• Đơn vị phát triển: ", bold: true, size: 21, color: PRIMARY }),
                          new TextRun({ text: "Đội ngũ Kỹ thuật & Quản trị Hệ thống ELearning", size: 21 })
                        ]
                      })
                    ]
                  })
                ]
              })
            ]
          }),

          new Paragraph({
            children: [new PageBreak()]
          })
        ]
      },

      // ==========================================
      // NỘI DUNG CHÍNH (CÓ HEADER & FOOTER)
      // ==========================================
      {
        properties: {},
        headers: {
          default: new Header({
            children: [
              new Paragraph({
                alignment: AlignmentType.RIGHT,
                spacing: { after: 100 },
                children: [
                  new TextRun({
                    text: "Tài liệu Hướng dẫn Sử dụng Hệ thống ELearning | Phiên bản 2.0",
                    size: 18,
                    color: MUTED,
                    italics: true
                  })
                ]
              })
            ]
          })
        },
        footers: {
          default: new Footer({
            children: [
              new Paragraph({
                alignment: AlignmentType.CENTER,
                spacing: { before: 100 },
                children: [
                  new TextRun({ text: "Trang ", size: 18, color: MUTED }),
                  new TextRun({
                    children: [PageNumber.CURRENT],
                    size: 18,
                    color: MUTED,
                    bold: true
                  }),
                  new TextRun({ text: " / ", size: 18, color: MUTED }),
                  new TextRun({
                    children: [PageNumber.TOTAL_PAGES],
                    size: 18,
                    color: MUTED
                  })
                ]
              })
            ]
          })
        },
        children: [
          // ------------------------------------------
          // MỤC LỤC TÀI LIỆU
          // ------------------------------------------
          titleHeading("MỤC LỤC TÀI LIỆU HƯỚNG DẪN"),
          para("Tài liệu hướng dẫn sử dụng toàn diện phần mềm ELearning bao gồm các phần chính sau:"),
          bullet("Phần 1: Giới thiệu Tổng quan, Đăng ký & Đăng nhập Hệ thống", "1."),
          bullet("Phần 2: Cẩm nang Sử dụng Toàn diện dành cho Học viên (Student Manual)", "2."),
          bullet("Phần 3: Cẩm nang Quản trị & Nghiệp vụ dành cho Giáo viên & Admin (Admin Manual)", "3."),
          bullet("Phần 4: Cơ chế Bảo mật, Chống gian lận & Đảm bảo Tính Toàn vẹn Dữ liệu", "4."),
          bullet("Phần 5: Bảng Tổng hợp Tính năng & Phím tắt Thao tác Nhanh", "5."),
          bullet("Phần 6: Các Câu hỏi Thường gặp (FAQ) & Hướng dẫn Xử lý Sự cố", "6."),
          bullet("Phần 7: Phụ lục Hướng dẫn Cài đặt & Khởi chạy Nhanh (Dành cho Kỹ thuật viên)", "7."),
          new Paragraph({ spacing: { before: 120, after: 120 } }),

          // ------------------------------------------
          // PHẦN 1
          // ------------------------------------------
          titleHeading("PHẦN 1: GIỚI THIỆU TỔNG QUAN & ĐĂNG NHẬP HỆ THỐNG"),
          
          subHeading("1.1. Giới thiệu Nền tảng ELearning"),
          para("ELearning là giải pháp thi trực tuyến và đào tạo số chuyên nghiệp, được xây dựng theo chuẩn kiến trúc Clean Architecture hiện đại: Backend .NET Core API hiệu năng cao kết hợp cơ sở dữ liệu quan hệ PostgreSQL 17 và giao diện người dùng React / TypeScript / Vite mượt mà, phản hồi tức thì."),
          para("Hệ thống giải quyết toàn diện bài toán tổ chức kiểm tra, đánh giá chất lượng học tập từ quy mô lớp học tới các kỳ thi tập trung hàng nghìn thí sinh đồng thời, với các đặc tính nổi bật:"),
          bullet("Tự động lưu bài tức thời (Autosave): Từng thao tác click chọn đáp án hoặc gõ chữ được lưu ngầm về máy chủ ngay lập tức, chống mất dữ liệu khi mất mạng hoặc sập nguồn điện.", "•"),
          bullet("Chống gian lận & Giám sát làm bài: Tự động ghi nhận các sự kiện thí sinh chuyển tab, mất tiêu điểm hoặc thoát toàn màn hình.", "•"),
          bullet("Hỗ trợ đa dạng loại câu hỏi: Trắc nghiệm 1 đáp án, Trắc nghiệm nhiều lựa chọn (hỗ trợ chấm từng phần Partial Scoring), Đúng / Sai, Điền từ ngữ / Số học (hỗ trợ sai số dung sai), và Tự luận (chấm tay kèm phản hồi nhận xét).", "•"),
          bullet("Kho Video bài giảng đa nguồn: Tích hợp phát YouTube (chuẩn, Shorts, Live), Google Drive preview và Video tệp trực tiếp MP4/WebM không quảng cáo.", "•"),
          bullet("Đề thi nhiều phiên bản (Multi-version): Cho phép xuất bản phiên bản mới mà vẫn bảo lưu nguyên vẹn lịch sử kết quả của các phiên bản cũ.", "•"),
          bullet("Quản trị linh hoạt: Bật/tắt tự đăng ký học viên, mở/khóa xem chi tiết đáp án chỉ bằng 1 cú nhấp chuột.", "•"),

          subHeading("1.2. Hướng dẫn Đăng ký Tài khoản (Dành cho Học viên mới)"),
          para("Khi tính năng tự đăng ký được Quản trị viên kích hoạt:"),
          para("1. Mở trình duyệt Web, truy cập liên kết: http://localhost:5173/register (hoặc bấm 'Đăng ký ngay' tại trang Đăng nhập)."),
          para("2. Điền đầy đủ các thông tin:"),
          bullet("Tên đăng nhập (Username): Viết liền không dấu, từ 3 ký tự trở lên (Ví dụ: nguyenvanan).", "•"),
          bullet("Họ và tên đầy đủ: Nhập tên có dấu (Ví dụ: Nguyễn Văn An).", "•"),
          bullet("Địa chỉ Email: Nhập email cá nhân đang hoạt động để nhận thông báo.", "•"),
          bullet("Mật khẩu: Nhập mật khẩu tối thiểu 8 ký tự (nên gồm cả chữ và số).", "•"),
          bullet("Xác nhận mật khẩu: Nhập lại chính xác mật khẩu vừa đặt.", "•"),
          para("3. Nhấn nút 'Đăng ký tài khoản'. Hệ thống sẽ tự động tạo tài khoản và đưa bạn về màn hình Đăng nhập để bắt đầu sử dụng."),

          callout("Lưu ý khi tính năng Tự đăng ký bị khóa:",
            "Nếu Quản trị viên đang tắt tính năng tự đăng ký, trang Đăng ký sẽ xuất hiện thông báo màu vàng: 'Tính năng tự đăng ký tài khoản hiện đang tạm khóa. Vui lòng liên hệ quản trị viên hoặc giáo viên để được cấp tài khoản.' Đồng thời nút Đăng ký sẽ bị vô hiệu hóa để bảo đảm an toàn dữ liệu trường học.", "warn"),

          subHeading("1.3. Hướng dẫn Đăng nhập Hệ thống"),
          para("1. Truy cập địa chỉ hệ thống trên trình duyệt: http://localhost:5173/login."),
          para("2. Nhập 'Tên đăng nhập' và 'Mật khẩu' được cấp."),
          para("3. Bấm nút 'Đăng nhập'. Hệ thống sẽ tự động chuyển hướng theo đúng vai trò:"),
          bullet("Tài khoản Học viên (Student): Tự động chuyển tới trang Danh sách Kỳ thi (/student/exams).", "•"),
          bullet("Tài khoản Quản trị / Giáo viên (Admin): Tự động chuyển tới Bảng điều khiển Quản trị (/admin/dashboard).", "•"),

          callout("Tài khoản thử nghiệm sẵn có trên hệ thống:",
            "• Quản trị viên (Admin): admin / Mật khẩu: Admin@123456\n• Học viên mẫu (Student): student01 hoặc student02 / Mật khẩu: Student@123456", "tip"),

          subHeading("1.4. Đổi Mật khẩu & Quản lý Thông tin Cá nhân"),
          para("Để bảo vệ tài khoản, người dùng nên đổi mật khẩu ngay sau lần đăng nhập đầu tiên:"),
          para("1. Bấm vào Tên người dùng hoặc Avatar ở góc trên cùng bên phải thanh menu -> Chọn 'Đổi mật khẩu' (/change-password)."),
          para("2. Nhập Mật khẩu hiện tại, sau đó nhập Mật khẩu mới và Xác nhận lại."),
          para("3. Bấm 'Lưu thay đổi'. Hệ thống sẽ cập nhật ngay lập tức."),
          para("4. Khi cần kết thúc phiên làm việc trên máy tính công cộng, bấm menu Avatar -> Chọn 'Đăng xuất' để xóa phiên đăng nhập an toàn."),

          new Paragraph({ spacing: { before: 120, after: 120 } }),

          // ------------------------------------------
          // PHẦN 2
          // ------------------------------------------
          titleHeading("PHẦN 2: CẨM NANG SỬ DỤNG DÀNH CHO HỌC VIÊN (STUDENT GUIDE)"),
          para("Giao diện học viên được thiết kế theo phong cách hiện đại, trực quan, tối ưu trên cả máy tính để bàn (PC), laptop và máy tính bảng."),

          subHeading("2.1. Thanh Điều Hướng & Các Phân Hệ Chính"),
          para("Sau khi đăng nhập, học viên có thanh menu trên cùng với các phân hệ chức năng:"),
          bullet("Kỳ thi & Bài kiểm tra (/student/exams): Nơi tập trung toàn bộ đề thi mà bạn có quyền tham gia.", "1."),
          bullet("Lớp học của tôi (/student/classes): Tra cứu lớp học, giáo viên phụ trách và danh sách bạn cùng lớp.", "2."),
          bullet("Video bài giảng (/student/videos): Kho học liệu số với các bài giảng video phong phú theo từng chuyên đề.", "3."),
          bullet("Lịch sử làm bài (/student/history): Nhật ký lưu trữ toàn bộ các bài thi đã làm kèm điểm số và thống kê.", "4."),

          subHeading("2.2. Phân Hệ Kỳ Thi & Bài Kiểm Tra"),
          para("Tại trang '/student/exams', học viên có thể xem danh sách tất cả các bài thi khả dụng:"),
          bullet("Thẻ thông tin đề thi: Hiển thị Mã đề, Tên đề thi, Thời lượng làm bài (phút), Điểm đạt yêu cầu (%), Thời hạn làm bài (bắt đầu - kết thúc) và Số lượt thi còn lại (Ví dụ: Còn 1 lượt, hoặc Thi tự do không giới hạn).", "•"),
          bullet("Phân loại đề thi: Đề 'Công khai' (mọi học sinh đều được làm) và Đề 'Chỉ định' (chỉ dành riêng cho lớp hoặc học sinh cụ thể).", "•"),
          bullet("Trạng thái đề thi: 'SẮP DIỄN RA' (chưa đến giờ mở), 'ĐANG MỞ' (nút Bắt đầu sáng màu xanh), 'ĐÃ KẾT THÚC' (hết hạn thi).", "•"),
          bullet("Xem thể lệ & Hướng dẫn: Bấm nút 'Xem chi tiết' để đọc kỹ quy chế và yêu cầu của giáo viên trước khi vào thi.", "•"),

          subHeading("2.3. Quy Trình Làm Bài Thi Trực Tuyến Từ A Đến Z (Exam Player)"),
          para("Khi bấm 'Bắt đầu làm bài', học viên sẽ bước vào giao diện phòng thi chuyên nghiệp:"),

          subSubHeading("1. Các thành phần trên màn hình phòng thi"),
          bullet("Đồng hồ đếm ngược (Exam Timer): Luôn hiển thị ở góc trên màn hình. Đồng hồ chuyển màu vàng cam khi còn dưới 5 phút, và chuyển màu đỏ nhấp nháy khi còn dưới 1 phút.", "•"),
          bullet("Tiến độ làm bài: Hiển thị số câu đã hoàn thành (Ví dụ: 'Đã trả lời 18/40 câu').", "•"),
          bullet("Bảng danh sách câu hỏi (Question Navigation Panel): Đánh số từ câu 1 đến câu N. Mã màu trực quan:", "•"),
          para("   - Màu xám: Câu hỏi chưa trả lời."),
          para("   - Màu xanh dương: Câu hỏi đã chọn hoặc đã điền đáp án thành công."),
          para("   - Biểu tượng lá cờ vàng: Câu hỏi được bạn đánh dấu xem lại."),
          bullet("Nút 'Đánh dấu xem lại' (Bookmark): Cho phép bạn gắn cờ những câu phân vân để dễ dàng click quay lại kiểm tra trước khi nộp bài.", "•"),

          subSubHeading("2. Hướng dẫn thao tác theo từng dạng câu hỏi"),
          createTable(
            ["Dạng câu hỏi", "Thao tác của Học sinh", "Đặc điểm tính điểm"],
            [
              ["Trắc nghiệm chọn 1 (Single Choice)", "Click vào nút tròn (Radio) của 1 phương án duy nhất (A, B, C hoặc D). Muốn đổi đáp án chỉ cần click phương án khác.", "Đúng nhận trọn điểm, sai nhận 0 điểm."],
              ["Trắc nghiệm nhiều đáp án (Multiple Choice)", "Click vào ô vuông (Checkbox) của các phương án đúng. Câu hỏi có thể có 2, 3 hoặc nhiều đáp án đúng.", "Hệ thống hỗ trợ Chấm từng phần: Chọn đúng một phần vẫn được cộng điểm tương ứng."],
              ["Đúng / Sai (True / False)", "Click chọn nút 'Đúng' hoặc nút 'Sai' theo nhận định của đề bài.", "Chấm điểm chuẩn xác theo đáp án."],
              ["Điền từ ngữ / Số học (Fill In)", "Nhập trực tiếp câu trả lời vào ô trống. Với câu hỏi số học, có thể nhập số nguyên hoặc thập phân (hỗ trợ dấu chấm hoặc dấu phẩy).", "Hỗ trợ dung sai sai số (Tolerance) và không phân biệt chữ hoa/thường."],
              ["Tự luận (Essay)", "Nhập bài làm vào khung soạn thảo văn bản. Có thể gõ nhiều đoạn văn, trình bày chi tiết các bước giải.", "Bài thi sẽ được giáo viên chấm tay và ghi nhận xét sau khi nộp."]
            ],
            [25, 45, 30]
          ),

          callout("Cơ chế Lưu bài tự động (Autosave):",
            "Mỗi khi bạn chọn đáp án hoặc gõ một ký tự, hệ thống sẽ gửi tín hiệu lưu ngầm về máy chủ trong vài mili-giây. Bạn không cần lo lắng về sự cố mất mạng, tắt máy hay sập nguồn. Khi đăng nhập lại trên bất kỳ thiết bị nào, toàn bộ câu trả lời đã làm sẽ được khôi phục nguyên vẹn 100%!", "tip"),

          subSubHeading("3. Cảnh báo chống gian lận trong khi làm bài"),
          para("Trong suốt thời gian thi, hệ thống sẽ tự động giám sát các hành vi bất thường:"),
          bullet("Phát hiện chuyển tab hoặc thu nhỏ trình duyệt: Khi bạn rời khỏi màn hình làm bài sang tab khác để tra cứu, hệ thống sẽ lập tức hiện hộp thoại cảnh báo và ghi nhận sự kiện vào nhật ký thi.", "•"),
          bullet("Vi phạm nhiều lần: Quản trị viên trong phòng giám sát có thể áp dụng hình thức cảnh cáo hoặc buộc nộp bài ngay lập tức.", "•"),

          subSubHeading("4. Nộp bài thi"),
          bullet("Nộp bài chủ động: Sau khi làm xong và kiểm tra lại danh sách câu hỏi, bấm nút 'Nộp bài' ở góc trên bên phải. Hệ thống sẽ mở bảng tóm tắt: Đã làm bao nhiêu câu, còn sót câu nào chưa làm, các câu đã bookmark. Bấm 'Xác nhận nộp bài' để hoàn tất.", "•"),
          bullet("Tự động nộp bài khi hết giờ: Nếu đồng hồ đếm ngược chạm mốc 00:00:00, bài thi sẽ tự động khóa và gửi toàn bộ các câu trả lời đã lưu lên máy chủ. Bạn không cần thao tác gì thêm và không bao giờ bị mất bài làm.", "•"),

          subHeading("2.4. Tra Cứu Kết Quả & Xem Lời Giải Chi Tiết"),
          para("Sau khi nộp bài, màn hình kết quả sẽ lập tức hiển thị:"),
          bullet("Biểu đồ điểm số: Điểm đạt được trên thang điểm tối đa (Ví dụ: 8.5 / 10.0) và tỉ lệ phần trăm.", "•"),
          bullet("Đánh giá kết quả: Hiển thị huy hiệu 'ĐẠT' (Màu xanh lá) nếu đạt trên điểm chuẩn quy định, hoặc 'CHƯA ĐẠT' (Màu đỏ) nếu dưới điểm chuẩn.", "•"),
          bullet("Thống kê chi tiết: Thời gian làm bài thực tế, số câu trả lời đúng, số câu trả lời sai.", "•"),
          bullet("Chính sách xem lại đề thi (Review Policy):", "•"),
          para("   - Khi Giáo viên đang khóa xem lại: Hiển thị icon ổ khóa 🔒 kèm thông báo 'Kết quả bài thi đã được ghi nhận. Chi tiết câu hỏi và đáp án sẽ hiển thị sau khi giáo viên / quản trị viên mở quyền xem lại.'"),
          para("   - Khi Giáo viên mở quyền xem lại: Bạn sẽ xem được toàn bộ danh sách câu hỏi trong đề, với: Phương án bạn đã chọn (Badge xanh nếu đúng, Badge đỏ nếu sai), Đáp án chuẩn xác của đề thi và Lời giải thích chi tiết của giáo viên."),

          subHeading("2.5. Kho Video Bài Giảng Đa Nguồn (/student/videos)"),
          para("Hệ thống trang bị kho video bài giảng hiện đại, hỗ trợ nhiều nguồn học liệu phong phú:"),
          bullet("YouTube tiêu chuẩn & YouTube Shorts: Hỗ trợ cả video bài giảng thông thường và các video ngắn (Shorts) với định dạng youtube.com/shorts/... chạy cực kỳ mượt mà.", "•"),
          bullet("YouTube Live stream: Xem các buổi livestream giảng bài trực tiếp.", "•"),
          bullet("Google Drive preview: Xem video lưu trữ trên Google Drive trường học mà không cần cấp quyền tải về máy.", "•"),
          bullet("Video trực tiếp (MP4 / WebM): Trình phát HTML5 native chất lượng cao, có đầy đủ công cụ tua thời gian, đổi tốc độ phát (0.75x, 1x, 1.25x, 1.5x) và xem toàn màn hình.", "•"),
          bullet("Lọc theo Chuyên đề & Lớp học: Dễ dàng tìm nhanh bài giảng theo môn Toán, Lý, Hóa, Ngoại ngữ hoặc chuyên đề riêng của lớp mình.", "•"),

          subHeading("2.6. Lớp Học Của Tôi & Lịch Sử Làm Bài"),
          bullet("Lớp học của tôi (/student/classes): Xem danh sách các lớp bạn đã được phân công, thông tin giáo viên chủ nhiệm / bộ môn và danh sách bạn bè cùng lớp.", "•"),
          bullet("Lịch sử làm bài (/student/history): Lưu trữ danh sách đầy đủ tất cả các lượt thi bạn đã từng tham gia từ trước đến nay, ngày giờ nộp bài, điểm số và trạng thái Đạt/Chưa đạt để theo dõi tiến độ tiến bộ.", "•"),

          new Paragraph({ spacing: { before: 120, after: 120 } }),

          // ------------------------------------------
          // PHẦN 3
          // ------------------------------------------
          titleHeading("PHẦN 3: CẨM NANG NGHIỆP VỤ DÀNH CHO GIÁO VIÊN & ADMIN"),
          para("Phân hệ Quản trị (/admin) là trung tâm điều hành toàn diện của nhà trường, bao quát toàn bộ nghiệp vụ từ ngân hàng câu hỏi, soạn đề, phân lớp, giám sát thi trực tiếp đến chấm điểm và xuất báo cáo."),

          subHeading("3.1. Bảng Điều Khiển Trung Tâm (Dashboard)"),
          para("Trang Dashboard (/admin/dashboard) cung cấp góc nhìn toàn cảnh về tình hình vận hành:"),
          bullet("4 thẻ chỉ số thống kê (KPIs): Tổng số học sinh, Tổng số đề thi đang hoạt động, Tổng số lượt làm bài, Điểm trung bình toàn trường.", "•"),
          bullet("Biểu đồ phân bổ điểm số: Biểu thị tỉ lệ học sinh đạt điểm Giỏi (9-10), Khá (7-8), Trung bình (5-6) và Chưa đạt (<5).", "•"),
          bullet("Danh sách bài tự luận chờ chấm: Cảnh báo nhắc nhở giáo viên các bài thi có câu tự luận cần chấm điểm thủ công.", "•"),

          subHeading("3.2. Quản Lý Người Dùng & Điều Khiển Tự Đăng Ký (/admin/users)"),
          subSubHeading("1. Bật / Tắt tính năng tự đăng ký học viên chỉ với 1 click"),
          para("Ngay đầu trang Quản lý Người dùng có thẻ 'Cấu hình Đăng ký tài khoản học sinh':"),
          bullet("Nếu đang MỞ: Nút bấm hiển thị 'Khóa tự đăng ký' (Màu đỏ). Học sinh bên ngoài có thể tự vào đăng ký tài khoản.", "•"),
          bullet("Nếu đang KHÓA: Nút bấm hiển thị 'Mở tự đăng ký' (Màu xanh lá). Học sinh bên ngoài không thể tự đăng ký, bảo đảm an toàn dữ liệu.", "•"),
          bullet("Thao tác: Quản trị viên chỉ cần click 1 lần vào nút này. Cấu hình sẽ được lưu vào cơ sở dữ liệu và có hiệu lực tức thì mà không cần khởi động lại máy chủ.", "•"),

          subSubHeading("2. Quản lý tài khoản học sinh và giáo viên"),
          bullet("Thêm người dùng mới: Nhấn nút 'Thêm người dùng', nhập Username, Họ và tên, Email, Mật khẩu ban đầu và chọn Vai trò (Student, Teacher, Admin).", "•"),
          bullet("Khóa / Kích hoạt tài khoản: Khi cần tạm khóa một học sinh, chỉ cần gạt switch trạng thái sang 'Đã khóa'. Khi mở lại chỉ cần click lại.", "•"),
          bullet("Đặt lại mật khẩu (Reset Password): Hỗ trợ học sinh trong trường hợp quên mật khẩu chỉ bằng 1 thao tác nhanh.", "•"),

          subHeading("3.3. Quản Lý Lớp Học & Phân Phối Học Viên (/admin/classes)"),
          para("1. Tạo lớp học mới: Nhập Tên lớp (Ví dụ: 'Lớp 12A1'), Mã lớp ('12A1-2026'), Niên khóa, Ngày bắt đầu và Ngày kết thúc."),
          para("2. Thêm học viên vào lớp: Tìm kiếm học sinh theo tên hoặc mã và thêm vào lớp học."),
          para("3. Tác dụng của Lớp học: Dùng để phân quyền giao bài thi chỉ định (chỉ học sinh trong lớp mới được thi) và chia sẻ video bài giảng cho riêng lớp đó."),

          subHeading("3.4. Quản Lý Ngân Hàng Câu Hỏi (Question Bank)"),
          subSubHeading("1. Phân loại Chuyên đề môn học (/admin/categories)"),
          para("Tạo các chuyên đề để nhóm câu hỏi theo chủ đề (Ví dụ: 'Toán - Giải tích', 'Lập trình C#', 'Tiếng Anh - Ngữ pháp'...)."),

          subSubHeading("2. Tạo câu hỏi mới (/admin/questions/create)"),
          para("Trình soạn thảo câu hỏi hỗ trợ các tính năng cao cấp:"),
          bullet("Soạn thảo Markdown & Code block: Hỗ trợ in đậm, in nghiêng, danh sách, công thức và khối mã nguồn có syntax highlighting.", "•"),
          bullet("Nút 'Chèn ảnh' trực quan: Bạn có thể chọn file ảnh từ máy tính (hỗ trợ JPG, PNG, GIF, WebP). Hệ thống sẽ tự động tối ưu hóa, lưu trữ an toàn theo mã băm SHA-256 và chèn thẻ ảnh Markdown vào đúng vị trí con trỏ.", "•"),
          bullet("Cấu hình Chấm từng phần (Partial Scoring): Đối với câu trắc nghiệm nhiều đáp án, bật tính năng này để học sinh chọn đúng 2/3 đáp án vẫn được 66.7% số điểm của câu.", "•"),
          bullet("Giải thích chi tiết: Soạn lời giải thích để học sinh đọc hiểu sau khi được mở quyền xem lại bài thi.", "•"),

          subSubHeading("3. Nhập câu hỏi hàng loạt từ Excel (Import Excel)"),
          para("Để tiết kiệm thời gian khi đưa hàng trăm câu hỏi vào hệ thống:"),
          para("1. Tại trang Ngân hàng câu hỏi, nhấn nút 'Tải file mẫu Excel' (Template_Import_CauHoi.xlsx)."),
          para("2. Điền dữ liệu vào các cột quy định: Mã câu hỏi, Nội dung, Mã chuyên đề, Dạng câu, Các phương án A, B, C, D, Đáp án đúng, Điểm mặc định, Độ khó, Lời giải."),
          para("3. Nhấn nút 'Nhập từ Excel' -> Chọn file vừa điền -> Bấm 'Kiểm tra trước' (Dry Run)."),
          para("4. Hệ thống sẽ quét toàn bộ dữ liệu. Nếu có dòng nào thiếu thông tin hoặc sai mã chuyên đề, hệ thống sẽ báo chính xác số dòng lỗi để bạn sửa. Khi kết quả báo 'Hợp lệ 100%', nhấn 'Thực hiện nhập' để hoàn tất."),

          subHeading("3.5. Xây Dựng, Xuất Bản & Quản Trị Đề Thi (/admin/exams)"),
          subSubHeading("1. Tạo đề thi mới"),
          para("Bấm nút 'Tạo đề thi' và thiết lập các thông số:"),
          bullet("Tên đề thi & Mã đề: Ví dụ 'Kiểm tra Giữa kỳ 1 - Lập trình C#' (Mã: 'CS-MID-01').", "•"),
          bullet("Thời lượng: Số phút làm bài (Ví dụ: 45 phút, 60 phút, 90 phút).", "•"),
          bullet("Điểm đạt yêu cầu: Tỉ lệ phần trăm để xếp loại Đạt (Ví dụ: 50% hoặc 60%).", "•"),
          bullet("Khung giờ thi: Đặt ngày giờ Bắt đầu và Kết thúc mở đề.", "•"),
          bullet("Số lượt thi tối đa: Đặt 1 lượt (cho kỳ thi chính thức) hoặc nhiều lượt (cho đề ôn tập).", "•"),
          bullet("Chế độ truy cập (Access Mode):", "•"),
          para("   - 'PUBLIC': Mọi học viên đều nhìn thấy và được làm bài."),
          para("   - 'ASSIGNED': Chỉ những Lớp học hoặc Học viên được phân công mới có quyền vào thi."),
          bullet("Bảo mật đề: Tick chọn 'Xáo trộn thứ tự câu hỏi' và 'Xáo trộn thứ tự phương án' để mỗi học sinh có một mã đề hiển thị khác nhau, hạn chế nhìn bài nhau.", "•"),

          subSubHeading("2. Trình dựng đề thi (Version Builder)"),
          para("1. Nhấn nút 'Thêm câu hỏi từ ngân hàng': Lọc theo chuyên đề, độ khó và tick chọn câu hỏi đưa vào đề."),
          para("2. Phân bổ điểm số: Có thể điều chỉnh điểm số từng câu (Ví dụ câu dễ 0.25đ, câu khó 1.0đ, tự luận 2.0đ)."),
          para("3. Bấm 'Xuất bản đề thi' (Publish):"),
          callout("Cơ chế Đóng băng Snapshot Đề thi:",
            "Khi bạn bấm 'Xuất bản', hệ thống sẽ tạo một bản snapshot độc lập lưu trữ toàn bộ nội dung câu hỏi và đáp án của đề thi tại thời điểm đó. Dù sau này bạn có chỉnh sửa hoặc xóa câu hỏi trong Ngân hàng câu hỏi, đề thi đã xuất bản vẫn được bảo toàn nguyên vẹn 100%, không bao giờ bị sai lệch kết quả!", "tip"),

          subSubHeading("3. Bật / Tắt Quyền xem kết quả chỉ với 1 Click"),
          para("Tại trang chi tiết đề thi (/admin/exams/:id), quản trị viên có thẻ điều khiển 'Quyền xem lại kết quả bài thi':"),
          bullet("Nút 'Mở cho học sinh xem chi tiết': Khi ca thi kết thúc, bấm nút này để toàn bộ thí sinh được xem chi tiết bài làm, đáp án đúng và lời giải thích.", "•"),
          bullet("Nút 'Khóa xem lại chi tiết': Khóa quyền xem lại tức thì nếu cần bảo mật đề cho các ca thi tiếp theo.", "•"),

          subHeading("3.6. Giám Sát Phòng Thi, Chấm Tự Luận & Xuất Báo Cáo"),
          subSubHeading("1. Giám sát phòng thi thời gian thực (/admin/exams/:id/results)"),
          bullet("Theo dõi thí sinh: Xem danh sách học sinh đang trong trạng thái 'ĐANG LÀM' (IN_PROGRESS), thời gian còn lại và tiến độ trả lời.", "•"),
          bullet("Gia hạn thời gian (Add time): Nếu một học sinh gặp sự cố máy móc hoặc mạng, giáo viên có thể cộng thêm số phút làm bài cho riêng học sinh đó ngay trên màn hình giám sát.", "•"),
          bullet("Buộc nộp bài (Force submit): Buộc kết thúc lượt thi của thí sinh cố tình vi phạm quy chế.", "•"),

          subSubHeading("2. Chấm bài tự luận (Manual Grading)"),
          para("1. Vào tab 'Chấm tự luận' trong kết quả đề thi."),
          para("2. Chọn bài làm của học sinh: Màn hình hiển thị nội dung câu hỏi tự luận, đáp án gợi ý của giáo viên và bài làm thực tế của học sinh."),
          para("3. Nhập số điểm chấm (từ 0 đến điểm tối đa của câu) và nhập 'Nhận xét của giáo viên'."),
          para("4. Bấm 'Lưu điểm chấm'. Hệ thống sẽ tự động cập nhật lại tổng điểm và xếp loại cho học sinh."),

          subSubHeading("3. Sửa đáp án & Chấm lại tự động (Regrade)"),
          para("Trong trường hợp hy hữu đề thi có một câu hỏi bị nhầm đáp án sau khi học sinh đã nộp bài:"),
          para("1. Vào chi tiết đề thi -> Bấm 'Sửa đáp án / Chấm lại'."),
          para("2. Chọn câu hỏi cần sửa: Cập nhật lại đáp án đúng chuẩn hoặc chọn 'Hủy câu' (mọi học sinh đều được trọn điểm câu này)."),
          para("3. Nhập lý do thay đổi và bấm 'Chấm lại toàn bộ lượt thi'."),
          para("4. Hệ thống sẽ tự động quét lại toàn bộ hàng trăm bài làm của học sinh và tính lại điểm số chuẩn xác chỉ trong vài giây, đồng thời ghi lại nhật ký kiểm toán (Audit Log)."),

          subSubHeading("4. Xuất Báo cáo Kết quả ra file Excel"),
          para("Nhấn nút 'Xuất Excel' tại trang kết quả đề thi. Hệ thống sẽ tạo file Excel hoàn chỉnh gồm: Mã học viên, Họ và tên, Lớp học, Điểm thi, Tỉ lệ phần trăm, Số câu đúng/sai, Thời gian bắt đầu, Thời gian nộp bài và Xếp loại (Đạt/Chưa đạt)."),

          subHeading("3.7. Quản Lý Kho Video Bài Giảng Đa Nguồn (/admin/videos)"),
          para("Quản trị viên có thể thêm bài giảng từ nhiều nguồn khác nhau:"),
          bullet("YouTube thông thường & YouTube Shorts: Dán link YouTube (dạng watch?v=... hoặc shorts/...). Hệ thống sẽ tự động bóc tách Video ID và lấy ảnh bìa đại diện (Thumbnail HD).", "•"),
          bullet("Google Drive video: Dán liên kết chia sẻ Google Drive (chú ý đặt quyền 'Bất kỳ ai có đường liên kết đều có thể xem').", "•"),
          bullet("File video trực tiếp (MP4 / WebM): Dán URL trực tiếp tới file video.", "•"),
          bullet("Phân quyền: Gán Chuyên đề môn học và chọn Lớp học được phép xem video.", "•"),

          new Paragraph({ spacing: { before: 120, after: 120 } }),

          // ------------------------------------------
          // PHẦN 4
          // ------------------------------------------
          titleHeading("PHẦN 4: CƠ CHẾ BẢO MẬT & ĐẢM BẢO TÍNH TOÀN VẸN"),
          subHeading("4.1. Nguyên Tắc 'Máy Chủ Là Nguồn Dữ Liệu Chuẩn' (Server-side Validation)"),
          bullet("Toàn bộ việc tính giờ, kiểm soát thời hạn nộp bài và chấm điểm đều thực hiện 100% tại máy chủ backend .NET 10.", "•"),
          bullet("Bảo mật đề thi: Khi học sinh nhận đề thi, dữ liệu gửi về trình duyệt chỉ bao gồm nội dung câu hỏi và các phương án lựa chọn. Đáp án đúng và lời giải thích TUYỆT ĐỐI KHÔNG tồn tại trên máy học sinh trong suốt quá trình làm bài, loại bỏ triệt để nguy cơ soi mã nguồn (Inspect Element) để gian lận.", "•"),
          bullet("Chống nộp trùng lặp (Idempotent): Dù học sinh có bấm nộp bài nhiều lần liên tiếp do mạng lag, máy chủ chỉ ghi nhận và chấm điểm đúng một lần duy nhất.", "•"),

          subHeading("4.2. Nhật Ký Giám Sát Hệ Thống (Audit Logs)"),
          para("Mọi hành động quan trọng trên hệ thống đều được lưu vết vĩnh viễn trong Nhật ký giám sát (/admin/audit-logs):"),
          bullet("Ai đã tạo, chỉnh sửa hoặc xuất bản đề thi?", "•"),
          bullet("Ai đã mở quyền hoặc khóa quyền xem lại kết quả bài thi?", "•"),
          bullet("Ai đã gia hạn thời gian, sửa đáp án hoặc chấm lại bài thi?", "•"),
          bullet("Ai đã bật/tắt tính năng đăng ký tài khoản học sinh?", "•"),

          new Paragraph({ spacing: { before: 120, after: 120 } }),

          // ------------------------------------------
          // PHẦN 5
          // ------------------------------------------
          titleHeading("PHẦN 5: BẢNG TỔNG HỢP TÍNH NĂNG & PHÍM TẮT THAO TÁC"),
          subHeading("5.1. Ma Trận Phân Quyền Tính Năng Theo Vai Trò"),
          createTable(
            ["Chức năng / Phân hệ", "Học viên (Student)", "Giáo viên (Teacher)", "Quản trị viên (Admin)"],
            [
              ["Xem danh sách đề thi & Làm bài", "Có (đề khả dụng)", "Có", "Có"],
              ["Xem kết quả & Điểm số cá nhân", "Có", "Có", "Có"],
              ["Xem chi tiết lời giải đề thi", "Khi được mở quyền", "Luôn xem được", "Luôn xem được"],
              ["Xem video bài giảng đa nguồn", "Có (theo phân quyền)", "Có", "Có"],
              ["Bật/tắt tự đăng ký học viên", "Không", "Không", "Có (1 Click)"],
              ["Soạn câu hỏi & Chèn ảnh", "Không", "Có", "Có"],
              ["Nhập câu hỏi từ Excel mẫu", "Không", "Có", "Có"],
              ["Tạo & Xuất bản đề thi", "Không", "Có", "Có"],
              ["Mở/Khóa xem đáp án chi tiết", "Không", "Có (đề của mình)", "Có (1 Click)"],
              ["Giám sát phòng thi & Thêm giờ", "Không", "Có", "Có"],
              ["Chấm bài thi tự luận", "Không", "Có", "Có"],
              ["Sửa đáp án & Chấm lại (Regrade)", "Không", "Có", "Có"],
              ["Xuất bảng điểm ra file Excel", "Không", "Có", "Có"],
              ["Quản lý người dùng & Đổi quyền", "Không", "Không", "Có"]
            ],
            [34, 22, 22, 22]
          ),

          new Paragraph({ spacing: { before: 120, after: 120 } }),

          // ------------------------------------------
          // PHẦN 6
          // ------------------------------------------
          titleHeading("PHẦN 6: CÂU HỎI THƯỜNG GẶP (FAQ) & XỬ LÝ SỰ CỐ"),
          createTable(
            ["Tình huống / Câu hỏi", "Nguyên nhân & Hướng dẫn xử lý"],
            [
              [
                "Học sinh bị mất mạng hoặc sập nguồn điện khi đang thi?",
                "Học sinh chỉ cần mở lại máy tính hoặc chuyển sang thiết bị khác, đăng nhập tài khoản và bấm vào đề thi để làm tiếp. Toàn bộ các câu trả lời trước đó đã được hệ thống lưu tự động qua cơ chế Autosave."
              ],
              [
                "Học sinh thi xong nhưng không thấy câu nào đúng, câu nào sai?",
                "Do Giáo viên đang đặt chế độ bảo mật 'Khóa xem lại chi tiết'. Điểm tổng quan của bạn đã được lưu an toàn. Khi đợt thi kết thúc và giáo viên bấm 'Mở cho học sinh xem chi tiết', bạn sẽ xem được toàn bộ lời giải."
              ],
              [
                "Trang đăng ký báo 'Tính năng tự đăng ký hiện đang tạm khóa'?",
                "Do Quản trị viên đang khóa tính năng tự do đăng ký để quản lý danh sách học sinh chặt chẽ. Hãy liên hệ với Quản trị viên hoặc Giáo viên để được cấp tài khoản chính thức."
              ],
              [
                "Đề thi bị hết số lượt làm nhưng giáo viên đã cập nhật đề mới?",
                "Hệ thống hỗ trợ Đề thi nhiều phiên bản (Multi-version). Khi giáo viên xuất bản phiên bản mới (v2), số lượt làm của học sinh sẽ được làm mới cho phiên bản đó, học sinh chỉ cần bấm 'Bắt đầu làm bài' phiên bản mới."
              ],
              [
                "Nhập file Excel câu hỏi bị báo lỗi dòng?",
                "Bấm xem chi tiết thông báo lỗi: Kiểm tra lại tên sheet là 'CauHoi', các cột không được đổi tên. Hãy đảm bảo Mã chuyên đề trùng khớp với chuyên đề đã tạo trong hệ thống và các phương án lựa chọn được điền liên tục từ A, B, C."
              ],
              [
                "Video YouTube hoặc Google Drive báo lỗi không phát được?",
                "• YouTube: Video phải để ở chế độ 'Công khai' (Public) hoặc 'Không công khai' (Unlisted). Không để chế độ 'Riêng tư' (Private).\n• Google Drive: Đường link chia sẻ phải được bật quyền 'Bất kỳ ai có đường liên kết đều có thể xem'."
              ]
            ],
            [35, 65]
          ),

          new Paragraph({ spacing: { before: 120, after: 120 } }),

          // ------------------------------------------
          // PHẦN 7
          // ------------------------------------------
          titleHeading("PHẦN 7: PHỤ LỤC HƯỚNG DẪN KHỞI CHẠY (DÀNH CHO KỸ THUẬT)"),
          subHeading("7.1. Yêu Cầu Môi Trường Kỹ Thuật"),
          bullet("Hệ điều hành: Windows 10/11, Windows Server hoặc Linux (Ubuntu 22.04+).", "•"),
          bullet("Cơ sở dữ liệu: PostgreSQL 16 hoặc 17.", "•"),
          bullet("Môi trường thực thi: .NET SDK 10 (hoặc .NET 9) và Node.js phiên bản 20+.", "•"),

          subHeading("7.2. Các Lệnh Khởi Động Nhanh"),
          subSubHeading("1. Chạy Backend API (.NET 10)"),
          para("Mở cửa sổ Terminal tại thư mục dự án:"),
          para("cd backend"),
          para("dotnet ef database update -p src/ELearning.Infrastructure -s src/ELearning.Api"),
          para("dotnet run --project src/ELearning.Api --launch-profile http"),
          para("-> Backend API chạy tại: http://localhost:5136 (Swagger UI: http://localhost:5136/swagger)"),

          subSubHeading("2. Chạy Giao Diện Web Frontend (React Vite)"),
          para("Mở cửa sổ Terminal thứ hai:"),
          para("cd frontend/elearning-web"),
          para("npm install"),
          para("npm run dev"),
          para("-> Giao diện Web chạy tại: http://localhost:5173"),

          subSubHeading("3. Hoặc chạy toàn bộ bằng Docker Compose"),
          para("docker compose up -d --build"),
          para("-> Toàn bộ ứng dụng sẵn sàng tại địa chỉ: http://localhost:8080"),

          new Paragraph({ spacing: { before: 300, after: 0 } }),
          new Paragraph({
            alignment: AlignmentType.CENTER,
            children: [
              new TextRun({
                text: "--- HẾT TÀI LIỆU HƯỚNG DẪN SỬ DỤNG ---",
                bold: true,
                size: 20,
                color: MUTED
              })
            ]
          })
        ]
      }
    ]
  });

  const outPath = path.resolve(__dirname, '../../HUONG_DAN_SU_DUNG_ELEARNING.docx');
  const buffer = await Packer.toBuffer(doc);
  fs.writeFileSync(outPath, buffer);

  // Tạo thêm bản lưu trong thư mục docs
  const outPathDocs = path.resolve(__dirname, '../../docs/HUONG_DAN_SU_DUNG_ELEARNING.docx');
  fs.writeFileSync(outPathDocs, buffer);

  console.log('File Word generated successfully at:');
  console.log('1.', outPath);
  console.log('2.', outPathDocs);
}

generate().catch(err => {
  console.error('Error generating document:', err);
  process.exit(1);
});
