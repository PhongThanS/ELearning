import { useState } from "react";
import { Button, Container, Dropdown, Nav, Navbar, Offcanvas } from "react-bootstrap";
import { NavLink, Outlet, useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import { Permissions, StudentRole } from "../constants/permissions";
import { useAuth } from "../features/auth/useAuth";

/** Chữ cái đầu của họ tên cho avatar, ví dụ "Nguyễn Văn An" → "NA". */
function initials(fullName: string): string {
  const words = fullName.trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) {
    return "?";
  }
  const first = words[0]![0] ?? "";
  const last = words.length > 1 ? (words[words.length - 1]![0] ?? "") : "";
  return (first + last).toUpperCase();
}

function UserMenu({ light = false }: { light?: boolean }) {
  const { t } = useTranslation();
  const { user, logout, hasRole } = useAuth();
  const navigate = useNavigate();
  if (!user) {
    return null;
  }
  return (
    <Dropdown align="end">
      <Dropdown.Toggle
        variant="link"
        id="user-menu"
        className={`user-menu-toggle d-flex align-items-center gap-2 text-decoration-none ${light ? "text-white" : "text-body"}`}
      >
        <span className="avatar" aria-hidden="true">{initials(user.fullName)}</span>
        <span className="d-none d-sm-inline">{user.fullName}</span>
      </Dropdown.Toggle>
      <Dropdown.Menu className="shadow border-0">
        {user.permissions.length > 0 && (
          <Dropdown.Item as={NavLink} to="/admin">
            <i className="bi bi-speedometer2 me-2" aria-hidden="true" />
            {t("nav.admin")}
          </Dropdown.Item>
        )}
        {hasRole(StudentRole) && (
          <Dropdown.Item as={NavLink} to="/student/exams">
            <i className="bi bi-mortarboard me-2" aria-hidden="true" />
            {t("nav.student")}
          </Dropdown.Item>
        )}
        <Dropdown.Item as={NavLink} to="/profile">
          <i className="bi bi-person-circle me-2" aria-hidden="true" />
          {t("nav.profile")}
        </Dropdown.Item>
        <Dropdown.Divider />
        <Dropdown.Item
          as="button"
          className="text-danger"
          onClick={async () => {
            await logout();
            navigate("/login", { replace: true });
          }}
        >
          <i className="bi bi-box-arrow-right me-2" aria-hidden="true" />
          {t("nav.logout")}
        </Dropdown.Item>
      </Dropdown.Menu>
    </Dropdown>
  );
}

/** Mục menu quản trị: mỗi khu một màu nhấn để dễ nhận ra (xem theme.scss, .accent-*). */
const adminNav: { to: string; label: string; permission: string; icon: string; accent: string }[] = [
  { to: "/admin/dashboard", label: "nav.dashboard", permission: Permissions.ReportView, icon: "bi-grid-1x2-fill", accent: "indigo" },
  { to: "/admin/exams", label: "nav.exams", permission: Permissions.ExamView, icon: "bi-journal-check", accent: "violet" },
  { to: "/admin/questions", label: "nav.questions", permission: Permissions.QuestionView, icon: "bi-patch-question-fill", accent: "sky" },
  { to: "/admin/categories", label: "nav.categories", permission: Permissions.CategoryView, icon: "bi-folder2-open", accent: "teal" },
  { to: "/admin/users", label: "nav.users", permission: Permissions.UserView, icon: "bi-people-fill", accent: "green" },
  { to: "/admin/groups", label: "nav.groups", permission: Permissions.GroupView, icon: "bi-diagram-3-fill", accent: "amber" },
  { to: "/admin/roles", label: "nav.roles", permission: Permissions.RoleView, icon: "bi-shield-lock-fill", accent: "orange" },
  { to: "/admin/audit-logs", label: "nav.auditLogs", permission: Permissions.AuditView, icon: "bi-clock-history", accent: "pink" },
];

function AdminNavItems({ onNavigate }: { onNavigate?: () => void }) {
  const { t } = useTranslation();
  const { hasPermission } = useAuth();
  return (
    <Nav className="flex-column gap-1">
      {adminNav
        .filter((item) => hasPermission(item.permission))
        .map((item) => (
          <Nav.Link key={item.to} as={NavLink} to={item.to} onClick={onNavigate} className={`sidebar-link accent-${item.accent}`}>
            <span className="sidebar-icon" aria-hidden="true">
              <i className={`bi ${item.icon}`} />
            </span>
            {t(item.label)}
          </Nav.Link>
        ))}
    </Nav>
  );
}

