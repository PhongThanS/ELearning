import { useCallback, useEffect, useRef, useState, type Dispatch } from "react";
import { studentApi } from "../../services/api";
import { ApiError } from "../../services/apiClient";
import type { AttemptEventType } from "../../types/api";
import { pendingItems, remainingMs, saveBackup, type PlayerAction, type PlayerState } from "./playerState";

const DEBOUNCE_MS = 500;
const BACKOFF_MS = [1000, 2000, 4000, 8000, 15000];
const TERMINAL_CODES = new Set(["ATTEMPT_EXPIRED", "ATTEMPT_NOT_IN_PROGRESS", "ATTEMPT_NOT_FOUND"]);

export type SaveStatus = "saved" | "saving" | "unsaved";

/** Giữ giá trị mới nhất trong ref, cập nhật sau render (không đọc/ghi ref trong lúc render). */
function useLatest<T>(value: T) {
  const ref = useRef(value);
  useEffect(() => {
    ref.current = value;
  });
  return ref;
}

/**
 * Autosave (D-18, docs/06-frontend.md mục 4.4):
 * - debounce 500 ms sau lần sửa cuối, gom mọi câu chưa lưu thành một lô;
 * - mỗi thời điểm chỉ một request lưu; lỗi mạng / 5xx thì thử lại theo backoff;
 * - lượt thi đã kết thúc / hết giờ thì dừng và báo onFinished.
 * Trả về `flush` để lưu ngay (trước khi nộp bài).
 */
export function useAutosave(state: PlayerState, dispatch: Dispatch<PlayerAction>, onFinished: (code: string) => void) {
  const stateRef = useLatest(state);
  const onFinishedRef = useLatest(onFinished);
  const inFlight = useRef<Promise<void> | null>(null);
  const failures = useRef(0);
  const stopped = useRef(false);
  const [saving, setSaving] = useState(false);

  const hasPending = pendingItems(state, 1).length > 0;
  // Trạng thái hiển thị được suy ra, không đồng bộ bằng effect
  const status: SaveStatus = !hasPending ? "saved" : saving ? "saving" : "unsaved";

  const run = useCallback((): Promise<void> => {
    if (inFlight.current) {
      return inFlight.current;
    }
    const current = stateRef.current;
    const items = pendingItems(current);
    if (stopped.current || items.length === 0) {
      return Promise.resolve();
    }

    setSaving(true);
    inFlight.current = studentApi
      .saveAnswers(current.attemptId, items)
      .then((response) => {
        failures.current = 0;
        dispatch({ type: "synced", response, receivedAt: Date.now() });
      })
      .catch((error: unknown) => {
        if (error instanceof ApiError && TERMINAL_CODES.has(error.code)) {
          stopped.current = true;
          onFinishedRef.current(error.code);
          return;
        }
        const permanent = error instanceof ApiError && error.status >= 400 && error.status < 500 && error.status !== 408 && error.status !== 429;
        // Lỗi dữ liệu (ví dụ số sai định dạng) không thử lại vô hạn; UI báo ở ô nhập.
        failures.current = permanent ? BACKOFF_MS.length : failures.current + 1;
        if (error instanceof ApiError && error.isNetworkError) {
          dispatch({ type: "online", online: false });
        }
        throw error;
      })
      .finally(() => {
        inFlight.current = null;
        setSaving(false);
      });
    return inFlight.current;
  }, [dispatch, stateRef, onFinishedRef]);

  // Mỗi khi state đổi: ghi backup ngay, rồi debounce gửi lên server.
  useEffect(() => {
    saveBackup(state);
    if (!hasPending || stopped.current) {
      return;
    }
    const delay = failures.current === 0 ? DEBOUNCE_MS : BACKOFF_MS[Math.min(failures.current - 1, BACKOFF_MS.length - 1)]!;
    const timer = window.setTimeout(() => void run().catch(() => undefined), delay);
    return () => window.clearTimeout(timer);
  }, [state, hasPending, run]);

  // Còn câu chưa lưu: thử lại định kỳ và ngay khi có mạng trở lại
  useEffect(() => {
    if (!hasPending) {
      return;
    }
    const retry = window.setInterval(() => {
      if (!inFlight.current && !stopped.current && failures.current > 0 && failures.current <= BACKOFF_MS.length) {
        void run().catch(() => undefined);
      }
    }, 5000);
    const onOnline = () => {
      dispatch({ type: "online", online: true });
      failures.current = Math.min(failures.current, 1);
      void run().catch(() => undefined);
    };
    const onOffline = () => dispatch({ type: "online", online: false });
    window.addEventListener("online", onOnline);
    window.addEventListener("offline", onOffline);
    return () => {
      window.clearInterval(retry);
      window.removeEventListener("online", onOnline);
      window.removeEventListener("offline", onOffline);
    };
  }, [hasPending, run, dispatch]);

  /** Lưu ngay mọi câu còn chờ (gọi trước khi nộp). Trả về false nếu vẫn còn câu chưa lưu. */
  const flush = useCallback(async (): Promise<boolean> => {
    for (let i = 0; i < 3; i++) {
      await inFlight.current?.catch(() => undefined);
      if (pendingItems(stateRef.current, 1).length === 0) {
        return true;
      }
      await run().catch(() => undefined);
    }
    return pendingItems(stateRef.current, 1).length === 0;
  }, [run, stateRef]);

  const stop = useCallback(() => {
    stopped.current = true;
  }, []);

  return { status, flush, stop };
}

