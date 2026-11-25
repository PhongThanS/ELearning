import type { IframeHTMLAttributes } from "react";

/**
 * Khung xem trước tệp Google Drive / Docs, nạp **không kèm cookie** (`credentialless`).
 * Trình duyệt đã đăng nhập Google (thường gặp trên điện thoại) bị Drive chuyển hướng qua trang đăng nhập,
 * trang này không nằm trong CSP `frame-src` nên khung hiện lỗi. Tệp bài giảng phải chia sẻ "bất kỳ ai có
 * đường liên kết" nên xem ẩn danh được. Trình duyệt không hỗ trợ thuộc tính này thì bỏ qua nó.
 */
export function DriveFrame({ title, ...props }: IframeHTMLAttributes<HTMLIFrameElement> & { title: string }) {
  // React coi `credentialless` là thuộc tính boolean (chuỗi rỗng sẽ bị bỏ) nhưng kiểu TypeScript chưa khai báo.
  // Phải đứng trước src để áp dụng cho lần nạp đầu.
  return <iframe {...{ credentialless: true }} title={title} {...props} />;
}
