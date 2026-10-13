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

// Colors
const PRIMARY = "1E3A8A";    // Deep Navy Blue
const SECONDARY = "2563EB";  // Royal Blue
const ACCENT = "0D9488";     // Teal
const TEXT_DARK = "1E293B";  // Slate Dark
const MUTED = "64748B";      // Gray Slate
const BG_HEADER = "1E3A8A";  // Header row bg
const BG_ZEBRA = "F8FAFC";   // Alternate row bg
const BG_CALLOUT = "EFF6FF"; // Note box bg
const BG_WARN = "FEF3C7";    // Warning box bg
const BORDER_COLOR = "CBD5E1";

// Helper functions for formatting
function titleHeading(text) {
  return new Paragraph({
    spacing: { before: 240, after: 120 },
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
    spacing: { before: 180, after: 80 },
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
    spacing: { before: 140, after: 60 },
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

function callout(title, content, isWarning = false) {
  const bg = isWarning ? BG_WARN : BG_CALLOUT;
  const borderColor = isWarning ? "D97706" : SECONDARY;

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
            text: (isWarning ? "⚠️ " : "ℹ️ ") + title,
            bold: true,
            size: 22,
            color: isWarning ? "B45309" : SECONDARY,
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

function createTable(headers, rowsData) {
  const headerCells = headers.map(h => new TableCell({
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
          alignment: cellIdx === 0 && row.length > 3 ? AlignmentType.CENTER : AlignmentType.LEFT,
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
      // TRANG BÌA
      {
        properties: {},
        children: [
          new Paragraph({ spacing: { before: 1200, after: 0 } }),
          new Paragraph({
            alignment: AlignmentType.CENTER,
            spacing: { before: 200, after: 200 },
            children: [
              new TextRun({
                text: "HỆ THỐNG THI TRỰC TUYẾN & ĐÀO TẠO",
                bold: true,
                size: 26,
                color: MUTED,
                font: "Calibri"
              })
            ]
          }),
          new Paragraph({
            alignment: AlignmentType.CENTER,
            spacing: { before: 100, after: 300 },
            children: [
              new TextRun({
                text: "ELEARNING SYSTEM",
                bold: true,
                size: 52, // 26pt
                color: PRIMARY,
                font: "Calibri"
              })
            ]
          }),
          new Paragraph({
            alignment: AlignmentType.CENTER,
            spacing: { before: 100, after: 600 },
            children: [
              new TextRun({
                text: "SÁCH HƯỚNG DẪN SỬ DỤNG PHẦN MỀM\n(USER MANUAL)",
                bold: true,
                size: 32,
                color: SECONDARY,
                font: "Calibri"
              })
            ]
          }),
          new Paragraph({
            alignment: AlignmentType.CENTER,
            spacing: { before: 100, after: 1200 },
            children: [
              new TextRun({
                text: "Dành trọn bộ cho Học viên, Giáo viên và Quản trị viên (Admin)",
                italics: true,
                size: 24,
                color: TEXT_DARK,
                font: "Calibri"
              })
            ]
          }),

          // Box thông tin phiên bản
          new Table({
            width: { size: 70, type: WidthType.PERCENTAGE },
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
                          new TextRun({ text: "Phiên bản 2.0 (PostgreSQL + React 19)", size: 21 })
                        ]
                      }),
                      new Paragraph({
                        spacing: { before: 40, after: 40 },
                        children: [
                          new TextRun({ text: "• Nền tảng công nghệ: ", bold: true, size: 21, color: PRIMARY }),
                          new TextRun({ text: ".NET 10 API, PostgreSQL 17, Vite & React TS", size: 21 })
                        ]
                      }),
                      new Paragraph({
                        spacing: { before: 40, after: 40 },
                        children: [
                          new TextRun({ text: "• Ngày biên soạn: ", bold: true, size: 21, color: PRIMARY }),
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

      // NỘI DUNG CHÍNH CÓ HEADER / FOOTER
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
          // MỤC LỤC
          titleHeading("MỤC LỤC TÀI LIỆU"),
          para("Tài liệu hướng dẫn sử dụng phần mềm bao gồm các phần chính sau:"),
          bullet("Phần 1: Giới thiệu Tổng quan & Thông tin Đăng nhập Hệ thống", "1."),
          bullet("Phần 2: Hướng dẫn dành cho Học viên (Student Manual)", "2."),
          bullet("Phần 3: Hướng dẫn dành cho Quản trị viên & Giáo viên (Admin & Teacher Manual)", "3."),
          bullet("Phần 4: Cơ chế Chấm thi, Tính điểm & Đảm bảo Tính Toàn vẹn Dữ liệu", "4."),
          bullet("Phần 5: Các Tình huống Thường gặp (FAQ) & Hướng dẫn Xử lý Sự cố", "5."),
          bullet("Phần 6: Phụ lục Hướng dẫn Cài đặt & Khởi động Hệ thống (Dành cho Kỹ thuật viên)", "6."),
          new Paragraph({ spacing: { before: 120, after: 120 } }),

          // PHẦN 1
          titleHeading("PHẦN 1: TỔNG QUAN & ĐĂNG NHẬP HỆ THỐNG"),
          subHeading("1.1. Giới thiệu về Nền tảng ELearning"),
          para("ELearning là hệ thống thi trực tuyến và đào tạo số chuyên nghiệp, được xây dựng trên nền tảng công nghệ cao cấp hiện đại: Backend .NET Core API kết hợp Cơ sở dữ liệu quan hệ PostgreSQL 17 và Giao diện người dùng Web React / TypeScript / Vite."),
          para("Hệ thống giải quyết toàn diện bài toán tổ chức kiểm tra, đánh giá chất lượng học tập từ mức quy mô lớp học tới các kỳ thi tập trung quy mô lớn, với các đặc tính nổi trội:"),
          bullet("Chống gian lận & Giám sát thông minh: Tự động phát hiện học sinh chuyển tab, mất tập trung hoặc thoát cửa sổ làm bài.", "•"),
          bullet("Tự động lưu bài tức thời (Autosave): Từng câu trả lời của thí sinh được lưu an toàn lên máy chủ sau mỗi vài mili-giây, bảo vệ bài làm tuyệt đối trước sự cố mất mạng hoặc sập nguồn điện.", "•"),
          bullet("Hỗ trợ đa dạng loại câu hỏi: Trắc nghiệm 1 đáp án, Trắc nghiệm nhiều đáp án (hỗ trợ chấm từng phần), Đúng / Sai, Điền từ ngữ / Số học (hỗ trợ sai số) và Tự luận (chấm tay có phản hồi).", "•"),
          bullet("Kho Video bài giảng tích hợp: Nhúng video YouTube theo chuyên đề và lớp học với trình phát chuẩn Youtube-NoCookie, tối ưu băng thông máy chủ và loại bỏ hoàn toàn quảng cáo.", "•"),
          bullet("Đề thi nhiều phiên bản (Multi-version): Cho phép giáo viên cập nhật đề thi, phát hành phiên bản mới mà không làm mất lịch sử thi của các phiên bản trước đó.", "•"),

          subHeading("1.2. Hướng dẫn Đăng nhập & Quản lý Tài khoản"),
          subSubHeading("1.2.1. Đăng nhập"),
          para("1. Mở trình duyệt Web (khuyến nghị Google Chrome, Microsoft Edge hoặc Safari phiên bản mới nhất)."),
          para("2. Nhập địa chỉ truy cập ứng dụng (Ví dụ: http://localhost:5173 trên môi trường chạy thử nghiệm hoặc tên miền chính thức của đơn vị)."),
          para("3. Tại màn hình Đăng nhập (/login):"),
          bullet("Tên đăng nhập (Username): Nhập tên tài khoản hoặc mã định danh do nhà trường / quản trị viên cung cấp.", "•"),
          bullet("Mật khẩu (Password): Nhập mật khẩu hiện tại.", "•"),
          bullet("Nhấn nút 'Đăng nhập'. Hệ thống sẽ tự động chuyển hướng bạn đến khu vực làm việc phù hợp với vai trò của mình (Học viên vào trang Danh sách đề thi, Quản trị viên vào Bảng điều khiển Admin).", "•"),

          callout("Tài khoản thử nghiệm sẵn có trên hệ thống:", 
            "• Quản trị viên (Admin): admin / Mật khẩu: Admin@123456\n• Học viên mẫu (Student): student01 hoặc student02 / Mật khẩu: Student@123456"),

          subSubHeading("1.2.2. Đổi mật khẩu cá nhân"),
          para("Để đảm bảo an toàn bảo mật, người dùng được khuyến nghị đổi mật khẩu ngay sau lần đăng nhập đầu tiên:"),
          para("1. Bấm vào biểu tượng ảnh đại diện (Avatar) hoặc tên người dùng ở góc trên bên phải màn hình."),
          para("2. Chọn mục 'Đổi mật khẩu' (/change-password)."),
          para("3. Nhập mật khẩu hiện tại, sau đó nhập mật khẩu mới và xác nhận mật khẩu mới (Mật khẩu tối thiểu 8 ký tự, bao gồm chữ hoa, chữ thường và chữ số)."),
          para("4. Bấm 'Lưu thay đổi'. Hệ thống sẽ tự động cập nhật và duy trì phiên đăng nhập an toàn."),

          new Paragraph({ spacing: { before: 120, after: 120 } }),

          // PHẦN 2
          titleHeading("PHẦN 2: HƯỚNG DẪN DÀNH CHO HỌC VIÊN (STUDENT)"),
          para("Giao diện học viên được tối ưu tinh gọn, trực quan, thân thiện với thiết bị máy tính để bàn, laptop cũng như máy tính bảng."),

          subHeading("2.1. Thanh Điều Hướng & Các Phân Hệ Chính"),
          para("Sau khi đăng nhập thành công, học viên sẽ nhìn thấy thanh menu điều hướng ở đầu trang với các mục chức năng:"),
          bullet("Kỳ thi & Bài kiểm tra (/student/exams): Hiển thị danh sách các đề thi được giao cho bạn hoặc các đề thi công khai.", "1."),
          bullet("Lớp học của tôi (/student/classes): Tra cứu danh sách các lớp học mà học viên đã tham gia, cùng danh sách bạn học và giáo viên phụ trách.", "2."),
          bullet("Video bài giảng (/student/videos): Xem các bài giảng video được phân loại theo chuyên đề và lớp học.", "3."),
          bullet("Lịch sử làm bài (/student/history): Xem lại toàn bộ các lượt thi bạn đã từng tham gia, kèm bảng thống kê điểm số và ngày giờ nộp bài.", "4."),

          subHeading("2.2. Kho Video Bài Giảng Trực Tuyến"),
          para("Tính năng Video bài giảng giúp học viên tự ôn luyện kiến thức trước hoặc sau các buổi thi:"),
          para("1. Truy cập vào mục 'Video bài giảng' trên thanh menu."),
          para("2. Sử dụng thanh công cụ lọc để chọn nhanh chuyên đề môn học (ví dụ: 'Lập trình C#', 'Cơ sở dữ liệu',...) hoặc lọc theo lớp học tương ứng."),
          para("3. Bấm vào thẻ video để mở cửa sổ phát video. Trình phát được tích hợp chế độ YouTube bảo mật, loại bỏ phiền toái từ quảng cáo, giúp học viên tập trung cao độ vào bài học."),

          subHeading("2.3. Quy Trình Làm Bài Thi Trực Tuyến"),
          para("Đây là tính năng cốt lõi quan trọng nhất của học viên trên hệ thống:"),

          subSubHeading("Bước 1: Chọn đề thi và xem thông tin thể lệ"),
          para("Tại trang 'Kỳ thi & Bài kiểm tra':"),
          bullet("Mỗi đề thi được hiển thị dưới dạng thẻ thông tin bao gồm: Tên đề thi, Mã đề, Thời lượng làm bài (phút), Điểm đạt yêu cầu (%), Thời gian mở/đóng đề và Số lượt thi còn lại.", "•"),
          bullet("Nhấn 'Xem chi tiết' để đọc kỹ hướng dẫn làm bài thi từ giáo viên.", "•"),
          bullet("Nếu đề thi ở trạng thái 'ĐANG MỞ' (AVAILABLE) và bạn còn lượt thi, nút 'Bắt đầu làm bài' sẽ sáng màu xanh. Nhấn nút này để bước vào phòng thi.", "•"),

          subSubHeading("Bước 2: Giao diện phòng thi (Exam Player)"),
          para("Màn hình làm bài được thiết kế theo tiêu chuẩn thi chuyên nghiệp toàn màn hình, bao gồm:"),
          bullet("Thanh công cụ đỉnh trang: Hiển thị tên bài thi, tiến độ trả lời (Ví dụ: 'Đã làm 15/40 câu') và Đồng hồ đếm ngược (Timer). Đồng hồ sẽ chuyển sang màu cam/đỏ khi sắp hết giờ.", "•"),
          bullet("Bảng điều hướng câu hỏi (Sidebar / Panel danh sách câu): Đánh số từ 1 đến N. Các câu đã trả lời sẽ được đổi màu để học sinh dễ nhận biết câu nào đã làm và câu nào còn sót.", "•"),
          bullet("Nút 'Đánh dấu xem lại' (Bookmark): Cho phép bạn đánh dấu các câu hỏi bạn còn phân vân để quay lại kiểm tra trước khi nộp bài.", "•"),
          bullet("Khu vực hiển thị câu hỏi: Nội dung câu hỏi hỗ trợ văn bản giàu định dạng (Markdown), công thức, khối mã nguồn (code snippet) và hình ảnh minh họa chất lượng cao.", "•"),

          subSubHeading("Bước 3: Thao tác trả lời theo từng dạng câu hỏi"),
          createTable(
            ["Dạng câu hỏi", "Cách thức thao tác của Thí sinh"],
            [
              ["Trắc nghiệm chọn 1 (Single Choice)", "Click vào ô tròn (Radio) của phương án bạn chọn (A, B, C hoặc D). Bạn có thể đổi lựa chọn bất kỳ lúc nào."],
              ["Trắc nghiệm nhiều đáp án (Multiple Choice)", "Click vào ô vuông (Checkbox) của các phương án đúng. Câu hỏi có thể có từ 2 đáp án đúng trở lên."],
              ["Đúng / Sai (True / False)", "Chọn phương án 'Đúng' hoặc 'Sai' tương ứng với nhận định trong đề bài."],
              ["Điền từ / Số học (Fill In)", "Gõ câu trả lời vào ô nhập liệu. Đối với câu số học, có thể nhập số nguyên hoặc số thập phân (dùng dấu chấm hoặc phẩy)."],
              ["Tự luận (Essay)", "Gõ nội dung bài làm trực tiếp vào khung soạn thảo văn bản. Bài làm tự luận sẽ được giáo viên chấm tay và nhận xét sau."]
            ]
          ),

          callout("Cơ chế Lưu bài tự động (Autosave):",
            "Mỗi khi bạn chọn một đáp án hoặc gõ một từ, hệ thống sẽ lập tức gửi tín hiệu lưu ngầm về máy chủ. Bạn không cần lo lắng về việc mất bài làm nếu mạng chập chờn hay thiết bị gặp sự cố. Khi đăng nhập lại, bài làm sẽ giữ nguyên trạng thái câu trả lời gần nhất của bạn."),

          subSubHeading("Bước 4: Nộp bài và Tự động nộp bài"),
          para("1. Khi đã hoàn thành toàn bộ bài thi hoặc muốn kết thúc sớm, nhấn nút 'Nộp bài' ở góc trên bên phải."),
          para("2. Hệ thống sẽ hiển thị một bảng tóm tắt: Số câu đã làm, số câu chưa trả lời, các câu đã đánh dấu xem lại. Nếu chắc chắn, nhấn 'Xác nhận nộp bài'."),
          para("3. Tự động nộp khi hết giờ: Nếu đồng hồ đếm ngược về 00:00:00, hệ thống sẽ tự động khóa bài thi và nộp toàn bộ các đáp án đã lưu lên máy chủ ngay lập tức. Thí sinh không bị mất điểm."),

          subHeading("2.4. Xem Điểm Số & Tra Cứu Kết Quả"),
          para("Sau khi nộp bài, màn hình kết quả sẽ hiển thị ngay lập tức với biểu đồ vòng tròn điểm số:"),
          bullet("Điểm số đạt được: Điểm thi thực tế trên thang điểm tối đa (Ví dụ: 8.5 / 10.0) và tỉ lệ phần trăm.", "•"),
          bullet("Kết quả tổng quan: Trạng thái 'ĐẠT' (Màu xanh lá) hoặc 'CHƯA ĐẠT' (Màu đỏ), số câu làm đúng / tổng số câu, thời gian hoàn thành.", "•"),
          bullet("Quyền xem lại chi tiết câu hỏi (Chính sách bảo mật đề):", "•"),
          para("   - Nếu đề thi đang được Quản trị viên 'Khóa xem lại chi tiết', màn hình sẽ hiển thị biểu tượng ổ khóa: 'Kết quả bài thi đã được ghi nhận. Chi tiết câu hỏi và đáp án sẽ hiển thị sau khi giáo viên / quản trị viên mở quyền xem lại.'"),
          para("   - Khi giáo viên mở quyền, bạn sẽ xem được toàn bộ chi tiết từng câu hỏi, kèm icon trực quan: Phương án bạn chọn đúng (Badge xanh), Phương án bạn chọn sai (Badge đỏ), Đáp án chuẩn của đề thi và Lời giải thích chi tiết."),

          new Paragraph({ spacing: { before: 120, after: 120 } }),

          // PHẦN 3
          titleHeading("PHẦN 3: HƯỚNG DẪN DÀNH CHO QUẢN TRỊ VIÊN & GIÁO VIÊN"),
          para("Khu vực Quản trị (/admin) là trung tâm điều hành toàn diện của trường học hoặc đơn vị đào tạo, với đầy đủ các nghiệp vụ từ tổ chức ngân hàng câu hỏi, tạo đề thi, phân lớp đến quản lý phòng thi và xuất báo cáo."),

          subHeading("3.1. Bảng Điều Khiển (Dashboard)"),
          para("Trang Dashboard (/admin/dashboard) cung cấp góc nhìn toàn cảnh về tình hình thi cử và học tập:"),
          bullet("Chỉ số tổng hợp (KPI Cards): Tổng số học sinh, Tổng số đề thi đang hoạt động, Tổng số lượt làm bài, Điểm trung bình toàn hệ thống.", "•"),
          bullet("Biểu đồ phân phối điểm số và biểu đồ lượt thi theo thời gian thực.", "•"),
          bullet("Cảnh báo vận hành: Nhắc nhở các bài thi tự luận đang chờ giáo viên chấm, các lượt thi bị quá hạn cần can thiệp xử lý.", "•"),

          subHeading("3.2. Quản Lý Người Dùng, Nhóm & Lớp Học"),
          subSubHeading("3.2.1. Quản lý Tài khoản (Users)"),
          para("Truy cập mục 'Người dùng' (/admin/users):"),
          bullet("Bật / Tắt tính năng tự đăng ký học viên (Registration Control): Ngay đầu trang Người dùng có thẻ 'Cấu hình Đăng ký tài khoản học sinh' với nút 'Mở tự đăng ký' / 'Khóa tự đăng ký' tức thì chỉ với 1 cú nhấp chuột mà không cần sửa cấu hình hay khởi động lại máy chủ.", "•"),
          bullet("Thêm người dùng mới: Nhập Username, Họ và tên, Email, Mật khẩu ban đầu và gán Vai trò (Admin, Student, Giáo viên...).", "•"),
          bullet("Khóa / Kích hoạt tài khoản: Khi cần tạm ngưng quyền truy cập của một học sinh, chỉ cần gạt nút chuyển trạng thái sang Khóa.", "•"),
          bullet("Đặt lại mật khẩu (Reset Password): Hỗ trợ học sinh trong trường hợp quên mật khẩu mà không cần thao tác email phức tạp.", "•"),

          subSubHeading("3.2.2. Quản lý Lớp học (Classrooms) & Nhóm người dùng"),
          para("1. Vào mục 'Lớp học' (/admin/classes): Tạo lớp học mới (Ví dụ: 'Lớp 12A1 - Năm học 2026')."),
          para("2. Thêm học viên vào lớp: Tìm kiếm học viên theo tên hoặc tài khoản và thêm vào danh sách lớp chỉ bằng một cú nhấp chuột."),
          para("3. Tác dụng của Lớp học: Dùng để phân công đề thi riêng cho từng lớp và chia sẻ các video bài giảng phù hợp với chương trình học của lớp đó."),

          subHeading("3.3. Quản Lý Ngân Hàng Câu Hỏi (Question Bank)"),
          subSubHeading("3.3.1. Phân loại Chuyên đề (Categories)"),
          para("Vào mục 'Chuyên đề' (/admin/categories) để tạo cây danh mục kiến thức (Ví dụ: Toán Đại số, Lập trình C#, Tiếng Anh Cơ bản...). Mọi câu hỏi và video bài giảng đều được gắn với Chuyên đề để dễ dàng quản lý và tái sử dụng."),

          subSubHeading("3.3.2. Tạo và Soạn thảo Câu hỏi"),
          para("Nhấn 'Thêm câu hỏi' (/admin/questions/create):"),
          bullet("Mã câu hỏi: Để trống để hệ thống tự động sinh mã dạng 'Q000123' hoặc tự đặt mã theo quy chuẩn của trường.", "•"),
          bullet("Chọn Loại câu hỏi: Chọn 1 trong 4 loại trắc nghiệm hoặc Tự luận.", "•"),
          bullet("Nội dung câu hỏi: Trình soạn thảo hỗ trợ Markdown đầy đủ, có nút 'Chèn ảnh' trực quan. Bạn có thể tải ảnh sơ đồ, biểu đồ từ máy tính (hỗ trợ JPG, PNG, GIF, WebP). Hệ thống sẽ tự động tối ưu và lưu trữ ảnh an toàn.", "•"),
          bullet("Thiết lập phương án & Đáp án đúng: Nhập nội dung các lựa chọn A, B, C, D... và tick chọn phương án đúng.", "•"),
          bullet("Chấm từng phần (Partial Scoring): Đối với câu chọn nhiều, tick chọn 'Bật chấm từng phần' để học sinh làm đúng một phần vẫn được điểm tương ứng.", "•"),
          bullet("Giải thích đáp án: Soạn thảo lời giải chi tiết (sẽ hiển thị cho học sinh khi được phép xem lại).", "•"),

          subSubHeading("3.3.3. Nhập câu hỏi hàng loạt từ Excel (Import Excel)"),
          para("1. Tại trang Ngân hàng câu hỏi, nhấn nút 'Tải file mẫu Excel'."),
          para("2. Điền danh sách câu hỏi theo các cột quy định trong file mẫu."),
          para("3. Nhấn nút 'Nhập từ Excel', chọn file dữ liệu và bấm 'Kiểm tra trước' (Dry Run)."),
          para("4. Nếu có lỗi sai dòng hoặc thiếu cột, hệ thống sẽ chỉ rõ chính xác số dòng lỗi. Nếu hợp lệ 100%, nhấn 'Thực hiện nhập' để thêm toàn bộ câu hỏi vào hệ thống."),

          subHeading("3.4. Quản Lý Kho Video Bài Giảng"),
          para("Quản trị viên có thể làm phong phú kho học liệu số của trường (/admin/videos):"),
          bullet("Thêm video mới: Dán đường link YouTube bài giảng (Ví dụ: https://www.youtube.com/watch?v=dQw4w9WgXcQ).", "•"),
          bullet("Tự động trích xuất: Hệ thống sẽ tự động bóc tách mã Video ID và tự động lấy ảnh bìa đại diện (Thumbnail HD) từ YouTube mà bạn không cần tải ảnh thủ công.", "•"),
          bullet("Gán chuyên đề & Lớp học: Chọn chuyên đề môn học và lớp học được phép xem.", "•"),
          bullet("Bật/tắt trạng thái hiển thị: Ẩn hoặc hiện video trên giao diện học viên bất cứ lúc nào.", "•"),

          subHeading("3.5. Xây Dựng & Phát Hành Đề Thi (Exams)"),
          subSubHeading("3.5.1. Tạo đề thi mới"),
          para("Vào mục 'Đề thi' (/admin/exams) -> Nhấn 'Tạo đề thi':"),
          bullet("Thông tin cơ bản: Tên đề thi, Mã đề thi, Thời lượng làm bài (phút), Điểm đạt yêu cầu (%).", "•"),
          bullet("Thời gian hiệu lực: Thiết lập 'Bắt đầu lúc' và 'Kết thúc lúc' để giới hạn khung giờ mở đề thi.", "•"),
          bullet("Số lượt thi tối đa: Đặt 1 lượt (thi chính thức) hoặc nhiều lượt (thi thử/luyện tập).", "•"),
          bullet("Chế độ truy cập (Access Mode):", "•"),
          para("   - 'PUBLIC': Mọi học viên trong hệ thống đều nhìn thấy và được thi."),
          para("   - 'ASSIGNED': Chỉ những Lớp học hoặc Học viên cụ thể được gán đề mới có quyền vào thi."),
          bullet("Tùy chọn xáo đề: Tick chọn 'Xáo trộn thứ tự câu hỏi' và 'Xáo trộn thứ tự các lựa chọn' để hạn chế tối đa việc chép bài."),

          subSubHeading("3.5.2. Chọn câu hỏi & Thiết lập điểm (Version Builder)"),
          para("Sau khi tạo đề, hệ thống sẽ mở ra giao diện 'Trình tạo phiên bản đề thi':"),
          para("1. Nhấn nút 'Thêm câu hỏi từ ngân hàng': Lọc theo chuyên đề, độ khó, dạng câu hỏi và tick chọn các câu cần đưa vào đề thi."),
          para("2. Điều chỉnh điểm số cho từng câu (Mặc định 1 điểm hoặc tùy chỉnh 0.25, 0.5, 2.0 điểm...)."),
          para("3. Sắp xếp lại thứ tự câu hỏi bằng thao tác kéo thả hoặc bấm nút di chuyển."),
          para("4. Bấm 'Xuất bản đề thi' (Publish): Sau khi xuất bản, đề thi sẽ chuyển sang trạng thái sẵn sàng để học sinh vào thi. Hệ thống sẽ tạo một bản snapshot đóng băng nội dung câu hỏi để đảm bảo tính bất biến của đề thi."),

          subSubHeading("3.5.3. Bật / Tắt Quyền xem kết quả chỉ với 1 Click"),
          para("Tại trang chi tiết đề thi (/admin/exams/:id), quản trị viên có thẻ điều khiển 'Quyền xem lại kết quả bài thi':"),
          bullet("Nút 'Mở cho học sinh xem chi tiết': Ngay khi kết thúc giờ thi chung, bấm nút này để toàn bộ học sinh có thể xem lại chi tiết bài làm và đáp án đúng.", "•"),
          bullet("Nút 'Khóa xem lại chi tiết': Khóa quyền xem lại tức thì nếu muốn bảo mật đề thi cho các đợt thi sau.", "•"),

          subHeading("3.6. Giám Sát Phòng Thi, Chấm Điểm & Xuất Báo Cáo"),
          subSubHeading("3.6.1. Giám sát & Can thiệp lượt thi đang làm"),
          para("Tại trang Kết quả đề thi (/admin/exams/:id/results):"),
          bullet("Theo dõi danh sách các thí sinh đang làm bài và đã nộp bài trong thời gian thực.", "•"),
          bullet("Gia hạn thời gian (Add time): Nếu một học sinh gặp sự cố mạng hoặc máy móc, quản trị viên có thể cộng thêm số phút làm bài cho riêng học sinh đó.", "•"),
          bullet("Buộc nộp bài (Force submit): Buộc kết thúc lượt thi của thí sinh vi phạm quy chế.", "•"),
          bullet("Cấp thêm lượt thi: Cho phép thí sinh thi lại nếu có lý do chính đáng.", "•"),

          subSubHeading("3.6.2. Chấm bài tự luận (Manual Grading)"),
          para("1. Đối với đề thi có câu hỏi tự luận, tab 'Chấm tự luận' sẽ liệt kê danh sách các bài làm đang chờ chấm điểm."),
          para("2. Giáo viên xem nội dung bài làm của thí sinh, đối chiếu với đáp án mẫu."),
          para("3. Nhập số điểm chấm (từ 0 đến điểm tối đa của câu) và nhập 'Nhận xét của giáo viên'."),
          para("4. Bấm 'Lưu điểm chấm'. Hệ thống sẽ tự động tính lại tổng điểm và cập nhật trạng thái Đạt/Chưa đạt cho học sinh."),

          subSubHeading("3.6.3. Sửa đáp án đề thi & Chấm lại tự động (Regrade)"),
          para("Trong trường hợp hy hữu khi đề thi đã phát hiện có câu hỏi bị nhầm đáp án sau khi thi:"),
          para("1. Quản trị viên vào chi tiết đề thi -> chọn 'Sửa đáp án / Hủy câu'."),
          para("2. Chọn câu hỏi cần xử lý: Cập nhật lại đáp án đúng hoặc chọn 'Hủy câu' (mọi thí sinh đều được trọn điểm câu này)."),
          para("3. Nhập lý do thay đổi và bấm 'Chấm lại toàn bộ lượt thi'."),
          para("4. Hệ thống sẽ tự động quét lại toàn bộ bài làm của tất cả học sinh và tính lại điểm số chuẩn xác chỉ trong vài giây, đồng thời ghi lại vết trong Nhật ký hệ thống."),

          subSubHeading("3.6.4. Xuất Báo cáo Kết quả ra file Excel"),
          para("Nhấn nút 'Xuất Excel' tại trang kết quả. Hệ thống sẽ kết xuất file Excel chuẩn báo cáo bao gồm: Mã học viên, Họ và tên, Lớp học, Điểm số, Tỉ lệ %, Số câu đúng/sai, Thời gian bắt đầu, Thời gian nộp bài và Xếp loại."),

          new Paragraph({ spacing: { before: 120, after: 120 } }),

          // PHẦN 4
          titleHeading("PHẦN 4: CƠ CHẾ BẢO MẬT & ĐẢM BẢO TÍNH TOÀN VẸN"),
          subHeading("4.1. Nguyên Tắc 'Máy Chủ Là Nguồn Dữ Liệu Chuẩn'"),
          para("Hệ thống ELearning tuân thủ nghiêm ngặt các tiêu chuẩn bảo mật dữ liệu thi trực tuyến:"),
          bullet("Toàn bộ việc tính giờ, thời điểm hết hạn và chấm điểm đều thực hiện 100% tại máy chủ (Backend), không tin cậy mã nguồn phía trình duyệt người dùng.", "1."),
          bullet("Khi bắt đầu bài thi, câu hỏi gửi về cho học sinh chỉ bao gồm nội dung và các phương án lựa chọn. Đáp án đúng và giải thích hoàn toàn KHÔNG bao giờ được tải về máy của học sinh trong suốt quá trình làm bài.", "2."),
          bullet("Cơ chế chống gửi bài thi trùng lặp (Idempotent): Đảm bảo khi học sinh bấm nộp bài nhiều lần liên tiếp, hệ thống chỉ ghi nhận đúng một kết quả duy nhất.", "3."),

          subHeading("4.2. Nhật Ký Giám Sát Hệ Thống (Audit Logs)"),
          para("Mọi hành động quan trọng trên hệ thống đều được ghi lại vĩnh viễn trong Nhật ký giám sát (/admin/audit-logs):"),
          bullet("Ai đã tạo, sửa hoặc xuất bản đề thi?", "•"),
          bullet("Ai đã mở quyền hoặc khóa quyền xem lại kết quả bài thi?", "•"),
          bullet("Ai đã gia hạn thời gian, hủy lượt hoặc can thiệp điểm số bài thi?", "•"),
          bullet("Các sự kiện bất thường trong quá trình thí sinh làm bài thi (chuyển tab, mất kết nối mạng...).", "•"),

          new Paragraph({ spacing: { before: 120, after: 120 } }),

          // PHẦN 5
          titleHeading("PHẦN 5: CÂU HỎI THƯỜNG GẶP (FAQ) & XỬ LÝ SỰ CỐ"),
          createTable(
            ["Tình huống / Câu hỏi", "Nguyên nhân & Hướng dẫn xử lý"],
            [
              [
                "Học sinh bị mất mạng hoặc mất điện đột ngột khi đang làm bài thi?",
                "Học sinh chỉ cần đổi thiết bị khác hoặc chờ có mạng lại, đăng nhập tài khoản và bấm vào đề thi để làm tiếp. Toàn bộ các câu trả lời trước đó đã được hệ thống lưu tự động qua cơ chế Autosave."
              ],
              [
                "Học sinh thi xong nhưng không nhìn thấy câu nào đúng, câu nào sai?",
                "Do giáo viên đang thiết lập chế độ bảo mật đề 'Khóa xem lại chi tiết'. Điểm tổng quan của bạn đã được ghi nhận. Khi giáo viên kết thúc đợt thi và bấm 'Mở cho học sinh xem chi tiết', bạn sẽ xem được toàn bộ lời giải."
              ],
              [
                "Đề thi bị hết số lượt làm bài nhưng giáo viên đã cập nhật đề mới?",
                "Hệ thống hỗ trợ Đề thi nhiều phiên bản (Multi-version). Khi giáo viên xuất bản phiên bản mới (v2), số lượt làm của học sinh sẽ được làm mới cho phiên bản đó, học sinh chỉ cần bấm 'Bắt đầu làm bài' phiên bản mới."
              ],
              [
                "Nhập file Excel câu hỏi bị báo lỗi dòng?",
                "Kiểm tra lại sheet đầu tiên có tên 'CauHoi', các cột tiêu đề không được đổi tên. Hãy đảm bảo mã chuyên đề trùng khớp với mã đã tạo trong hệ thống và các phương án lựa chọn được điền liên tục từ A, B, C."
              ],
              [
                "Video bài giảng YouTube báo lỗi 'Video không khả dụng'?",
                "Kiểm tra lại quyền riêng tư của video trên YouTube: Video phải để ở chế độ 'Công khai' (Public) hoặc 'Không công khai' (Unlisted). Không được để chế độ 'Riêng tư' (Private) vì trình phát sẽ bị YouTube chặn."
              ]
            ]
          ),

          new Paragraph({ spacing: { before: 120, after: 120 } }),

          // PHẦN 6
          titleHeading("PHẦN 6: PHỤ LỤC KỸ THUẬT & HƯỚNG DẪN KHỞI CHẠY"),
          subHeading("6.1. Yêu Cầu Môi Trường Cài Đặt"),
          bullet("Hệ điều hành: Windows 10/11, Windows Server hoặc Linux (Ubuntu 22.04+).", "•"),
          bullet("Cơ sở dữ liệu: PostgreSQL 16 hoặc 17 (Cổng mặc định: 5432 hoặc 5433).", "•"),
          bullet("Môi trường thực thi: .NET SDK 10 (hoặc .NET 9 trở lên) và Node.js phiên bản 22 trở lên.", "•"),
          bullet("Công cụ quản trị cơ sở dữ liệu: pgAdmin 4 hoặc DBeaver.", "•"),

          subHeading("6.2. Hướng Dẫn Khởi Động Nhanh Hệ Thống Bằng Lệnh"),
          subSubHeading("Bước 1: Khởi động Cơ sở dữ liệu PostgreSQL"),
          para("Đảm bảo dịch vụ PostgreSQL đang chạy trên máy tính hoặc khởi động qua Docker:"),
          para("docker compose up -d postgres"),

          subSubHeading("Bước 2: Cập nhật Migration & Khởi động Backend API"),
          para("Mở cửa sổ dòng lệnh Terminal tại thư mục gốc của dự án:"),
          para("cd backend"),
          para("dotnet ef database update -p src/ELearning.Infrastructure -s src/ELearning.Api"),
          para("dotnet run --project src/ELearning.Api --launch-profile http"),
          para("-> Backend sẽ chạy tại địa chỉ: http://localhost:5136 (Swagger UI: http://localhost:5136/swagger)"),

          subSubHeading("Bước 3: Khởi động Giao diện Frontend React"),
          para("Mở một cửa sổ Terminal thứ hai:"),
          para("cd frontend/elearning-web"),
          para("npm install"),
          para("npm run dev"),
          para("-> Giao diện Web sẽ sẵn sàng tại địa chỉ: http://localhost:5173"),

          callout("Khởi động toàn bộ chỉ bằng 1 lệnh Docker:",
            "Nếu bạn đã cài đặt Docker Desktop, chỉ cần đứng tại thư mục gốc và chạy lệnh:\ndocker compose up -d --build\nHệ thống sẽ tự động cấu hình cơ sở dữ liệu, chạy migration và bật toàn bộ website tại cổng http://localhost:8080."),

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