function Brand({ subtitle }: { subtitle?: string }) {
  const { t } = useTranslation();
  return (
    <span className="brand d-inline-flex align-items-center gap-2">
      <span className="brand-mark" aria-hidden="true">
        <i className="bi bi-mortarboard-fill" />
      </span>
      <span className="lh-sm">
        <span className="d-block fw-bold">{t("app.name")}</span>
        {subtitle && <span className="d-block brand-sub">{subtitle}</span>}
      </span>
    </span>
  );
}

export function AdminLayout() {
  const { t } = useTranslation();
  const [menuOpen, setMenuOpen] = useState(false);

  return (
    <div className="admin-shell d-flex">
      <aside className="admin-sidebar d-none d-lg-flex flex-column" aria-label="Menu quản trị">
        <NavLink to="/admin" className="sidebar-brand text-white text-decoration-none">
          <Brand subtitle={t("nav.admin")} />
        </NavLink>
        <AdminNavItems />
        <div className="mt-auto sidebar-foot small">{t("app.tagline")}</div>
      </aside>

      <Offcanvas show={menuOpen} onHide={() => setMenuOpen(false)} className="admin-sidebar admin-offcanvas" aria-label="Menu quản trị">
        <Offcanvas.Header closeButton closeVariant="white">
          <Offcanvas.Title as="div">
            <Brand subtitle={t("nav.admin")} />
          </Offcanvas.Title>
        </Offcanvas.Header>
        <Offcanvas.Body>
          <AdminNavItems onNavigate={() => setMenuOpen(false)} />
        </Offcanvas.Body>
      </Offcanvas>

      <div className="flex-grow-1 min-w-0 d-flex flex-column">
        <header className="admin-topbar d-flex align-items-center gap-2 px-3">
          <Button variant="light" className="d-lg-none" aria-label="Mở menu" onClick={() => setMenuOpen(true)}>
            <i className="bi bi-list fs-5" aria-hidden="true" />
          </Button>
          <span className="d-lg-none fw-semibold">{t("app.name")}</span>
          <div className="ms-auto">
            <UserMenu />
          </div>
        </header>
        <main className="admin-main flex-grow-1 p-3 p-lg-4">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

export function StudentLayout() {
  const { t } = useTranslation();
  return (
    <>
      <Navbar expand="md" data-bs-theme="dark" className="student-navbar">
        <Container>
          <Navbar.Brand as={NavLink} to="/student/exams">
            <Brand />
          </Navbar.Brand>
          <Navbar.Toggle aria-controls="student-nav" />
          <Navbar.Collapse id="student-nav">
            <Nav className="me-auto gap-md-1">
              <Nav.Link as={NavLink} to="/student/exams" className="student-nav-link">
                <i className="bi bi-journal-text me-1" aria-hidden="true" />
                {t("nav.studentExams")}
              </Nav.Link>
              <Nav.Link as={NavLink} to="/student/history" className="student-nav-link">
                <i className="bi bi-bar-chart-line me-1" aria-hidden="true" />
                {t("nav.history")}
              </Nav.Link>
            </Nav>
            <UserMenu light />
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
    <main className="player-shell">
      <Outlet />
    </main>
  );
}

/** Trang đăng nhập / đăng ký: một nửa giới thiệu nhiều màu, một nửa biểu mẫu. */
export function PublicLayout() {
  const { t } = useTranslation();
  const features: [string, string, string][] = [
    ["bi-lightning-charge-fill", "amber", t("landing.autosave")],
    ["bi-stopwatch-fill", "sky", t("landing.timer")],
    ["bi-graph-up-arrow", "green", t("landing.results")],
  ];
  return (
    <div className="public-shell min-vh-100 d-flex">
      <section className="public-hero d-none d-lg-flex flex-column justify-content-center text-white" aria-hidden="true">
        <div className="hero-blob hero-blob-1" />
        <div className="hero-blob hero-blob-2" />
        <div className="position-relative">
          <Brand />
          <h2 className="display-6 fw-bold mt-4 mb-3">{t("landing.title")}</h2>
          <p className="fs-5 opacity-75 mb-4">{t("landing.subtitle")}</p>
          <ul className="list-unstyled d-grid gap-3 mb-0">
            {features.map(([icon, accent, text]) => (
              <li key={icon} className={`d-flex align-items-center gap-3 accent-${accent}`}>
                <span className="hero-feature-icon">
                  <i className={`bi ${icon}`} />
                </span>
                {text}
              </li>
            ))}
          </ul>
        </div>
      </section>
      <section className="flex-grow-1 d-flex align-items-center justify-content-center p-3">
        <div className="w-100" style={{ maxWidth: 440 }}>
          <div className="text-center mb-4 d-lg-none">
            <Brand />
            <p className="text-secondary mt-2 mb-0">{t("app.tagline")}</p>
          </div>
          <Outlet />
        </div>
      </section>
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
