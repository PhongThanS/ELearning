import { useEffect, useMemo, useRef, useState } from "react";
import { Form, Modal } from "react-bootstrap";
import { useNavigate } from "react-router";
import { useQuery } from "@tanstack/react-query";
import { Permissions } from "../constants/permissions";
import { useAuth } from "../features/auth/useAuth";
import { categoriesApi, examsApi, usersApi } from "../services/api";
import { matchClassroom, useAllClassrooms } from "../hooks/useClassrooms";
import { normalizeVietnamese } from "../components/common/SearchableSelect";
import { categoryLabel, orderCategories } from "../utils/categoryTree";

/** Một trang trong menu quản trị, dùng cho nhóm "Trang" của ô tìm nhanh (icon: lớp Bootstrap Icons). */
export interface QuickPage {
  to: string;
  label: string;
  icon: string;
}

interface Hit {
  key: string;
  group: string;
  to: string;
  icon: string;
  title: string;
  hint?: string;
}

const PER_GROUP = 5;

const matches = (keyword: string, ...texts: (string | null | undefined)[]) => {
  const k = normalizeVietnamese(keyword.trim());
  return !k || texts.some((t) => t && normalizeVietnamese(t).includes(k));
};

/** Chờ người dùng ngừng gõ rồi mới gọi API (đề thi, người dùng). */
function useDebounced(value: string, ms: number) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), ms);
    return () => window.clearTimeout(timer);
  }, [value, ms]);
  return debounced;
}

/**
 * Ô tìm nhanh toàn hệ thống (Ctrl+K / ⌘K): gõ vài chữ để nhảy tới trang, lớp học, chuyên đề, đề thi, người dùng.
 * Chỉ tìm trong những gì người dùng có quyền xem; backend vẫn kiểm tra quyền khi mở trang.
 */
