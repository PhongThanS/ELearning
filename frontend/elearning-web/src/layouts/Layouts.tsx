import { useState, useRef } from "react";
import { Button, Container, Nav, Navbar, NavDropdown, Spinner } from "react-bootstrap";
import { NavLink, Outlet, useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import { Permissions, StudentRole } from "../constants/permissions";
import { useAuth } from "../features/auth/useAuth";
import { groupsApi, categoriesApi } from "../services/api";
import type { Group, Category } from "../types/api";

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
          EClass
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
          🛡️ {t("nav.admin")}
        </NavDropdown.Item>
      )}
      {hasRole(StudentRole) && (
        <NavDropdown.Item as={NavLink} to="/student/exams">
          🎓 {t("nav.student")}
        </NavDropdown.Item>
      )}
      <NavDropdown.Item as={NavLink} to="/profile">
        ⚙️ {t("nav.profile")}
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
        🚪 {t("nav.logout")}
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

function getGroupBadge(code: string) {
  const parts = code.split("-");
  return parts.length > 1 ? parts.slice(1).join("-") : code.slice(0, 4).toUpperCase();
}


// Hover dropdown cho Danh sách lớp học (dữ liệu động từ API backend)
function SidebarGroupsHoverItem({ label }: { label: string }) {
  const [isOpen, setIsOpen] = useState(false);
  const timerRef = useRef<number | null>(null);

  // Gọi trực tiếp API backend /api/groups
  const { data: groupsData, isLoading } = useQuery({
    queryKey: ["sidebar-groups"],
    queryFn: () => groupsApi.list({ page: 1, pageSize: 20 }),
    staleTime: 60_000,
  });

  const groups: Group[] = groupsData?.items ?? [];

  const handleMouseEnter = () => {
    if (timerRef.current) clearTimeout(timerRef.current);
    setIsOpen(true);
  };

  const handleMouseLeave = () => {
    timerRef.current = window.setTimeout(() => {
      setIsOpen(false);
    }, 120);
  };

  return (
    <div
      className={`sidebar-groups-wrapper is-bottom ${isOpen ? "hover-active" : ""}`}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
    >
      <NavLink
        to="/admin/groups"
        className={({ isActive }) =>
          `nav-link groups-nav-link d-flex align-items-center justify-content-between ${
            isActive ? "active" : ""
          }`
        }
        end
      >
        <span className="d-flex align-items-center gap-2 text-truncate">
          <span className="nav-item-icon">🏫</span>
          <span className="nav-item-label">{label}</span>
        </span>
        <span className="d-flex align-items-center gap-1">
          <span className="class-pill-badge">
            {isLoading ? "..." : `${groups.length} lớp`}
          </span>
          <span className={`chevron-indicator ${isOpen ? "open" : ""}`}>›</span>
        </span>
      </NavLink>

      {/* Flyout panel on hover */}
      <div className={`groups-flyout-menu shadow-lg ${isOpen ? "is-visible" : ""}`}>
        <div className="flyout-hover-bridge" />
        <div className="groups-flyout-header">
          <div className="d-flex align-items-center justify-content-between">
            <span className="groups-flyout-title">🏫 Danh sách lớp học</span>
            <span className="badge bg-primary-subtle text-primary border border-primary-subtle">
              {groups.length} lớp đang mở
            </span>
          </div>
          <div className="groups-flyout-subtitle">
            Dữ liệu trực tiếp từ máy chủ backend
          </div>
        </div>

        <div className="groups-flyout-list">
          {isLoading && (
            <div className="text-center py-3 text-muted small">
              <Spinner size="sm" animation="border" className="me-2" />
              Đang tải danh sách lớp...
            </div>
          )}

          {!isLoading && groups.length === 0 && (
            <div className="text-center py-3 text-muted small">
              Chưa có lớp học nào trong hệ thống.
            </div>
          )}

          {groups.map((g) => (
            <NavLink
              key={g.id}
              to={`/admin/groups/${g.id}`}
              className={({ isActive }) =>
                `class-card-item d-flex align-items-center ${isActive ? "active" : ""}`
              }
              onClick={() => setIsOpen(false)}
            >
              <div
                className="class-card-badge"
                style={{ backgroundColor: getCodeColor(g.code) }}
              >
                {getGroupBadge(g.code)}
              </div>
              <div className="class-card-info flex-grow-1 min-w-0">
                <div className="class-card-name text-truncate">
                  {g.name}
                </div>
                <div className="class-card-meta d-flex align-items-center gap-2">
                  <span className="class-card-students">
                    👥 {g.memberCount} học viên
                  </span>
                  <span className="class-card-code">
                    {g.code}
                  </span>
                </div>
              </div>
              <span className="class-card-arrow">→</span>
            </NavLink>
          ))}
        </div>

        <div className="groups-flyout-footer">
          <NavLink
            to="/admin/groups"
            className="view-all-link"
            onClick={() => setIsOpen(false)}
          >
            <span>Quản lý tất cả danh sách lớp học</span>
            <span className="ms-1">→</span>
          </NavLink>
        </div>
      </div>
    </div>
  );
}

