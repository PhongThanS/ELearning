import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { authApi } from "../../services/api";
import { refreshSession, setSessionHandlers, tokenStore } from "../../services/apiClient";
import type { AuthResponse, AuthUser } from "../../types/api";
import { AuthContext, type AuthContextValue, type AuthStatus, type SessionEnd } from "./useAuth";

const channelName = "elearning-auth";

/**
 * Trạng thái đăng nhập (docs/06-frontend.md mục 2):
 * - khi tải trang gọi /auth/refresh (trình duyệt tự gửi cookie HttpOnly) để lấy access token;
 * - access token chỉ nằm trong bộ nhớ;
 * - đăng xuất đồng bộ giữa các tab qua BroadcastChannel.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<AuthStatus>("loading");
  const [user, setUser] = useState<AuthUser | null>(null);
  const [sessionEnd, setSessionEnd] = useState<SessionEnd | null>(null);
  const userIdRef = useRef<string | null>(null);

  const applySession = useCallback((session: AuthResponse) => {
    tokenStore.set(session.accessToken);
    userIdRef.current = session.user.id;
    setUser(session.user);
    setSessionEnd(null);
    setStatus("authenticated");
  }, []);

  const clearSession = useCallback(
    (reason: SessionEnd["reason"]) => {
      tokenStore.set(null);
      setSessionEnd(reason === "EXPIRED" ? { reason, userId: userIdRef.current } : { reason });
      userIdRef.current = null;
      setUser(null);
      setStatus("anonymous");
      queryClient.clear();
      // Phiên hết hạn giữa giờ thi: giữ backup để đăng nhập lại không mất câu trả lời chưa lưu.
      if (reason === "LOGOUT") {
        clearExamBackups();
      }
    },
    [queryClient],
  );

  useEffect(() => {
    setSessionHandlers({ expired: () => clearSession("EXPIRED"), refreshed: applySession });
    let cancelled = false;
    void refreshSession().then((session) => {
      if (cancelled) {
        return;
      }
      if (session) {
        applySession(session);
      } else {
        setStatus("anonymous");
      }
    });

    const channel = typeof BroadcastChannel === "undefined" ? null : new BroadcastChannel(channelName);
    if (channel) {
      channel.onmessage = (event: MessageEvent<string>) => {
        if (event.data === "logout") {
          clearSession("LOGOUT");
        }
      };
    }

    return () => {
      cancelled = true;
      channel?.close();
    };
  }, [applySession, clearSession]);

  const login = useCallback(
    async (userName: string, password: string) => {
      const session = await authApi.login(userName, password);
      applySession(session);
      return session.user;
    },
    [applySession],
  );

  const logout = useCallback(async () => {
    try {
      await authApi.logout();
    } finally {
      clearSession("LOGOUT");
      if (typeof BroadcastChannel !== "undefined") {
        const channel = new BroadcastChannel(channelName);
        channel.postMessage("logout");
        channel.close();
      }
    }
  }, [clearSession]);

  const value = useMemo<AuthContextValue>(
    () => ({
      status,
      user,
      sessionEnd,
      login,
      logout,
      applySession,
      hasPermission: (permission) => user?.permissions.includes(permission) ?? false,
      hasRole: (role) => user?.roles.includes(role) ?? false,
    }),
    [status, user, sessionEnd, login, logout, applySession],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}


/** Xóa backup bài làm trong sessionStorage khi đăng xuất (máy dùng chung — D-18). */
function clearExamBackups() {
  try {
    for (let i = sessionStorage.length - 1; i >= 0; i--) {
      const key = sessionStorage.key(i);
      if (key?.startsWith("exam_attempt_")) {
        sessionStorage.removeItem(key);
      }
    }
  } catch {
    // sessionStorage có thể bị chặn; không ảnh hưởng đăng xuất.
  }
}
