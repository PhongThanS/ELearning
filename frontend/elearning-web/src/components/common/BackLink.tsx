import { Link, useLocation } from "react-router";

/**
 * State gửi kèm khi mở một trang con (bài làm, xem đề…) từ một trang khác,
 * để nút "←" ở trang con quay về đúng trang đã bấm.
 */
export interface BackState {
  backTo: string;
  backLabel: string;
}

/** Tạo state quay lại trang hiện tại, gắn vào `<Link state={…}>`. */
export function useBackState(label: string): BackState {
  const location = useLocation();
  return { backTo: location.pathname + location.search, backLabel: label };
}

/**
 * Nút "← …" ở đầu trang con. Mở từ một trang có gửi `BackState` thì quay về trang đó;
 * không có (gõ thẳng URL, tải lại trang) thì về `to` mặc định.
 */
export function BackLink({ to, label }: { to: string; label: string }) {
  const state = useLocation().state as Partial<BackState> | null;
  const backTo = typeof state?.backTo === "string" && state.backTo.startsWith("/") ? state.backTo : null;
  return (
    <Link to={backTo ?? to} className="small">
      ← {backTo && state?.backLabel ? state.backLabel : label}
    </Link>
  );
}