export function QuickSearch({ pages }: { pages: QuickPage[] }) {
  const navigate = useNavigate();
  const { hasPermission } = useAuth();
  const [open, setOpen] = useState(false);
  const [keyword, setKeyword] = useState("");
  const [active, setActive] = useState(0);
  const listRef = useRef<HTMLDivElement>(null);
  const debounced = useDebounced(keyword.trim(), 250);
  const remote = open && debounced.length >= 2;

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setOpen(true);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  const classes = useAllClassrooms(open && hasPermission(Permissions.ClassView));
  const categories = useQuery({
    queryKey: ["sidebar-categories"],
    queryFn: () => categoriesApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
    enabled: open && hasPermission(Permissions.CategoryView),
  });
  const exams = useQuery({
    queryKey: ["quick-search", "exams", debounced],
    queryFn: () => examsApi.list({ keyword: debounced, pageSize: PER_GROUP }),
    enabled: remote && hasPermission(Permissions.ExamView),
  });
  const users = useQuery({
    queryKey: ["quick-search", "users", debounced],
    queryFn: () => usersApi.list({ keyword: debounced, pageSize: PER_GROUP }),
    enabled: remote && hasPermission(Permissions.UserView),
  });

  const hits = useMemo<Hit[]>(() => {
    const q = keyword.trim();
    const result: Hit[] = pages
      .filter((p) => matches(q, p.label))
      .slice(0, q ? PER_GROUP : pages.length)
      .map((p) => ({ key: `page:${p.to}`, group: "Trang", to: p.to, icon: p.icon, title: p.label }));
    if (!q) {
      return result;
    }
    for (const c of (classes.data ?? []).filter((c) => matchClassroom(c, q)).slice(0, PER_GROUP)) {
      result.push({ key: `class:${c.id}`, group: "Lớp học", to: `/admin/classes/${c.id}`, icon: "bi-mortarboard", title: c.name, hint: `${c.code} · ${c.studentCount} học viên` });
    }
    for (const c of orderCategories(categories.data?.items ?? []).filter((c) => matches(q, c.code, c.name, c.parentName)).slice(0, PER_GROUP)) {
      result.push({ key: `category:${c.id}`, group: "Chuyên đề", to: `/admin/questions?categoryId=${c.id}`, icon: "bi-folder2", title: categoryLabel(c), hint: c.code });
    }
    if (debounced === q) {
      for (const e of exams.data?.items ?? []) {
        result.push({ key: `exam:${e.id}`, group: "Đề thi", to: `/admin/exams/${e.id}`, icon: "bi-file-earmark-text", title: e.name, hint: e.code });
      }
      for (const u of users.data?.items ?? []) {
        result.push({ key: `user:${u.id}`, group: "Người dùng", to: `/admin/users/${u.id}`, icon: "bi-person", title: u.fullName, hint: `@${u.userName}` });
      }
    }
    return result;
  }, [keyword, debounced, pages, classes.data, categories.data, exams.data, users.data]);

  const loading = remote && (exams.isFetching || users.isFetching || debounced !== keyword.trim());

  useEffect(() => setActive(0), [keyword]);
  useEffect(() => {
    listRef.current?.querySelector<HTMLElement>(`[data-index="${active}"]`)?.scrollIntoView({ block: "nearest" });
  }, [active]);

  const close = () => {
    setOpen(false);
    setKeyword("");
  };
  const go = (hit: Hit | undefined) => {
    if (!hit) return;
    close();
    navigate(hit.to);
  };

  return (
    <>
      <button type="button" className="quick-search-trigger d-none d-md-inline-flex" onClick={() => setOpen(true)} aria-label="Tìm nhanh (Ctrl+K)">
        <i className="bi bi-search" aria-hidden="true" />
        <span className="flex-grow-1 text-start">Tìm nhanh…</span>
        <kbd>Ctrl K</kbd>
      </button>
      <button type="button" className="quick-search-trigger-icon d-md-none" onClick={() => setOpen(true)} aria-label="Tìm nhanh">
        <i className="bi bi-search" aria-hidden="true" />
      </button>

      <Modal show={open} onHide={close} dialogClassName="quick-search-dialog" aria-label="Tìm nhanh">
        <div className="quick-search-input">
          <i className="bi bi-search text-secondary" aria-hidden="true" />
          <Form.Control
            autoFocus
            plaintext
            value={keyword}
            placeholder="Tìm trang, lớp học, chuyên đề, đề thi, người dùng…"
            aria-label="Từ khóa tìm nhanh"
            aria-controls="quick-search-results"
            onChange={(e) => setKeyword(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "ArrowDown") {
                e.preventDefault();
                setActive((i) => Math.min(i + 1, hits.length - 1));
              } else if (e.key === "ArrowUp") {
                e.preventDefault();
                setActive((i) => Math.max(i - 1, 0));
              } else if (e.key === "Enter") {
                e.preventDefault();
                go(hits[active]);
              }
            }}
          />
          <kbd>Esc</kbd>
        </div>
        <div className="quick-search-results" id="quick-search-results" role="listbox" ref={listRef}>
          {hits.map((hit, i) => (
            <div key={hit.key}>
              {(i === 0 || hits[i - 1]!.group !== hit.group) && <div className="quick-search-group">{hit.group}</div>}
              <button
                type="button"
                role="option"
                aria-selected={i === active}
                data-index={i}
                className={`quick-search-item ${i === active ? "active" : ""}`}
                onMouseEnter={() => setActive(i)}
                onClick={() => go(hit)}
              >
                <span className="quick-search-icon" aria-hidden="true">
                  <i className={`bi ${hit.icon}`} />
                </span>
                <span className="flex-grow-1 min-w-0 text-truncate">{hit.title}</span>
                {hit.hint && <span className="quick-search-hint text-truncate">{hit.hint}</span>}
              </button>
            </div>
          ))}
          {loading && <div className="quick-search-empty">Đang tìm đề thi, người dùng…</div>}
          {!loading && keyword.trim() && hits.length === 0 && <div className="quick-search-empty">Không tìm thấy kết quả cho “{keyword.trim()}”.</div>}
        </div>
        <div className="quick-search-footer">
          <span>
            <kbd>↑</kbd> <kbd>↓</kbd> chọn
          </span>
          <span>
            <kbd>Enter</kbd> mở
          </span>
          <span>
            <kbd>Esc</kbd> đóng
          </span>
        </div>
      </Modal>
    </>
  );
}
