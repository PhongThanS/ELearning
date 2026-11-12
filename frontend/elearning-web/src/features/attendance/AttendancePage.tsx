import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Badge, Button, ButtonGroup, Card, Col, Form, Modal, Row, Spinner, Table } from "react-bootstrap";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router";
import { attendanceApi } from "../../services/api";
import { MonthlyHistoryView } from "./MonthlyHistoryView";
import type { AttendanceEntry, AttendanceSession, AttendanceStudent } from "../../types/api";
import { Permissions } from "../../constants/permissions";
import { useAuth } from "../auth/useAuth";
import { useToast } from "../../components/common/toast";
import { ConfirmDialog, Empty, ErrorAlert, Loading } from "../../components/common/Feedback";
import { PageHeader } from "../../components/common/DataTable";
import { SearchableSelect, normalizeVietnamese } from "../../components/common/SearchableSelect";
import { formatDateOnly, formatDateTime } from "../../utils/format";
import {
  academicChips,
  appendNote,
  buildMonthlyReport,
  buildZaloReport,
  computeStats,
  formatScoreValue,
  homeworkOptions,
  homeworkSummary,
  matchesFilter,
  parseScore,
  phoneDigits,
  scoreVariant,
  statusChips,
  statusOptions,
  stepScore,
  vnDate,
  type AttendanceFilter,
} from "./attendanceLogic";

const AUTOSAVE_DELAY_MS = 800;

/** Sao chép văn bản (báo cáo Zalo, tổng kết tháng). */
function useCopy() {
  const toast = useToast();
  return async (text: string, done: string) => {
    try {
      await navigator.clipboard.writeText(text);
      toast.success(done);
    } catch {
      toast.info("Trình duyệt chặn sao chép tự động — hãy bôi đen và nhấn Ctrl+C.");
    }
  };
}

function ContactLinks({ phone }: { phone: string | null }) {
  const digits = phoneDigits(phone);
  if (!digits) {
    return null;
  }
  return (
    <>
      <a href={`tel:${digits}`} className="ms-2 text-success" title={`Gọi ${phone}`} aria-label={`Gọi ${phone}`}>
        <i className="bi bi-telephone-fill" />
      </a>
      <a href={`https://zalo.me/${digits}`} target="_blank" rel="noreferrer" className="ms-2 text-primary" title="Nhắn Zalo">
        <i className="bi bi-chat-dots-fill" />
      </a>
    </>
  );
}

/** Trang điểm danh + BTVN của giáo viên (D-31, docs/06-frontend.md mục 4.9). */
export function AttendancePage() {
  const [params, setParams] = useSearchParams();
  const today = vnDate();
  const date = params.get("date") && params.get("date")! <= today ? params.get("date")! : today;
  const classes = useQuery({ queryKey: ["attendance-classes"], queryFn: attendanceApi.classes, staleTime: 60_000 });
  const classId = params.get("class") ?? classes.data?.[0]?.id ?? null;

  const session = useQuery({
    queryKey: ["attendance-session", classId, date],
    queryFn: () => attendanceApi.session(classId!, date),
    enabled: classId !== null,
    // Dữ liệu đang sửa nằm ở trình soạn; không tải lại ngầm để khỏi ghi đè thao tác của giáo viên
    staleTime: Infinity,
    gcTime: 0,
    refetchOnWindowFocus: false,
  });

  const go = (next: { class?: string; date?: string }) => {
    const merged = new URLSearchParams(params);
    if (next.class) merged.set("class", next.class);
    if (next.date) merged.set("date", next.date);
    setParams(merged, { replace: true });
  };

  if (classes.isLoading) {
    return <Loading />;
  }
  if (classes.error) {
    return <ErrorAlert error={classes.error} onRetry={() => void classes.refetch()} />;
  }
  if (!classes.data?.length) {
    return (
      <>
        <PageHeader title="Điểm danh & BTVN" />
        <Empty text="Bạn chưa chủ nhiệm lớp nào đang hoạt động. Liên hệ quản trị viên để được phân công giáo viên chủ nhiệm." />
      </>
    );
  }

  return (
    <>
      <PageHeader title="Điểm danh & BTVN">
        <div className="text-secondary small">Điểm danh, chấm BTVN từng buổi — tự động lưu khi thay đổi.</div>
      </PageHeader>

      <Card className="mb-3 filter-bar">
        <Card.Body className="d-flex flex-wrap gap-3 align-items-end">
          <Form.Group style={{ minWidth: 260, flex: "1 1 260px" }}>
            <Form.Label className="small fw-semibold mb-1">Lớp học</Form.Label>
            <SearchableSelect
              value={classId}
              clearable={false}
              onChange={(value) => value && go({ class: value })}
              options={classes.data.map((c) => ({
                value: c.id,
                label: c.name,
                code: c.code,
                sublabel: [c.schoolYear, `${c.studentCount} HS`, c.homeroomTeacherName && `GVCN: ${c.homeroomTeacherName}`].filter(Boolean).join(" · "),
              }))}
            />
          </Form.Group>
          <Form.Group>
            <Form.Label className="small fw-semibold mb-1" htmlFor="attendance-date">
              Ngày học
            </Form.Label>
            <div className="d-flex gap-1">
              <Button variant="outline-secondary" onClick={() => go({ date: vnDate(-1, date) })} aria-label="Ngày trước">
                <i className="bi bi-chevron-left" />
              </Button>
              <Form.Control
                id="attendance-date"
                type="date"
                value={date}
                max={today}
                onChange={(e) => e.target.value && go({ date: e.target.value })}
                style={{ width: 170 }}
              />
              <Button variant="outline-secondary" disabled={date >= today} onClick={() => go({ date: vnDate(1, date) })} aria-label="Ngày sau">
                <i className="bi bi-chevron-right" />
              </Button>
              {date !== today && (
                <Button variant="link" onClick={() => go({ date: today })}>
                  Hôm nay
                </Button>
              )}
            </div>
          </Form.Group>
        </Card.Body>
      </Card>

      {session.isLoading && <Loading />}
      {session.error && <ErrorAlert error={session.error} onRetry={() => void session.refetch()} />}
      {session.data && classId && <SessionEditor key={`${classId}|${date}|${session.dataUpdatedAt}`} classId={classId} date={date} initial={session.data} />}
    </>
  );
}