// Hover dropdown cho Chuyên đề môn học (dữ liệu động từ API backend)
function SidebarCategoriesHoverItem({ label }: { label: string }) {
  const [isOpen, setIsOpen] = useState(false);
  const timerRef = useRef<number | null>(null);

  // Gọi trực tiếp API backend /api/question-categories
  const { data: categoriesData, isLoading } = useQuery({
    queryKey: ["sidebar-categories"],
    queryFn: () => categoriesApi.list({ page: 1, pageSize: 50 }),
    staleTime: 60_000,
  });

  const categories: Category[] = categoriesData?.items ?? [];

  const handleMouseEnter = () => {
    if (timerRef.current) clearTimeout(timerRef.current);
    setIsOpen(true);
  };

  const handleMouseLeave = () => {
    timerRef.current = window.setTimeout(() => {
      setIsOpen(false);
    }, 120);
  };

  return (
    <div
      className={`sidebar-groups-wrapper ${isOpen ? "hover-active" : ""}`}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
    >
      <NavLink
        to="/admin/categories"
        className={({ isActive }) =>
          `nav-link groups-nav-link d-flex align-items-center justify-content-between ${
            isActive ? "active" : ""
          }`
        }
        end
      >
        <span className="d-flex align-items-center gap-2 text-truncate">
          <span className="nav-item-icon">📁</span>
          <span className="nav-item-label">{label}</span>
        </span>
        <span className="d-flex align-items-center gap-1">
          <span className="class-pill-badge">
            {isLoading ? "..." : `${categories.length} CĐ`}
          </span>
          <span className={`chevron-indicator ${isOpen ? "open" : ""}`}>›</span>
        </span>
      </NavLink>

      {/* Flyout panel on hover */}
      <div className={`groups-flyout-menu shadow-lg ${isOpen ? "is-visible" : ""}`} style={{ width: 360 }}>
        <div className="flyout-hover-bridge" />
        <div className="groups-flyout-header">
          <div className="d-flex align-items-center justify-content-between">
            <span className="groups-flyout-title">📁 Danh sách chuyên đề</span>
            <span className="badge bg-primary-subtle text-primary border border-primary-subtle">
              {categories.length} chuyên đề
            </span>
          </div>
          <div className="groups-flyout-subtitle">
            Dữ liệu trực tiếp từ máy chủ backend
          </div>
        </div>

        <div className="groups-flyout-list">
          {isLoading && (
            <div className="text-center py-3 text-muted small">
              <Spinner size="sm" animation="border" className="me-2" />
              Đang tải chuyên đề từ máy chủ...
            </div>
          )}

          {!isLoading && categories.length === 0 && (
            <div className="text-center py-3 text-muted small">
              Chưa có chuyên đề nào trong hệ thống.
            </div>
          )}

          {categories.map((c) => (
            <NavLink
              key={c.id}
              to={`/admin/questions?categoryId=${c.id}`}
              className={({ isActive }) =>
                `class-card-item d-flex align-items-center ${isActive ? "active" : ""}`
              }
              onClick={() => setIsOpen(false)}
            >
              <div
                className="class-card-badge"
                style={{ backgroundColor: getCodeColor(c.code) }}
              >
                {getGroupBadge(c.code)}
              </div>
              <div className="class-card-info flex-grow-1 min-w-0">
                <div className="class-card-name text-truncate">
                  {c.name}
                </div>
                <div className="class-card-meta d-flex align-items-center gap-2">
                  <span className="class-card-students">
                    ❓ {c.questionCount} câu hỏi
                  </span>
                  <span className="class-card-code">
                    {c.code}
                  </span>
                </div>
              </div>
              <span className="class-card-arrow">→</span>
            </NavLink>
          ))}
        </div>

        <div className="groups-flyout-footer">
          <NavLink
            to="/admin/categories"
            className="view-all-link"
            onClick={() => setIsOpen(false)}
          >
            <span>Quản lý tất cả danh mục chuyên đề</span>
            <span className="ms-1">→</span>
          </NavLink>
        </div>
      </div>
    </div>
  );
}

