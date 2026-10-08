import axios, { AxiosError, type AxiosRequestConfig, type InternalAxiosRequestConfig } from "axios";
import type { ApiEnvelope, ApiErrorItem, AuthResponse } from "../types/api";
import { errorMessage } from "../i18n";

/**
 * Một instance Axios duy nhất (docs/03-kien-truc.md mục 7):
 * - access token chỉ giữ trong bộ nhớ (D-15), không lưu localStorage;
 * - gặp 401 thì refresh MỘT lần, các request song song dùng chung promise, rồi gửi lại;
 * - refresh thất bại thì báo AuthProvider đăng xuất.
 */

let accessToken: string | null = null;
let onSessionExpired: (() => void) | null = null;
let onSessionRefreshed: ((session: AuthResponse) => void) | null = null;
let refreshPromise: Promise<AuthResponse | null> | null = null;

export const tokenStore = {
  get: () => accessToken,
  set: (token: string | null) => {
    accessToken = token;
  },
};

export function setSessionHandlers(handlers: {
  expired: () => void;
  refreshed: (session: AuthResponse) => void;
}) {
  onSessionExpired = handlers.expired;
  onSessionRefreshed = handlers.refreshed;
}

/** Lỗi API đã chuẩn hóa: frontend quyết định theo `code`, không theo message (docs/05-api.md mục 2). */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly errors: ApiErrorItem[];
  readonly traceId: string | null;

  constructor(status: number, code: string, message: string, errors: ApiErrorItem[], traceId: string | null) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.code = code;
    this.errors = errors;
    this.traceId = traceId;
  }

  /** Lỗi theo trường, dùng để gắn vào form. */
  fieldErrors(): Record<string, string> {
    const result: Record<string, string> = {};
    for (const e of this.errors) {
      if (e.field && !result[e.field]) {
        result[e.field] = e.code ? errorMessage(e.code, e.message) : e.message;
      }
    }
    return result;
  }

  get isNetworkError() {
    return this.status === 0;
  }
}

export const apiClient = axios.create({
  baseURL: "/api",
  withCredentials: true,
  headers: { "X-Requested-With": "XMLHttpRequest" },
});

apiClient.interceptors.request.use((config) => {
  if (accessToken) {
    config.headers.set("Authorization", `Bearer ${accessToken}`);
  }
  return config;
});

type RetriableConfig = InternalAxiosRequestConfig & { _retried?: boolean };

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiEnvelope<unknown>>) => {
    const config = error.config as RetriableConfig | undefined;
    const isAuthCall = config?.url?.startsWith("/auth/") ?? false;
    if (error.response?.status === 401 && config && !config._retried && !isAuthCall) {
      config._retried = true;
      const session = await refreshSession();
      if (session) {
        config.headers.set("Authorization", `Bearer ${session.accessToken}`);
        return apiClient.request(config);
      }
      onSessionExpired?.();
    }
    return Promise.reject(toApiError(error));
  },
);

/** Refresh dùng chung một promise cho mọi request đang chờ. */
export function refreshSession(): Promise<AuthResponse | null> {
  if (!refreshPromise) {
    refreshPromise = apiClient
      .post<ApiEnvelope<AuthResponse>>("/auth/refresh")
      .then((response) => {
        const session = response.data.data;
        if (session) {
          tokenStore.set(session.accessToken);
          onSessionRefreshed?.(session);
        }
        return session;
      })
      .catch(() => {
        tokenStore.set(null);
        return null;
      })
      .finally(() => {
        refreshPromise = null;
      });
  }
  return refreshPromise;
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }
  if (axios.isAxiosError(error)) {
    const body = error.response?.data as ApiEnvelope<unknown> | undefined;
    if (error.response && body && Array.isArray(body.errors)) {
      const first = body.errors[0];
      return new ApiError(
        error.response.status,
        first?.code ?? "UNKNOWN",
        body.message ?? first?.message ?? "Đã xảy ra lỗi.",
        body.errors,
        body.traceId,
      );
    }
    if (!error.response) {
      return new ApiError(0, "NETWORK_ERROR", "Không kết nối được máy chủ. Kiểm tra mạng và thử lại.", [], null);
    }
    return new ApiError(error.response.status, "HTTP_" + error.response.status, error.message, [], null);
  }
  return new ApiError(0, "UNKNOWN", error instanceof Error ? error.message : "Đã xảy ra lỗi.", [], null);
}

/** Gọi API và trả về `data` của ApiResponse. */
export async function request<T>(config: AxiosRequestConfig): Promise<T> {
  const response = await apiClient.request<ApiEnvelope<T>>(config);
  return response.data.data as T;
}

export const http = {
  get: <T>(url: string, params?: object) => request<T>({ method: "GET", url, params }),
  post: <T>(url: string, data?: unknown) => request<T>({ method: "POST", url, data }),
  put: <T>(url: string, data?: unknown) => request<T>({ method: "PUT", url, data }),
  patch: <T>(url: string, data?: unknown) => request<T>({ method: "PATCH", url, data }),
  delete: <T>(url: string) => request<T>({ method: "DELETE", url }),
};
