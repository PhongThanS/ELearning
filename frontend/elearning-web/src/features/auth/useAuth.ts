import { createContext, useContext } from "react";
import type { AuthResponse, AuthUser } from "../../types/api";

export type AuthStatus = "loading" | "authenticated" | "anonymous";

/**
 * LOGOUT: người dùng chủ động đăng xuất (kể cả từ tab khác) → đăng nhập lại về trang chủ.
 * EXPIRED: phiên hết hạn → đăng nhập lại đúng người đó thì quay về trang đang xem.
 */
export type SessionEnd = { reason: "LOGOUT" } | { reason: "EXPIRED"; userId: string | null };

/** State gửi kèm khi chuyển tới /login. userId: chỉ quay lại `from` nếu cùng người dùng. */
export interface LoginRedirectState {
  from?: string;
  userId?: string | null;
  notice?: string;
}

/** Trang cần quay lại sau khi đăng nhập; "/" nếu trang đó thuộc về người dùng khác. */
export function resolveLoginRedirect(state: LoginRedirectState | null, userId: string): string {
  if (!state?.from) {
    return "/";
  }
  return state.userId && state.userId !== userId ? "/" : state.from;
}

export interface AuthContextValue {
  status: AuthStatus;
  user: AuthUser | null;
  /** Phiên trước kết thúc thế nào (null: chưa từng đăng nhập trong lần tải trang này). */
  sessionEnd: SessionEnd | null;
  login: (userName: string, password: string) => Promise<AuthUser>;
  logout: () => Promise<void>;
  applySession: (session: AuthResponse) => void;
  hasPermission: (permission: string) => boolean;
  hasRole: (role: string) => boolean;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth phải nằm trong AuthProvider");
  }
  return context;
}
