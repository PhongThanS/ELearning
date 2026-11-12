import { Fragment, useEffect, useState } from "react";
import { Button, Col, Container, Nav, Navbar, NavDropdown, Row } from "react-bootstrap";
import { Link, NavLink, Outlet, useLocation, useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import { Permissions, StudentRole } from "../constants/permissions";
import { Features } from "../constants/features";
import { useAuth } from "../features/auth/useAuth";
import { categoriesApi } from "../services/api";
import { useAllClassrooms } from "../hooks/useClassrooms";
import { categoryLabel, orderCategories } from "../utils/categoryTree";
import { RECENT_LIMIT, rememberRecent, useRecent, type RecentItem } from "./recentItems";
import { QuickSearch, type QuickPage } from "./QuickSearch";
import { prefetchPages } from "../app/pageLoaders";

export function SClassLogo({
  size = 36,
  showTagline = true,
  lightText = true,
}: {
  size?: number;
  showTagline?: boolean;
  lightText?: boolean;
}) {
  return (
    <div className="sclass-brand-wrap d-inline-flex align-items-center gap-2">
      <div
        className="sclass-badge-box d-flex align-items-center justify-content-center flex-shrink-0"
        style={{
          width: size,
          height: size,
          minWidth: size,
          borderRadius: Math.max(7, Math.round(size * 0.22)),
          background: "#123788",
          border: lightText ? "1.5px solid rgba(255, 255, 255, 0.4)" : "1.5px solid #123788",
          boxShadow: lightText
            ? "0 2px 8px rgba(0, 210, 255, 0.35)"
            : "0 2px 8px rgba(18, 55, 136, 0.3)",
        }}
      >
        <svg
          width={Math.round(size * 0.64)}
          height={Math.round(size * 0.64)}
          viewBox="0 0 24 24"
          fill="none"
          stroke="#ffffff"
          strokeWidth="2.2"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <path d="M22 10v6M2 10l10-5 10 5-10 5z" />
          <path d="M6 12v5c3 3 9 3 12 0v-5" />
        </svg>
      </div>
      <div className="d-flex flex-column justify-content-center lh-1">
        <span
          className="sclass-brand-text fw-bold"
          style={{
            fontSize: `${Math.round(size * 0.65)}px`,
            color: lightText ? "#ffffff" : "#0c276b",
            fontFamily: "'Segoe UI', -apple-system, BlinkMacSystemFont, sans-serif",
            letterSpacing: "-0.02em",
          }}
        >
          SClass
        </span>
        {showTagline && (
          <span
            className="sclass-tagline"
            style={{
              fontSize: `${Math.max(9, Math.round(size * 0.27))}px`,
              color: lightText ? "#fda4af" : "#d92638",
              fontWeight: 600,
              marginTop: "2px",
              whiteSpace: "nowrap",
              letterSpacing: "-0.01em",
            }}
          >
            Dạy học là truyền cảm hứng
          </span>
        )}
      </div>
    </div>
  );
}

function UserMenu() {
  const { t } = useTranslation();
  const { user, logout, hasRole } = useAuth();
  const navigate = useNavigate();
  if (!user) {
    return null;
  }
  const firstLetter = (user.fullName || user.userName || "U").trim().charAt(0).toUpperCase();

  return (
    <NavDropdown
      title={
        <span className="d-inline-flex align-items-center gap-2 text-white">
          <span className="user-avatar-badge">{firstLetter}</span>
          <span className="fw-semibold small">{user.fullName}</span>
        </span>
      }
      align="end"
      id="user-menu"
      className="sclass-user-nav-dropdown"
    >
      <div className="px-3 py-2 border-bottom bg-light-subtle">
        <div className="fw-bold text-dark small">{user.fullName}</div>
        <div className="text-secondary" style={{ fontSize: "0.75rem" }}>
          @{user.userName} • {user.email}
        </div>
      </div>
      {user.permissions.length > 0 && (
        <NavDropdown.Item as={NavLink} to="/admin">
          <i className="bi bi-shield-lock me-2" aria-hidden="true" />{t("nav.admin")}
        </NavDropdown.Item>
      )}
      {hasRole(StudentRole) && (
        <NavDropdown.Item as={NavLink} to="/student/attendance">
          <i className="bi bi-mortarboard me-2" aria-hidden="true" />{t("nav.student")}
        </NavDropdown.Item>
      )}
      <NavDropdown.Item as={NavLink} to="/profile">
        <i className="bi bi-person-gear me-2" aria-hidden="true" />{t("nav.profile")}
      </NavDropdown.Item>
      <NavDropdown.Divider />
      <NavDropdown.Item
        as="button"
        className="text-danger fw-medium"
        onClick={async () => {
          await logout();
          navigate("/login", { replace: true });
        }}
      >
        <i className="bi bi-box-arrow-right me-2" aria-hidden="true" />{t("nav.logout")}
      </NavDropdown.Item>
    </NavDropdown>
  );
}

function getCodeColor(code: string): string {
  let hash = 0;
  for (let i = 0; i < code.length; i++) {
    hash = code.charCodeAt(i) + ((hash << 5) - hash);
  }
  const colors = [
    "#163e8a", "#0d9488", "#d97706", "#7c3aed",
    "#2563eb", "#059669", "#dc2626", "#4f46e5",
    "#0891b2", "#9333ea", "#c026d3", "#e11d48",
  ];
  return colors[Math.abs(hash) % colors.length] ?? "#2563eb";
}

interface AdminNavLeaf {
  to: string;
  label: string;
  /** Lớp Bootstrap Icons nét mảnh, ví dụ "bi-people". */
  icon: string;
  permission: string;
  /** Chỉ sáng khi đúng đường dẫn (không sáng ở trang con có mục menu riêng). */
  end?: boolean;
  /** Chữ hiển thị nguyên văn (tên lớp, chuyên đề) thay cho khóa dịch `label`. */
  text?: string;
  /** Dòng phụ "Xem tất cả": chữ nhạt, không tô sáng như mục đang chọn. */
  muted?: boolean;
}

interface AdminNavItem extends Omit<AdminNavLeaf, "to" | "permission"> {
  /** Trống: mục chỉ để gom nhóm (bấm để mở / thu các mục con). */
  to?: string;
  /** Trống: hiện khi người dùng có quyền với ít nhất một mục con. */
  permission?: string;
  /** Mục con là các lớp mở gần đây (D-28). */
  isClassesDropdown?: boolean;
  /** Mục con là các chuyên đề mở gần đây. */
  isCategoriesDropdown?: boolean;
  /** Mục con xổ xuống bằng mũi tên. */
  children?: AdminNavLeaf[];
  /** Tiêu đề nhóm hiện phía trên mục (bỏ trống: cùng nhóm với mục trước). */
  section?: string;
}

const adminNav: AdminNavItem[] = [
  { to: "/admin/dashboard", label: "nav.dashboard", icon: "bi-grid-1x2", permission: Permissions.ReportView },
  { section: "Giảng dạy", to: "/admin/attendance", label: "nav.attendance", icon: "bi-calendar-check", permission: Permissions.AttendanceTake },
  { to: "/admin/classes", label: "nav.classes", icon: "bi-mortarboard", permission: Permissions.ClassView, isClassesDropdown: true, end: true },
  { to: "/admin/videos", label: "nav.videos", icon: "bi-play-btn", permission: Permissions.VideoView },
  { section: "Đề và câu hỏi", to: "/admin/exams", label: "nav.exams", icon: "bi-file-earmark-text", permission: Permissions.ExamView },
  {
    to: "/admin/questions",
    label: "nav.questions",
    icon: "bi-collection",
    permission: Permissions.QuestionView,
    end: true,
    children: [{ to: "/admin/questions/import-markdown", label: "nav.importExam", icon: "bi-file-earmark-arrow-up", permission: Permissions.QuestionCreate }],
  },
  { to: "/admin/categories", label: "nav.categories", icon: "bi-folder2", permission: Permissions.CategoryView, isCategoriesDropdown: true, end: true },
  // Nhóm người dùng đang ẩn (Features.userGroups)
  ...(Features.userGroups ? [{ to: "/admin/groups", label: "nav.groups", icon: "bi-people", permission: Permissions.GroupView } as AdminNavItem] : []),
  {
    section: "Hệ thống",
    label: "nav.systemSettings",
    icon: "bi-gear",
    children: [
      { to: "/admin/users", label: "nav.users", icon: "bi-person", permission: Permissions.UserView },
      { to: "/admin/roles", label: "nav.roles", icon: "bi-shield-lock", permission: Permissions.RoleView },
      { to: "/admin/audit-logs", label: "nav.auditLogs", icon: "bi-journal-text", permission: Permissions.AuditView },
    ],
  },
];

/**
 * Mục menu bên có mục con: mũi tên để mở / thu (tự mở khi đang ở trang của một mục con).
 * Mục có trang riêng (Ngân hàng câu hỏi) bấm tên để mở trang, bấm mũi tên để xổ mục con;
 * mục chỉ gom nhóm (Cấu hình hệ thống) bấm cả dòng để mở / thu.
 */
function SidebarGroupItem({ item, leaves }: { item: AdminNavItem; leaves: AdminNavLeaf[] }) {
  const { t } = useTranslation();
  const { pathname, search } = useLocation();
  const inChild = leaves.some((c) => pathname.startsWith(c.to));
  const [open, setOpen] = useState(inChild);
  useEffect(() => {
    if (inChild) setOpen(true);
  }, [inChild]);
  const id = `nav-group-${item.label.replace(/\W/g, "-")}`;
  const chevron = <i className={`bi ${open ? "bi-chevron-down" : "bi-chevron-right"} small`} aria-hidden="true" />;

  return (
    <>
      {item.to ? (
        <div className="d-flex align-items-center">
          <Nav.Link as={NavLink} to={item.to} end={item.end} className="d-flex align-items-center gap-2 flex-grow-1">
            <i className={`nav-item-icon bi ${item.icon}`} aria-hidden="true" />
            <span className="nav-item-label">{t(item.label)}</span>
          </Nav.Link>
          <button
            type="button"
            className="btn btn-link btn-sm px-2 nav-group-toggle"
            aria-expanded={open}
            aria-controls={id}
            aria-label={`${open ? "Thu gọn" : "Mở"} ${t(item.label)}`}
            onClick={() => setOpen(!open)}
          >
            {chevron}
          </button>
        </div>
      ) : (
        <Nav.Link
          as="button"
          type="button"
          className="d-flex align-items-center gap-2 w-100 text-start border-0 bg-transparent"
          aria-expanded={open}
          aria-controls={id}
          onClick={() => setOpen(!open)}
        >
          <i className={`nav-item-icon bi ${item.icon}`} aria-hidden="true" />
          <span className="nav-item-label flex-grow-1">{t(item.label)}</span>
          {chevron}
        </Nav.Link>
      )}
      {open && (
        <div id={id} className="nav-subgroup">
          {leaves.map((c) =>
            c.muted ? (
              <Link key={c.to} to={c.to} className="nav-view-all d-block small">
                {c.text ?? t(c.label)}
                <i className="bi bi-arrow-right ms-1" aria-hidden="true" />
              </Link>
            ) : c.to.includes("?") ? (
              // Liên kết có tham số (chuyên đề): chỉ sáng khi đúng cả tham số, NavLink chỉ so phần đường dẫn
              <Link
                key={c.to}
                to={c.to}
                className={`nav-link d-flex align-items-center gap-2 nav-sublink ${pathname + search === c.to ? "active" : ""}`}
                aria-current={pathname + search === c.to ? "page" : undefined}
                title={c.text}
              >
                <i className={`nav-item-icon bi ${c.icon}`} aria-hidden="true" />
                <span className="nav-item-label">{c.text ?? t(c.label)}</span>
              </Link>
            ) : (
              <Nav.Link key={c.to} as={NavLink} to={c.to} end={c.end} className="d-flex align-items-center gap-2 nav-sublink" title={c.text}>
                <i className={`nav-item-icon bi ${c.icon}`} aria-hidden="true" />
                <span className="nav-item-label">{c.text ?? t(c.label)}</span>
              </Nav.Link>
            ),
          )}
        </div>
      )}
    </>
  );
}

export function AdminLayout() {
  const { t } = useTranslation();
  const { hasPermission } = useAuth();
  // Tải ngầm sẵn các trang quản trị khi trình duyệt rảnh: chuyển trang không phải chờ tải qua mạng
  useEffect(() => prefetchPages("admin"), []);
  // Mục con lọc theo quyền; mục chỉ gom nhóm hiện khi còn ít nhất một mục con
  const location = useLocation();
  const { data: allClasses } = useAllClassrooms(hasPermission(Permissions.ClassView));
  const { data: categoriesData } = useQuery({
    queryKey: ["sidebar-categories"],
    queryFn: () => categoriesApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
    enabled: hasPermission(Permissions.CategoryView),
  });
  const classes = allClasses ?? [];
  const categories = orderCategories(categoriesData?.items ?? []);
  const recentClasses = useRecent("class");
  const recentCategories = useRecent("category");

  // Ghi nhớ lớp / chuyên đề vừa mở để menu bên hiện "gần đây"
  useEffect(() => {
    const classId = /^\/admin\/classes\/([^/]+)$/.exec(location.pathname)?.[1];
    const opened = classId ? classes.find((c) => c.id === classId) : undefined;
    if (opened) rememberRecent("class", { id: opened.id, name: opened.name, code: opened.code });
    const categoryId = location.pathname === "/admin/questions" ? new URLSearchParams(location.search).get("categoryId") : null;
    const category = categoryId ? categories.find((c) => c.id === categoryId) : undefined;
    if (category) rememberRecent("category", { id: category.id, name: categoryLabel(category), code: category.code });
  }, [location.pathname, location.search, classes, categories]);

  // Mục con: tối đa 5 mục (ưu tiên mục mở gần đây, còn thiếu thì lấy theo thứ tự danh sách); cuối cùng là dòng "Xem tất cả"
  const pick = <T extends { id: string }>(recent: RecentItem[], all: T[], toItem: (x: T) => RecentItem): RecentItem[] => {
    const byId = new Map(all.map((x) => [x.id, x]));
    const known = recent.filter((r) => byId.has(r.id)).map((r) => toItem(byId.get(r.id)!));
    const rest = all.filter((x) => !known.some((k) => k.id === x.id)).map(toItem);
    // "Gần đây" chỉ quyết định hiện mục nào; thứ tự hiển thị luôn theo danh sách gốc để bấm vào mục không làm danh sách nhảy
    const shown = new Set([...known, ...rest].slice(0, RECENT_LIMIT).map((x) => x.id));
    return all.filter((x) => shown.has(x.id)).map(toItem);
  };
  const classLeaves = (permission: string): AdminNavLeaf[] => [
    ...pick(recentClasses, classes.filter((c) => c.isActive), (c) => ({ id: c.id, name: c.name, code: c.code })).map((c) => ({
      to: `/admin/classes/${c.id}`,
      label: c.code,
      text: c.name,
      icon: "bi-dot",
      permission,
    })),
    { to: "/admin/classes", label: "", text: `Xem tất cả ${classes.length} lớp`, icon: "", permission, muted: true, end: true },
  ];
  const categoryLeaves = (permission: string): AdminNavLeaf[] => [
    ...pick(recentCategories, categories, (c) => ({ id: c.id, name: categoryLabel(c), code: c.code })).map((c) => ({
      to: `/admin/questions?categoryId=${c.id}`,
      label: c.code,
      text: c.name,
      icon: "bi-dot",
      permission,
    })),
    { to: "/admin/categories", label: "", text: `Xem tất cả ${categories.length} chuyên đề`, icon: "", permission, muted: true, end: true },
  ];

  const items = adminNav
    .map((item) => {
      const children = item.isClassesDropdown
        ? classLeaves(item.permission!)
        : item.isCategoriesDropdown
          ? categoryLeaves(item.permission!)
          : (item.children ?? []);
      return { item, leaves: children.filter((c) => hasPermission(c.permission)) };
    })
    .map((entry, index, all) => {
      const visible = (e: typeof entry) => (e.item.permission ? hasPermission(e.item.permission) : e.leaves.length > 0);
      let section: string | undefined;
      for (let i = index; i >= 0; i--) {
        if (all[i]!.item.section) {
          section = all.slice(i, index).some(visible) ? undefined : all[i]!.item.section;
          break;
        }
      }
      return { ...entry, section, visible: visible(entry) };
    })
    .filter((entry) => entry.visible);

  // Ô tìm nhanh: mọi trang trong menu (trừ danh sách lớp / chuyên đề, được tìm riêng)
  const quickPages: QuickPage[] = items.flatMap(({ item, leaves }) => [
    ...(item.to ? [{ to: item.to, label: t(item.label), icon: item.icon }] : []),
    ...(item.isClassesDropdown || item.isCategoriesDropdown ? [] : leaves.map((c) => ({ to: c.to, label: t(c.label), icon: c.icon }))),
  ]);

  return (
    <>
      <Navbar bg="dark" data-bs-theme="dark" expand="lg" className="mb-0 sclass-navbar shadow-sm">
        <Container fluid>
          <Navbar.Brand as={NavLink} to="/admin" className="sclass-brand d-flex align-items-center">
            <SClassLogo size={36} showTagline={true} lightText={true} />
            <span className="sclass-portal-tag ms-2">{t("nav.admin")}</span>
          </Navbar.Brand>
          <div className="ms-auto ms-lg-4 me-2 me-lg-0">
            <QuickSearch pages={quickPages} />
          </div>
          <Navbar.Toggle aria-controls="admin-nav" />
          <Navbar.Collapse id="admin-nav">
            <Nav className="me-auto d-lg-none py-2">
              {items.map(({ item, leaves }) => {
                if (leaves.length > 0) {
                  return (
                    <NavDropdown key={item.label} title={t(item.label)} id={`mobile-${item.label}`}>
                      {item.to && (
                        <NavDropdown.Item as={NavLink} to={item.to} end={item.end}>
                          {t(item.label)}
                        </NavDropdown.Item>
                      )}
                      {leaves.map((c) => (
                        <NavDropdown.Item key={c.to} as={NavLink} to={c.to} className={c.muted ? "small text-secondary" : undefined}>
                          {c.text ?? t(c.label)}
                        </NavDropdown.Item>
                      ))}
                    </NavDropdown>
                  );
                }
                return (
                  <Nav.Link key={item.to} as={NavLink} to={item.to!} end={item.end} className="d-flex align-items-center gap-2">
                    <i className={`bi ${item.icon}`} aria-hidden="true" />
                    <span>{t(item.label)}</span>
                  </Nav.Link>
                );
              })}
            </Nav>
            <Nav className="ms-auto">
              <UserMenu />
            </Nav>
          </Navbar.Collapse>
        </Container>
      </Navbar>
      <div className="d-flex">
        <nav className="admin-sidebar d-none d-lg-block border-end" aria-label="Menu quản trị">
          <Nav className="flex-column" variant="pills">
            {items.map(({ item, leaves, section }) => (
              <Fragment key={item.to ?? item.label}>
                {section && <div className="nav-section">{section}</div>}
                {leaves.length > 0 ? (
                  <SidebarGroupItem item={item} leaves={leaves} />
                ) : (
                  <Nav.Link as={NavLink} to={item.to!} end={item.end} className="d-flex align-items-center gap-2">
                    <i className={`nav-item-icon bi ${item.icon}`} aria-hidden="true" />
                    <span className="nav-item-label">{t(item.label)}</span>
                  </Nav.Link>
                )}
              </Fragment>
            ))}
          </Nav>
        </nav>
        <main className="flex-grow-1 p-3 min-w-0">
          <Outlet />
        </main>
      </div>
    </>
  );
}

export function StudentLayout() {
  const { t } = useTranslation();
  // Tải ngầm sẵn phòng thi và các trang học viên khi trình duyệt rảnh
  useEffect(() => prefetchPages("student"), []);
  return (
    <>
      <Navbar bg="primary" data-bs-theme="dark" expand="xl" className="sclass-navbar shadow-sm">
        <Container>
          <Navbar.Brand as={NavLink} to="/student/attendance" className="sclass-brand d-flex align-items-center">
            <SClassLogo size={36} showTagline={true} lightText={true} />
            <span className="sclass-portal-tag ms-2">{t("nav.student")}</span>
          </Navbar.Brand>
          <Navbar.Toggle aria-controls="student-nav" />
          <Navbar.Collapse id="student-nav">
            <Nav className="me-auto">
              <Nav.Link as={NavLink} to="/student/attendance" className="d-flex align-items-center gap-1.5 text-nowrap">
                <i className="bi bi-calendar-check" aria-hidden="true" /> {t("nav.myAttendance")}
              </Nav.Link>
              <Nav.Link as={NavLink} to="/student/exams" className="d-flex align-items-center gap-1.5 text-nowrap">
                <i className="bi bi-file-earmark-text" aria-hidden="true" /> {t("nav.studentExams")}
              </Nav.Link>
              <Nav.Link as={NavLink} to="/student/classes" className="d-flex align-items-center gap-1.5 text-nowrap">
                <i className="bi bi-mortarboard" aria-hidden="true" /> {t("nav.myClasses")}
              </Nav.Link>
              <Nav.Link as={NavLink} to="/student/videos" className="d-flex align-items-center gap-1.5 text-nowrap">
                <i className="bi bi-play-btn" aria-hidden="true" /> {t("nav.videos", "Video bài giảng")}
              </Nav.Link>
              <Nav.Link as={NavLink} to="/student/history" className="d-flex align-items-center gap-1.5 text-nowrap">
                <i className="bi bi-clock-history" aria-hidden="true" /> {t("nav.history")}
              </Nav.Link>
            </Nav>
            <Nav>
              <UserMenu />
            </Nav>
          </Navbar.Collapse>
        </Container>
      </Navbar>
      <Container as="main" className="py-4">
        <Outlet />
      </Container>
    </>
  );
}

/** Layout cho trang làm bài: không có menu để học viên tập trung. */
export function PlayerLayout() {
  return (
    <main>
      <Outlet />
    </main>
  );
}

/** Ba điểm nổi bật ở trang đăng nhập: [khóa i18n, icon Bootstrap Icons, màu nhấn]. */
const landingFeatures: [string, string, string][] = [
  ["feature1", "bi-laptop", "orange"],
  ["feature2", "bi-collection", "indigo"],
  ["feature3", "bi-graph-up-arrow", "teal"],
];

/** Trang đăng nhập / đăng ký / quên mật khẩu: giới thiệu bên trái, khung form bên phải, nền pastel. */
export function PublicLayout() {
  const { t } = useTranslation();
  const { data: catData } = useQuery({
    queryKey: ["public-active-categories"],
    queryFn: () => categoriesApi.list({ isActive: true, pageSize: 5, sortBy: "name" }),
    staleTime: 60_000,
  });
  const categories = catData?.items ?? [];

  return (
    <div className="auth-page">
      {/* Mảng màu loang và hình vẽ trang trí (không nhận chuột) */}
      <div className="auth-blob auth-blob-lemon" aria-hidden="true" />
      <div className="auth-blob auth-blob-lavender" aria-hidden="true" />
      <div className="auth-blob auth-blob-sky" aria-hidden="true" />
      <svg className="auth-doodle auth-doodle-squiggle" width="92" height="64" viewBox="0 0 92 64" fill="none" aria-hidden="true">
        <path d="M4 52c14-2 22-14 18-24-3-8-14-5-11 4 4 11 24 12 36 2 9-8 13-20 26-24" stroke="#f5b82e" strokeWidth="3" strokeLinecap="round" />
        <path d="M70 6l12 4-7 10" stroke="#f5b82e" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
      <span className="auth-doodle auth-doodle-ring" aria-hidden="true" />
      <span className="auth-doodle auth-doodle-dots" aria-hidden="true" />
      <svg className="auth-doodle auth-doodle-triangle" width="72" height="72" viewBox="0 0 100 100" fill="none" stroke="#7c6fff" strokeOpacity="0.35" strokeWidth="3" aria-hidden="true">
        <polygon points="50,12 12,86 88,86" />
        <path d="M27 68a20 20 0 0 0 17 18" />
      </svg>

      <Container className="auth-container">
        <Row className="align-items-center g-4 g-lg-5">
          <Col lg={6} className="auth-intro">
            <SClassLogo size={44} showTagline={true} lightText={false} />
            <h1 className="auth-headline mt-4">
              {t("landing.headlineStart")} <span className="text-gradient">{t("landing.headlineAccent")}</span> {t("landing.headlineEnd")}
            </h1>
            <p className="auth-lead">{t("landing.intro")}</p>
            <ul className="auth-features list-unstyled d-none d-md-grid">
              {landingFeatures.map(([key, icon, accent], index) => (
                <li key={key} className={`auth-feature accent-${accent}`}>
                  <span className="auth-feature-number">{String(index + 1).padStart(2, "0")}</span>
                  <div className="min-w-0">
                    <div className="auth-feature-title">
                      <i className={`bi ${icon} me-2`} aria-hidden="true" />
                      {t(`landing.${key}Title`)}
                    </div>
                    <div className="auth-feature-text">{t(`landing.${key}Text`)}</div>
                  </div>
                </li>
              ))}
            </ul>
            <div className="d-flex flex-wrap gap-2 mt-3">
              {categories.length > 0 ? (
                categories.map((c) => (
                  <span
                    key={c.id}
                    className="auth-chip"
                    style={{ color: getCodeColor(c.code), borderColor: `${getCodeColor(c.code)}40`, backgroundColor: `${getCodeColor(c.code)}12` }}
                  >
                    <i className="bi bi-bookmark-star-fill me-1" aria-hidden="true" />
                    {c.name}
                  </span>
                ))
              ) : (
                <span className="auth-chip">{t("landing.tagline")}</span>
              )}
            </div>
          </Col>
          <Col lg={6}>
            <div className="auth-form-wrap">
              <div className="auth-float auth-float-top d-none d-sm-flex" aria-hidden="true">
                <span className="auth-float-icon accent-green"><i className="bi bi-patch-check-fill" /></span>
                {t("landing.badgeGrading")}
              </div>
              <div className="auth-float auth-float-bottom d-none d-sm-flex" aria-hidden="true">
                <span className="auth-float-icon accent-amber"><i className="bi bi-cloud-check-fill" /></span>
                {t("landing.badgeTimer")}
              </div>
              <Outlet />
            </div>
          </Col>
        </Row>
      </Container>
    </div>
  );
}

export function BackButton() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  return (
    <Button variant="link" className="px-0" onClick={() => navigate(-1)}>
      ← {t("common.back")}
    </Button>
  );
}
