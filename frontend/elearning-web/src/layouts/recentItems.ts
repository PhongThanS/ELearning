import { useSyncExternalStore } from "react";

/** Lớp học / chuyên đề mở gần đây, hiện trong menu bên. Chỉ là tiện ích trên trình duyệt này (localStorage). */
export type RecentKind = "class" | "category";

export interface RecentItem {
  id: string;
  name: string;
  code: string;
}

export const RECENT_LIMIT = 5;
const EVENT = "sclass-recent-changed";
const key = (kind: RecentKind) => `sclass.recent.${kind}`;
const EMPTY: RecentItem[] = [];
const cache = new Map<RecentKind, { raw: string | null; items: RecentItem[] }>();

function read(kind: RecentKind): RecentItem[] {
  let raw: string | null = null;
  try {
    raw = window.localStorage.getItem(key(kind));
  } catch {
    raw = null;
  }
  const cached = cache.get(kind);
  if (cached && cached.raw === raw) {
    return cached.items;
  }
  let items: RecentItem[] = [];
  try {
    const parsed: unknown = raw ? JSON.parse(raw) : [];
    if (Array.isArray(parsed)) {
      items = parsed
        .filter((x): x is RecentItem => typeof x?.id === "string" && typeof x?.name === "string" && typeof x?.code === "string")
        .slice(0, RECENT_LIMIT);
    }
  } catch {
    items = [];
  }
  cache.set(kind, { raw, items });
  return items;
}

/** Đưa mục lên đầu danh sách gần đây (bỏ trùng, giữ tối đa RECENT_LIMIT mục). */
export function rememberRecent(kind: RecentKind, item: RecentItem): void {
  const current = read(kind);
  if (current[0]?.id === item.id && current[0].name === item.name) {
    return;
  }
  const next = [item, ...current.filter((x) => x.id !== item.id)].slice(0, RECENT_LIMIT);
  try {
    window.localStorage.setItem(key(kind), JSON.stringify(next));
  } catch {
    return;
  }
  window.dispatchEvent(new Event(EVENT));
}

function subscribe(onChange: () => void): () => void {
  window.addEventListener(EVENT, onChange);
  window.addEventListener("storage", onChange);
  return () => {
    window.removeEventListener(EVENT, onChange);
    window.removeEventListener("storage", onChange);
  };
}

export function useRecent(kind: RecentKind): RecentItem[] {
  return useSyncExternalStore(
    subscribe,
    () => read(kind),
    () => EMPTY,
  );
}