type SaveState = { kind: "idle" } | { kind: "pending" } | { kind: "saving" } | { kind: "saved"; at: string } | { kind: "error" };

type SortMode = "order" | "scoreDesc" | "scoreAsc";

function SessionEditor({ classId, date, initial }: { classId: string; date: string; initial: AttendanceSession }) {
  const toast = useToast();
  const copy = useCopy();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();

  const toEntries = (s: AttendanceSession) =>
    Object.fromEntries(
      s.students.map((st) => [st.studentId, { status: st.status, homeworkStatus: st.homeworkStatus, homeworkScore: st.homeworkScore, note: st.note }]),
    ) as Record<string, AttendanceEntry>;

  const [students, setStudents] = useState<AttendanceStudent[]>(initial.students);
  const [rows, setRows] = useState<Record<string, AttendanceEntry>>(() => toEntries(initial));
  const [notes, setNotes] = useState({ topic: initial.topic ?? "", nextHomework: initial.nextHomework ?? "" });
  const [saveState, setSaveState] = useState<SaveState>(initial.updatedAt ? { kind: "saved", at: initial.updatedAt } : { kind: "idle" });
  const [scoreDrafts, setScoreDrafts] = useState<Record<string, string>>({});
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<AttendanceFilter>("ALL");
  const [sort, setSort] = useState<SortMode>("order");
  const [showZalo, setShowZalo] = useState(false);
  const [showShortcuts, setShowShortcuts] = useState(false);
  const [confirmReset, setConfirmReset] = useState(false);
  const [resetting, setResetting] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [contactFor, setContactFor] = useState<AttendanceStudent | null>(null);
  const [historyFor, setHistoryFor] = useState<AttendanceStudent | null>(null);
  const searchRef = useRef<HTMLInputElement>(null);

  // ----- Tự động lưu: gom thay đổi, gửi sau 0,8 giây; khi rời buổi (đổi lớp / ngày) gửi ngay -----
  const rowsRef = useRef(rows);
  const notesRef = useRef(notes);
  const pending = useRef({ ids: new Set<string>(), notes: false });
  const timer = useRef<number | undefined>(undefined);
  const saving = useRef(false);
  const again = useRef(false);
  // useToast trả object mới mỗi lần render → giữ qua ref để flush ổn định (cleanup chỉ chạy khi rời buổi)
  const toastRef = useRef(toast);
  useEffect(() => {
    toastRef.current = toast;
  });

  const flush = useCallback(async () => {
    window.clearTimeout(timer.current);
    if (saving.current) {
      again.current = true;
      return;
    }
    const { ids, notes: notesChanged } = pending.current;
    if (ids.size === 0 && !notesChanged) {
      return;
    }
    pending.current = { ids: new Set(), notes: false };
    saving.current = true;
    setSaveState({ kind: "saving" });
    try {
      const result = await attendanceApi.save(classId, date, {
        topic: notesRef.current.topic.trim() || null,
        nextHomework: notesRef.current.nextHomework.trim() || null,
        records: [...ids].map((id) => ({ studentId: id, ...rowsRef.current[id]! })),
      });
      setSaveState({ kind: "saved", at: result.updatedAt });
      void queryClient.invalidateQueries({ queryKey: ["attendance-history"] });
    } catch (error) {
      ids.forEach((id) => pending.current.ids.add(id));
      pending.current.notes ||= notesChanged;
      setSaveState({ kind: "error" });
      toastRef.current.error(error);
    } finally {
      saving.current = false;
      if (again.current) {
        again.current = false;
        void flush();
      }
    }
  }, [classId, date, queryClient]);

  const schedule = useCallback(() => {
    setSaveState({ kind: "pending" });
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => void flush(), AUTOSAVE_DELAY_MS);
  }, [flush]);

  useEffect(() => {
    const warn = (e: BeforeUnloadEvent) => {
      if (pending.current.ids.size > 0 || pending.current.notes || saving.current) {
        e.preventDefault();
      }
    };
    window.addEventListener("beforeunload", warn);
    return () => {
      window.removeEventListener("beforeunload", warn);
      void flush();
    };
  }, [flush]);

  const update = useCallback(
    (changes: Record<string, Partial<AttendanceEntry>>) => {
      const next = { ...rowsRef.current };
      for (const [id, patch] of Object.entries(changes)) {
        next[id] = { ...next[id]!, ...patch };
        pending.current.ids.add(id);
      }
      rowsRef.current = next;
      setRows(next);
      schedule();
    },
    [schedule],
  );

  const updateNotes = (patch: Partial<typeof notes>) => {
    const next = { ...notesRef.current, ...patch };
    notesRef.current = next;
    setNotes(next);
    pending.current.notes = true;
    schedule();
  };

  // Phím "/" để tìm học sinh
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const target = e.target as HTMLElement;
      if (e.key === "/" && !["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName)) {
        e.preventDefault();
        searchRef.current?.focus();
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, []);

  const entries = students.map((s) => rows[s.studentId]!);
  const stats = computeStats(entries);
  const homework = homeworkSummary(entries);
  const order = useMemo(() => new Map(students.map((s, i) => [s.studentId, i + 1])), [students]);

  const visible = useMemo(() => {
    const keyword = normalizeVietnamese(search);
    const list = students.filter(
      (s) =>
        matchesFilter(rows[s.studentId]!, filter) &&
        (!keyword || normalizeVietnamese(`${s.fullName} ${s.userName} ${s.parentName ?? ""} ${s.parentPhone ?? ""}`).includes(keyword)),
    );
    if (sort !== "order") {
      const sign = sort === "scoreDesc" ? -1 : 1;
      list.sort((a, b) => {
        const sa = rows[a.studentId]!.homeworkScore;
        const sb = rows[b.studentId]!.homeworkScore;
        if (sa === null || sb === null) return sa === sb ? 0 : sa === null ? 1 : -1;
        return sign * (sa - sb);
      });
    }
    return list;
  }, [students, rows, filter, search, sort]);

  const setScore = (id: string, score: number | null) => {
    const current = rowsRef.current[id]!;
    // Nhập điểm khi chưa chọn mức BTVN → gợi ý mức theo điểm (giáo viên vẫn đổi được)
    const homeworkStatus =
      current.homeworkStatus ?? (score === null ? null : score >= 8.5 ? "EXCELLENT" : score >= 5 ? "DONE" : "PARTIAL");
    update({ [id]: { homeworkScore: score, homeworkStatus } });
  };

  const commitDraft = (id: string) => {
    const draft = scoreDrafts[id];
    if (draft === undefined) return;
    setScoreDrafts((drafts) => {
      const rest = { ...drafts };
      delete rest[id];
      return rest;
    });
    const score = parseScore(draft);
    if (score !== rowsRef.current[id]!.homeworkScore) {
      setScore(id, score);
    }
  };

  const onScoreKey = (e: React.KeyboardEvent<HTMLInputElement>, id: string) => {
    if (e.key !== "Enter" && e.key !== "ArrowDown" && e.key !== "ArrowUp") return;
    e.preventDefault();
    commitDraft(id);
    const inputs = Array.from(document.querySelectorAll<HTMLInputElement>("input[data-score-input]"));
    const index = inputs.findIndex((el) => el.dataset.scoreInput === id);
    const target = inputs[e.key === "ArrowUp" ? index - 1 : index + 1];
    target?.focus();
    target?.select();
  };

  const markAll = (field: "status" | "homeworkStatus") => {
    const changes: Record<string, Partial<AttendanceEntry>> = {};
    for (const s of students) {
      if (s.inClass && rowsRef.current[s.studentId]![field] === null) {
        changes[s.studentId] = field === "status" ? { status: "PRESENT" } : { homeworkStatus: "DONE" };
      }
    }
    if (Object.keys(changes).length === 0) {
      toast.info(field === "status" ? "Mọi học sinh đã được điểm danh." : "Mọi học sinh đã có mức BTVN.");
      return;
    }
    update(changes);
    toast.success(
      field === "status"
        ? `Đã đánh dấu có mặt ${Object.keys(changes).length} học sinh chưa điểm danh.`
        : `Đã đánh dấu Đạt BTVN ${Object.keys(changes).length} học sinh chưa chấm.`,
    );
  };

  const reset = async () => {
    setResetting(true);
    try {
      window.clearTimeout(timer.current);
      pending.current = { ids: new Set(), notes: false };
      const fresh = await attendanceApi.reset(classId, date);
      rowsRef.current = toEntries(fresh);
      notesRef.current = { topic: "", nextHomework: "" };
      setRows(rowsRef.current);
      setNotes(notesRef.current);
      setStudents(fresh.students);
      setScoreDrafts({});
      setSaveState(fresh.updatedAt ? { kind: "saved", at: fresh.updatedAt } : { kind: "idle" });
      toast.success("Đã làm mới buổi học: mọi học sinh về chưa điểm danh.");
      setConfirmReset(false);
    } catch (error) {
      toast.error(error);
    } finally {
      setResetting(false);
    }
  };

  const exportExcel = async () => {
    setExporting(true);
    try {
      await flush();
      await attendanceApi.export(classId, date);
    } catch (error) {
      toast.error(error);
    } finally {
      setExporting(false);
    }
  };

  const statCards: { filter: AttendanceFilter; label: string; value: string; sub?: string; color: string }[] = [
    { filter: "ALL", label: "Sĩ số", value: `${stats.attended}/${stats.total}`, sub: `Tham gia ${stats.rate}%`, color: "secondary" },
    { filter: "PRESENT", label: "Có mặt", value: String(stats.present), color: "success" },
    { filter: "LATE", label: "Đi muộn", value: String(stats.late), color: "warning" },
    { filter: "EXCUSED", label: "Vắng có phép", value: String(stats.excused), color: "info" },
    { filter: "UNEXCUSED", label: "Vắng không phép", value: String(stats.unexcused), color: "danger" },
  ];

  const filterPills: { value: AttendanceFilter; label: string }[] = [
    { value: "ALL", label: `Tất cả (${stats.total})` },
    { value: "UNMARKED", label: `Chưa điểm danh (${stats.unmarked})` },
    ...statusOptions.map((o) => ({ value: o.value as AttendanceFilter, label: o.label })),
    { value: "HW_ISSUE", label: "BTVN cần lưu ý" },
  ];

  return (
    <>
      {/* Trạng thái lưu */}
      <div className="d-flex justify-content-between align-items-center flex-wrap gap-2 mb-2">
        <div className="fw-semibold">
          {initial.classroomName} · {formatDateOnly(date)}
        </div>
        <SaveIndicator state={saveState} onRetry={() => void flush()} />
      </div>

      {/* Thao tác nhanh */}
      <div className="d-flex flex-wrap gap-2 mb-3">
        <Button size="sm" variant="success" onClick={() => markAll("status")}>
          <i className="bi bi-check2-all me-1" />
          Tất cả có mặt
        </Button>
        <Button size="sm" variant="primary" onClick={() => markAll("homeworkStatus")}>
          <i className="bi bi-journal-check me-1" />
          100% Đạt BTVN
        </Button>
        {hasPermission(Permissions.ClassManage) && (
          <Link className="btn btn-sm btn-outline-secondary" to={`/admin/classes/${classId}`}>
            <i className="bi bi-person-plus me-1" />
            Thêm học sinh
          </Link>
        )}
        <Button size="sm" variant="outline-danger" onClick={() => setConfirmReset(true)}>
          <i className="bi bi-arrow-counterclockwise me-1" />
          Làm mới buổi học
        </Button>
        <Button size="sm" variant="outline-secondary" onClick={() => setShowShortcuts(true)}>
          <i className="bi bi-keyboard me-1" />
          Phím tắt
        </Button>
        <div className="ms-auto d-flex flex-wrap gap-2">
          <Button size="sm" variant="outline-success" disabled={exporting} onClick={() => void exportExcel()}>
            {exporting ? <Spinner size="sm" className="me-1" /> : <i className="bi bi-file-earmark-excel me-1" />}
            Xuất Excel
          </Button>
          <Button size="sm" variant="info" className="text-white" onClick={() => setShowZalo(true)}>
            <i className="bi bi-send me-1" />
            Báo cáo Zalo PH
          </Button>
        </div>
      </div>

      {/* Thẻ thống kê (bấm để lọc) */}
      <Row xs={2} md={3} xl={5} className="g-2 mb-3">
        {statCards.map((c) => (
          <Col key={c.filter}>
            <Card
              role="button"
              tabIndex={0}
              onClick={() => setFilter(c.filter)}
              onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && setFilter(c.filter)}
              className={`h-100 attendance-stat border-${c.color} ${filter === c.filter ? `bg-${c.color}-subtle is-active` : ""}`}
              style={{ borderLeftWidth: 4 }}
            >
              <Card.Body className="py-2">
                <div className={`small text-${c.color === "secondary" ? "secondary" : c.color}-emphasis fw-semibold`}>{c.label}</div>
                <div className="fs-4 fw-bold">{c.value}</div>
                {c.sub && <div className="small text-secondary">{c.sub}</div>}
              </Card.Body>
            </Card>
          </Col>
        ))}
      </Row>

      {/* Chủ đề + BTVN buổi sau */}
      <Row className="g-2 mb-3">
        <Col md={6}>
          <Form.Group controlId="attendance-topic">
            <Form.Label className="small fw-semibold mb-1">Nội dung buổi học</Form.Label>
            <Form.Control
              size="sm"
              value={notes.topic}
              maxLength={500}
              placeholder="VD: Khảo sát hàm số & cực trị"
              onChange={(e) => updateNotes({ topic: e.target.value })}
            />
          </Form.Group>
        </Col>
        <Col md={6}>
          <Form.Group controlId="attendance-next-homework">
            <Form.Label className="small fw-semibold mb-1">BTVN giao cho buổi sau</Form.Label>
            <Form.Control
              size="sm"
              value={notes.nextHomework}
              maxLength={1000}
              placeholder="VD: Hoàn thành phiếu bài tập số 05"
              onChange={(e) => updateNotes({ nextHomework: e.target.value })}
            />
          </Form.Group>
        </Col>
      </Row>

      {/* Tìm, lọc, sắp xếp */}
      <Card className="data-card">
        <Card.Header className="data-card-header d-flex flex-wrap gap-2 align-items-center">
          <Form.Control
            ref={searchRef}
            size="sm"
            type="search"
            placeholder="Tìm học sinh, phụ huynh, SĐT… (phím /)"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            style={{ maxWidth: 300 }}
            aria-label="Tìm học sinh"
          />
          <div className="d-flex flex-wrap gap-1">
            {filterPills.map((p) => (
              <Button
                key={p.value}
                size="sm"
                className="rounded-pill filter-pill"
                variant={filter === p.value ? "dark" : "outline-secondary"}
                onClick={() => setFilter(p.value)}
              >
                {p.label}
              </Button>
            ))}
          </div>
          <Form.Select size="sm" value={sort} onChange={(e) => setSort(e.target.value as SortMode)} style={{ width: "auto" }} className="ms-auto" aria-label="Sắp xếp">
            <option value="order">Theo STT</option>
            <option value="scoreDesc">Điểm BTVN cao → thấp</option>
            <option value="scoreAsc">Điểm BTVN thấp → cao</option>
          </Form.Select>
        </Card.Header>
        <Card.Body className="py-2 small border-bottom d-flex flex-wrap gap-2 align-items-center">
          <span className="fw-semibold">BTVN:</span>
          <Badge bg={scoreVariant(homework.average)}>TB lớp {homework.average === null ? "—" : formatScoreValue(homework.average)}</Badge>
          <Badge bg="success-subtle" text="success-emphasis">Xuất sắc {homework.excellent}</Badge>
          <Badge bg="primary-subtle" text="primary-emphasis">Đạt {homework.done}</Badge>
          <Badge bg="warning-subtle" text="warning-emphasis">Thiếu {homework.partial + homework.missing}</Badge>
          <Badge bg="secondary-subtle" text="secondary-emphasis">Chưa chấm {homework.unrated}</Badge>
        </Card.Body>

        {visible.length === 0 ? (
          <Card.Body>
            <Empty text={students.length === 0 ? "Lớp chưa có học sinh." : "Không có học sinh khớp bộ lọc."} />
          </Card.Body>
        ) : (
          <Table responsive hover className="mb-0 align-middle attendance-table">
            <thead className="table-light">
              <tr>
                <th className="text-center" style={{ width: 48 }}>
                  STT
                </th>
                <th style={{ minWidth: 220 }}>Học sinh</th>
                <th style={{ minWidth: 300 }}>Điểm danh</th>
                <th style={{ minWidth: 150 }}>BTVN</th>
                <th style={{ minWidth: 150 }}>Điểm</th>
                <th style={{ minWidth: 280 }}>Nhận xét</th>
              </tr>
            </thead>
            <tbody>
              {visible.map((s) => {
                const r = rows[s.studentId]!;
                const draft = scoreDrafts[s.studentId];
                return (
                  <tr key={s.studentId} className={r.status === null ? "" : `table-row-${r.status.toLowerCase()}`}>
                    <td className="text-center text-secondary">{order.get(s.studentId)}</td>
                    <td>
                      <div className="fw-semibold">
                        {s.fullName}
                        {!s.inClass && (
                          <Badge bg="secondary" className="ms-2">
                            Đã rời lớp
                          </Badge>
                        )}
                      </div>
                      <div className="small text-secondary">
                        {s.parentName || s.parentPhone ? (
                          <>
                            PH: {s.parentName ?? "—"}
                            {s.parentPhone && <> · {s.parentPhone}</>}
                            <ContactLinks phone={s.parentPhone} />
                          </>
                        ) : (
                          <span className="fst-italic">Chưa có liên hệ phụ huynh</span>
                        )}
                        {s.inClass && (
                          <Button variant="link" size="sm" className="p-0 ms-2 align-baseline" onClick={() => setContactFor(s)} title="Sửa liên hệ phụ huynh">
                            <i className="bi bi-pencil-square" />
                          </Button>
                        )}
                      </div>
                      <Button variant="link" size="sm" className="p-0 small" onClick={() => setHistoryFor(s)}>
                        <i className="bi bi-graph-up me-1" />
                        Điểm theo tháng
                      </Button>
                    </td>
                    <td>
                      <ButtonGroup size="sm" aria-label={`Điểm danh ${s.fullName}`} className="flex-wrap">
                        {statusOptions.map((o) => (
                          <Button
                            key={o.value}
                            variant={r.status === o.value ? o.variant : `outline-${o.variant}`}
                            className={r.status === o.value && (o.variant === "warning" || o.variant === "info") ? "text-dark" : ""}
                            aria-pressed={r.status === o.value}
                            onClick={() => update({ [s.studentId]: { status: r.status === o.value ? null : o.value } })}
                          >
                            {o.short}
                          </Button>
                        ))}
                      </ButtonGroup>
                    </td>
                    <td>
                      <Form.Select
                        size="sm"
                        value={r.homeworkStatus ?? ""}
                        aria-label={`BTVN ${s.fullName}`}
                        onChange={(e) => update({ [s.studentId]: { homeworkStatus: (e.target.value || null) as AttendanceEntry["homeworkStatus"] } })}
                      >
                        <option value="">— Chưa chấm —</option>
                        {homeworkOptions.map((o) => (
                          <option key={o.value} value={o.value}>
                            {o.icon} {o.label}
                          </option>
                        ))}
                      </Form.Select>
                    </td>
                    <td>
                      <div className="d-flex align-items-center gap-1">
                        <Button size="sm" variant="outline-secondary" aria-label="Giảm 0,5 điểm" onClick={() => setScore(s.studentId, stepScore(r.homeworkScore, -0.5))}>
                          −
                        </Button>
                        <Form.Control
                          size="sm"
                          inputMode="decimal"
                          className={`text-center fw-semibold border-${scoreVariant(r.homeworkScore)}`}
                          style={{ width: 56 }}
                          data-score-input={s.studentId}
                          aria-label={`Điểm BTVN ${s.fullName}`}
                          placeholder="—"
                          value={draft ?? formatScoreValue(r.homeworkScore)}
                          onChange={(e) => setScoreDrafts((d) => ({ ...d, [s.studentId]: e.target.value }))}
                          onBlur={() => commitDraft(s.studentId)}
                          onKeyDown={(e: React.KeyboardEvent<HTMLInputElement>) => onScoreKey(e, s.studentId)}
                          onFocus={(e) => e.currentTarget.select()}
                        />
                        <Button size="sm" variant="outline-secondary" aria-label="Tăng 0,5 điểm" onClick={() => setScore(s.studentId, stepScore(r.homeworkScore, 0.5))}>
                          +
                        </Button>
                      </div>
                    </td>
                    <td>
                      <Form.Control
                        as="textarea"
                        rows={1}
                        size="sm"
                        maxLength={1000}
                        value={r.note ?? ""}
                        placeholder="Nhận xét…"
                        aria-label={`Nhận xét ${s.fullName}`}
                        onChange={(e) => update({ [s.studentId]: { note: e.target.value || null } })}
                      />
                      <div className="d-flex flex-wrap gap-1 mt-1">
                        {[...statusChips(r.status), ...academicChips].map((c) => (
                          <button
                            key={c.label}
                            type="button"
                            className="btn btn-light border btn-sm py-0 px-2 small rounded-pill"
                            style={{ fontSize: "0.72rem" }}
                            onClick={() => update({ [s.studentId]: { note: appendNote(rowsRef.current[s.studentId]!.note, c.text) } })}
                          >
                            + {c.label}
                          </button>
                        ))}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </Table>
        )}
      </Card>

      <ZaloReportModal
        show={showZalo}
        onHide={() => setShowZalo(false)}
        className={initial.classroomName}
        date={date}
        notes={notes}
        onNotes={updateNotes}
        students={students.map((s) => ({ ...rows[s.studentId]!, fullName: s.fullName }))}
        onCopy={(text) => void copy(text, "Đã sao chép tin nhắn Zalo.")}
      />

      <ContactModal
        student={contactFor}
        classId={classId}
        onHide={() => setContactFor(null)}
        onSaved={(c) => setStudents((list) => list.map((s) => (s.studentId === c.studentId ? { ...s, ...c } : s)))}
      />

      <HistoryModal student={historyFor} classId={classId} month={date.slice(0, 7)} onHide={() => setHistoryFor(null)} onCopy={(t) => void copy(t, "Đã sao chép tổng kết tháng.")} />

      <ConfirmDialog
        show={confirmReset}
        title="Làm mới buổi học?"
        body="Mọi học sinh về trạng thái chưa điểm danh; điểm, BTVN, nhận xét, nội dung buổi học bị xóa. Thao tác được ghi nhật ký."
        confirmText="Làm mới"
        variant="danger"
        busy={resetting}
        onConfirm={() => void reset()}
        onCancel={() => setConfirmReset(false)}
      />

      <Modal show={showShortcuts} onHide={() => setShowShortcuts(false)} centered>
        <Modal.Header closeButton>
          <Modal.Title as="h2" className="h5">
            <i className="bi bi-keyboard me-2" />
            Mẹo & phím tắt
          </Modal.Title>
        </Modal.Header>
        <Modal.Body>
          <ul className="list-unstyled mb-0 d-grid gap-2">
            <li className="d-flex justify-content-between">
              Tìm nhanh học sinh <kbd>/</kbd>
            </li>
            <li className="d-flex justify-content-between">
              Chấm điểm liên tục (sang dòng dưới / trên)
              <span>
                <kbd>Enter</kbd> <kbd>↓</kbd> <kbd>↑</kbd>
              </span>
            </li>
            <li className="d-flex justify-content-between">
              Điểm nhận cả dấu phẩy, tự làm tròn bước 0,5 <kbd>7,5</kbd>
            </li>
            <li className="d-flex justify-content-between">
              Lọc nhanh theo trạng thái <span className="text-secondary">bấm vào thẻ thống kê</span>
            </li>
            <li className="d-flex justify-content-between">
              Bỏ điểm danh <span className="text-secondary">bấm lại nút đang chọn</span>
            </li>
          </ul>
        </Modal.Body>
      </Modal>
    </>
  );
}

function SaveIndicator({ state, onRetry }: { state: SaveState; onRetry: () => void }) {
  switch (state.kind) {
    case "pending":
    case "saving":
      return (
        <span className="small text-secondary">
          <Spinner size="sm" className="me-1" />
          Đang lưu…
        </span>
      );
    case "saved":
      return (
        <span className="small text-success">
          <i className="bi bi-cloud-check me-1" />
          Đã lưu tự động · {formatDateTime(state.at)}
        </span>
      );
    case "error":
      return (
        <span className="small text-danger">
          <i className="bi bi-exclamation-triangle me-1" />
          Chưa lưu được
          <Button size="sm" variant="link" className="py-0" onClick={onRetry}>
            Thử lại
          </Button>
        </span>
      );
    default:
      return <span className="small text-secondary">Chưa có dữ liệu buổi này</span>;
  }
}

function ZaloReportModal({
  show,
  onHide,
  className,
  date,
  notes,
  onNotes,
  students,
  onCopy,
}: {
  show: boolean;
  onHide: () => void;
  className: string;
  date: string;
  notes: { topic: string; nextHomework: string };
  onNotes: (patch: Partial<{ topic: string; nextHomework: string }>) => void;
  students: Parameters<typeof buildZaloReport>[0];
  onCopy: (text: string) => void;
}) {
  const [style, setStyle] = useState<"detailed" | "compact">("detailed");
  const [includeScoreList, setIncludeScoreList] = useState(true);
  const [sortByScore, setSortByScore] = useState(true);
  const text = show
    ? buildZaloReport(students, { className, date, topic: notes.topic, nextHomework: notes.nextHomework, style, includeScoreList, sortByScore })
    : "";

  return (
    <Modal show={show} onHide={onHide} size="lg" centered scrollable>
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">
          <i className="bi bi-send me-2" />
          Báo cáo buổi học gửi nhóm Zalo phụ huynh
        </Modal.Title>
      </Modal.Header>
      <Modal.Body>
        <ButtonGroup size="sm" className="mb-3">
          <Button variant={style === "detailed" ? "primary" : "outline-primary"} onClick={() => setStyle("detailed")}>
            Chi tiết
          </Button>
          <Button variant={style === "compact" ? "primary" : "outline-primary"} onClick={() => setStyle("compact")}>
            Ngắn gọn
          </Button>
        </ButtonGroup>
        <Row className="g-2 mb-2">
          <Col md={6}>
            <Form.Control size="sm" placeholder="Nội dung buổi học" value={notes.topic} onChange={(e) => onNotes({ topic: e.target.value })} aria-label="Nội dung buổi học" />
          </Col>
          <Col md={6}>
            <Form.Control
              size="sm"
              placeholder="BTVN buổi sau"
              value={notes.nextHomework}
              onChange={(e) => onNotes({ nextHomework: e.target.value })}
              aria-label="BTVN buổi sau"
            />
          </Col>
        </Row>
        <div className="d-flex flex-wrap gap-3 mb-2 small">
          <Form.Check id="zalo-score-list" label="Kèm bảng điểm cả lớp" checked={includeScoreList} onChange={(e) => setIncludeScoreList(e.target.checked)} />
          <Form.Check id="zalo-sort" label="Xếp điểm cao → thấp" checked={sortByScore} onChange={(e) => setSortByScore(e.target.checked)} />
        </div>
        <Form.Control as="textarea" rows={14} readOnly value={text} className="font-monospace small" aria-label="Nội dung tin nhắn" />
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={onHide}>
          Đóng
        </Button>
        <Button variant="primary" onClick={() => onCopy(text)}>
          <i className="bi bi-clipboard me-1" />
          Sao chép tin nhắn
        </Button>
      </Modal.Footer>
    </Modal>
  );
}

function ContactModal({
  student,
  classId,
  onHide,
  onSaved,
}: {
  student: AttendanceStudent | null;
  classId: string;
  onHide: () => void;
  onSaved: (c: { studentId: string; parentName: string | null; parentPhone: string | null }) => void;
}) {
  const toast = useToast();
  const [form, setForm] = useState({ parentName: "", parentPhone: "" });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const save = async () => {
    if (!student) return;
    setBusy(true);
    setError(null);
    try {
      const saved = await attendanceApi.updateContact(classId, student.studentId, {
        parentName: form.parentName.trim() || null,
        parentPhone: form.parentPhone.trim() || null,
      });
      onSaved(saved);
      toast.success("Đã lưu liên hệ phụ huynh.");
      onHide();
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      show={student !== null}
      onHide={onHide}
      centered
      onShow={() => {
        setForm({ parentName: student?.parentName ?? "", parentPhone: student?.parentPhone ?? "" });
        setError(null);
      }}
    >
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">
          Liên hệ phụ huynh — {student?.fullName}
        </Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {error !== null && <ErrorAlert error={error} />}
        <Form.Group className="mb-3" controlId="contact-parent-name">
          <Form.Label>Tên phụ huynh</Form.Label>
          <Form.Control value={form.parentName} maxLength={200} onChange={(e) => setForm((f) => ({ ...f, parentName: e.target.value }))} />
        </Form.Group>
        <Form.Group controlId="contact-parent-phone">
          <Form.Label>Số điện thoại (dùng cho nút gọi / Zalo)</Form.Label>
          <Form.Control
            value={form.parentPhone}
            maxLength={20}
            inputMode="tel"
            placeholder="VD: 0912 345 678"
            onChange={(e) => setForm((f) => ({ ...f, parentPhone: e.target.value }))}
          />
        </Form.Group>
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={onHide}>
          Hủy
        </Button>
        <Button disabled={busy} onClick={() => void save()}>
          {busy && <Spinner size="sm" className="me-1" />}
          Lưu
        </Button>
      </Modal.Footer>
    </Modal>
  );
}

function HistoryModal({
  student,
  classId,
  month: initialMonth,
  onHide,
  onCopy,
}: {
  student: AttendanceStudent | null;
  classId: string;
  month: string;
  onHide: () => void;
  onCopy: (text: string) => void;
}) {
  const [month, setMonth] = useState(initialMonth);
  const history = useQuery({
    queryKey: ["attendance-history", classId, student?.studentId, month],
    queryFn: () => attendanceApi.history(classId, student!.studentId, month),
    enabled: student !== null && /^\d{4}-\d{2}$/.test(month),
  });
  const data = history.data;

  return (
    <Modal show={student !== null} onHide={onHide} onShow={() => setMonth(initialMonth)} size="xl" centered scrollable>
      <Modal.Header closeButton>
        <Modal.Title as="h2" className="h5">
          <i className="bi bi-graph-up me-2" />
          {student?.fullName} — điểm BTVN theo tháng
          <ContactLinks phone={student?.parentPhone ?? null} />
        </Modal.Title>
      </Modal.Header>
      <Modal.Body>
        <div className="d-flex flex-wrap gap-2 align-items-center mb-3">
          <Form.Label htmlFor="history-month" className="mb-0 small fw-semibold">
            Tháng
          </Form.Label>
          <Form.Control id="history-month" type="month" size="sm" value={month} onChange={(e) => setMonth(e.target.value)} style={{ width: 170 }} />
        </div>
        {history.isLoading && <Loading />}
        {history.error && <ErrorAlert error={history.error} onRetry={() => void history.refetch()} />}
        {data && <MonthlyHistoryView history={data} />}
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={onHide}>
          Đóng
        </Button>
        <Button disabled={!data} onClick={() => data && onCopy(buildMonthlyReport(data))}>
          <i className="bi bi-clipboard me-1" />
          Sao chép tổng kết tháng
        </Button>
      </Modal.Footer>
    </Modal>
  );
}