const adminNav: {
  to: string;
  label: string;
  icon: string;
  permission: string;
  isGroupsDropdown?: boolean;
  isCategoriesDropdown?: boolean;
}[] = [
  { to: "/admin/dashboard", label: "nav.dashboard", icon: "📊", permission: Permissions.ReportView },
  { to: "/admin/exams", label: "nav.exams", icon: "📝", permission: Permissions.ExamView },
  { to: "/admin/questions", label: "nav.questions", icon: "❓", permission: Permissions.QuestionView },
  { to: "/admin/categories", label: "nav.categories", icon: "📁", permission: Permissions.CategoryView, isCategoriesDropdown: true },
  { to: "/admin/users", label: "nav.users", icon: "👤", permission: Permissions.UserView },
  { to: "/admin/roles", label: "nav.roles", icon: "🛡️", permission: Permissions.RoleView },
  { to: "/admin/audit-logs", label: "nav.auditLogs", icon: "📜", permission: Permissions.AuditView },
  { to: "/admin/groups", label: "nav.groups", icon: "🏫", permission: Permissions.GroupView, isGroupsDropdown: true },
];

export function AdminLayout() {
  const { t } = useTranslation();
  const { hasPermission } = useAuth();
  const items = adminNav.filter((item) => hasPermission(item.permission));

  // Lấy dữ liệu cho menu di động từ API backend
  const { data: groupsData } = useQuery({
    queryKey: ["sidebar-groups"],
    queryFn: () => groupsApi.list({ page: 1, pageSize: 20 }),
    staleTime: 60_000,
  });
  const { data: categoriesData } = useQuery({
    queryKey: ["sidebar-categories"],
    queryFn: () => categoriesApi.list({ page: 1, pageSize: 50 }),
    staleTime: 60_000,
  });

  const groups = groupsData?.items ?? [];
  const categories = categoriesData?.items ?? [];

  return (
    <>
      <Navbar bg="dark" data-bs-theme="dark" expand="lg" className="mb-0 sclass-navbar shadow-sm">
        <Container fluid>
          <Navbar.Brand as={NavLink} to="/admin" className="sclass-brand d-flex align-items-center">
            <SClassLogo size={36} showTagline={true} lightText={true} />
            <span className="sclass-portal-tag ms-2">{t("nav.admin")}</span>
          </Navbar.Brand>
          <Navbar.Toggle aria-controls="admin-nav" />
          <Navbar.Collapse id="admin-nav">
            <Nav className="me-auto d-lg-none py-2">
              {items.map((item) => {
                if (item.isGroupsDropdown) {
                  return (
                    <NavDropdown key={item.to} title={`🏫 ${t(item.label)}`} id="mobile-groups-nav">
                      <NavDropdown.Item as={NavLink} to="/admin/groups" end>
                        📋 Tất cả danh sách lớp học
                      </NavDropdown.Item>
                      <NavDropdown.Divider />
                      {groups.map((g) => (
                        <NavDropdown.Item key={g.id} as={NavLink} to={`/admin/groups/${g.id}`}>
                          🎓 {g.name} ({g.memberCount} học viên)
                        </NavDropdown.Item>
                      ))}
                    </NavDropdown>
                  );
                }
                if (item.isCategoriesDropdown) {
                  return (
                    <NavDropdown key={item.to} title={`📁 ${t(item.label)}`} id="mobile-categories-nav">
                      <NavDropdown.Item as={NavLink} to="/admin/categories" end>
                        📋 Tất cả chuyên đề
                      </NavDropdown.Item>
                      <NavDropdown.Divider />
                      {categories.map((c) => (
                        <NavDropdown.Item key={c.id} as={NavLink} to={`/admin/questions?categoryId=${c.id}`}>
                          📁 {c.name}
                        </NavDropdown.Item>
                      ))}
                    </NavDropdown>
                  );
                }
                return (
                  <Nav.Link key={item.to} as={NavLink} to={item.to} className="d-flex align-items-center gap-2">
                    <span>{item.icon}</span>
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
        <nav className="admin-sidebar d-none d-lg-block border-end p-2.5" aria-label="Menu quản trị">
          <Nav className="flex-column" variant="pills">
            {items.map((item) => {
              if (item.isGroupsDropdown) {
                return (
                  <SidebarGroupsHoverItem
                    key={item.to}
                    label={t(item.label)}
                  />
                );
              }
              if (item.isCategoriesDropdown) {
                return (
                  <SidebarCategoriesHoverItem
                    key={item.to}
                    label={t(item.label)}
                  />
                );
              }
              return (
                <Nav.Link key={item.to} as={NavLink} to={item.to} className="d-flex align-items-center gap-2">
                  <span className="nav-item-icon">{item.icon}</span>
                  <span className="nav-item-label">{t(item.label)}</span>
                </Nav.Link>
              );
            })}
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
  return (
    <>
      <Navbar bg="primary" data-bs-theme="dark" expand="md" className="sclass-navbar shadow-sm">
        <Container>
          <Navbar.Brand as={NavLink} to="/student/exams" className="sclass-brand d-flex align-items-center">
            <SClassLogo size={36} showTagline={true} lightText={true} />
            <span className="sclass-portal-tag ms-2">{t("nav.student")}</span>
          </Navbar.Brand>
          <Navbar.Toggle aria-controls="student-nav" />
          <Navbar.Collapse id="student-nav">
            <Nav className="me-auto">
              <Nav.Link as={NavLink} to="/student/exams" className="d-flex align-items-center gap-1.5">
                <span>📝</span> {t("nav.studentExams")}
              </Nav.Link>
              <Nav.Link as={NavLink} to="/student/history" className="d-flex align-items-center gap-1.5">
                <span>📊</span> {t("nav.history")}
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

export function PublicLayout() {
  const { data: catData } = useQuery({
    queryKey: ["public-active-categories"],
    queryFn: () => categoriesApi.list({ isActive: true, pageSize: 5, sortBy: "name" }),
    staleTime: 60_000,
  });
  const categories = catData?.items ?? [];

  return (
    <div className="min-vh-100 d-flex align-items-center sclass-auth-wrapper py-5 position-relative">
      {/* Background study doodles inspired by S Class banner */}
      <div className="sclass-banner-backdrop" />
      <div className="sclass-study-doodle doodle-triangle">
        <svg width="75" height="75" viewBox="0 0 100 100" fill="none" stroke="rgba(255,255,255,0.15)" strokeWidth="2.5">
          <polygon points="50,15 15,85 85,85" />
          <path d="M 28,68 A 20,20 0 0,0 45,85" />
          <text x="47" y="10" fill="rgba(255,255,255,0.3)" fontSize="12" fontWeight="bold">A</text>
          <text x="3" y="96" fill="rgba(255,255,255,0.3)" fontSize="12" fontWeight="bold">B</text>
          <text x="87" y="96" fill="rgba(255,255,255,0.3)" fontSize="12" fontWeight="bold">C</text>
        </svg>
      </div>
      <div className="sclass-study-doodle doodle-book">
        <svg width="68" height="68" viewBox="0 0 24 24" fill="none" stroke="rgba(255,255,255,0.18)" strokeWidth="2">
          <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20" />
          <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z" />
        </svg>
      </div>
      <div className="sclass-dots-matrix matrix-left" />
      <div className="sclass-dots-matrix matrix-right" />

      <Container style={{ maxWidth: 460, position: "relative", zIndex: 2 }}>
        <div className="text-center mb-4">
          <div className="bg-white d-inline-block px-4 py-3 rounded-4 shadow-lg border border-light-subtle mb-2">
            <SClassLogo size={46} showTagline={true} lightText={false} />
          </div>
          <div className="d-flex justify-content-center flex-wrap gap-2 mt-2">
            {categories.length > 0 ? (
              categories.map((c) => (
                <span
                  key={c.id}
                  className="badge px-2.5 py-1"
                  style={{
                    backgroundColor: `${getCodeColor(c.code)}18`,
                    color: getCodeColor(c.code),
                    border: `1px solid ${getCodeColor(c.code)}40`,
                  }}
                >
                  {c.name}
                </span>
              ))
            ) : (
              <span className="badge bg-primary-subtle text-primary border border-primary-subtle px-2.5 py-1">
                Hệ thống Khảo thí &amp; Luyện thi Trực tuyến
              </span>
            )}
          </div>
        </div>
        <Outlet />
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