/** Đồng hồ: render lại mỗi giây, tính từ expiredAt + độ lệch với server; gọi onTimeUp đúng một lần. */
export function useCountdown(state: PlayerState, onTimeUp: () => void): number {
  const [now, setNow] = useState(() => Date.now());
  const fired = useRef(false);
  const onTimeUpRef = useLatest(onTimeUp);

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);

  const remaining = remainingMs(state, now);
  useEffect(() => {
    if (remaining <= 0 && !fired.current) {
      fired.current = true;
      onTimeUpRef.current();
    }
  }, [remaining, onTimeUpRef]);

  return remaining;
}

/**
 * Ghi nhận sự kiện rời trang / mất mạng / dán (docs/06-frontend.md mục 4.7). Không chặn thao tác của học viên.
 * Gửi theo lô mỗi 10 giây hoặc khi đủ 20 sự kiện. Phát hiện nhiều tab qua BroadcastChannel.
 */
export function useAttemptEvents(attemptId: string, onMultiTab: () => void) {
  const queue = useRef<{ type: AttemptEventType; clientTime: string; detail?: string }[]>([]);
  const onMultiTabRef = useLatest(onMultiTab);

  useEffect(() => {
    let lastType: AttemptEventType | null = null;
    let lastAt = 0;
    const flush = async () => {
      const batch = queue.current.splice(0, 50);
      if (batch.length > 0) {
        await studentApi.events(attemptId, batch).catch(() => undefined);
      }
    };
    const push = (type: AttemptEventType, detail?: string) => {
      const at = Date.now();
      if (type === lastType && at - lastAt < 1000) {
        return; // gộp sự kiện lặp trong 1 giây
      }
      lastType = type;
      lastAt = at;
      queue.current.push({ type, clientTime: new Date(at).toISOString(), detail });
      if (queue.current.length >= 20) {
        void flush();
      }
    };

    const onVisibility = () => push(document.hidden ? "VISIBILITY_HIDDEN" : "VISIBILITY_VISIBLE");
    const onBlur = () => push("WINDOW_BLUR");
    const onFocus = () => push("WINDOW_FOCUS");
    const onOnline = () => push("ONLINE");
    const onOffline = () => push("OFFLINE");
    const onFullscreen = () => {
      if (!document.fullscreenElement) {
        push("FULLSCREEN_EXIT");
      }
    };
    document.addEventListener("visibilitychange", onVisibility);
    window.addEventListener("blur", onBlur);
    window.addEventListener("focus", onFocus);
    window.addEventListener("online", onOnline);
    window.addEventListener("offline", onOffline);
    document.addEventListener("fullscreenchange", onFullscreen);
    const timer = window.setInterval(() => void flush(), 10_000);

    // Nhiều tab: tab mới hỏi, tab cũ trả lời → cả hai đều cảnh báo
    const channel = typeof BroadcastChannel === "undefined" ? null : new BroadcastChannel(`attempt_${attemptId}`);
    if (channel) {
      channel.onmessage = (event: MessageEvent<string>) => {
        if (event.data === "hello") {
          channel.postMessage("here");
        }
        onMultiTabRef.current();
        push("MULTI_TAB_DETECTED");
      };
      channel.postMessage("hello");
    }

    return () => {
      document.removeEventListener("visibilitychange", onVisibility);
      window.removeEventListener("blur", onBlur);
      window.removeEventListener("focus", onFocus);
      window.removeEventListener("online", onOnline);
      window.removeEventListener("offline", onOffline);
      document.removeEventListener("fullscreenchange", onFullscreen);
      window.clearInterval(timer);
      channel?.close();
      void flush();
    };
  }, [attemptId, onMultiTabRef]);

  const recordPaste = useCallback(() => {
    queue.current.push({ type: "PASTE", clientTime: new Date().toISOString() });
  }, []);

  return { recordPaste };
}
