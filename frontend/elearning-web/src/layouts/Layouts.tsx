import { Button, Container, Nav, Navbar, NavDropdown } from "react-bootstrap";
import { NavLink, Outlet, useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import { Permissions, StudentRole } from "../constants/permissions";
import { useAuth } from "../features/auth/useAuth";

function UserMenu() {
  const { t } = useTranslation();
  const { user, logout, hasRole } = useAuth();
  const navigate = useNavigate();
  if (!user) {
    return null;
  }
  return (
    <NavDropdown title={user.fullName} align="end" id="user-menu">
      {user.permissions.length > 0 && (
        <NavDropdown.Item as={NavLink} to="/admin">
          {t("nav.admin")}
        </NavDropdown.Item>
      )}
      {hasRole(StudentRole) && (
        <NavDropdown.Item as={NavLink} to="/student/exams">
          {t("nav.student")}
        </NavDropdown.Item>
      )}
      <NavDropdown.Item as={NavLink} to="/profile">
        {t("nav.profile")}
      </NavDropdown.Item>
      <NavDropdown.Divider />
      <NavDropdown.Item
        as="button"
        onClick={async () => {
          await logout();
          navigate("/login", { replace: true });
        }}
      >
        {t("nav.logout")}
      </NavDropdown.Item>
    </NavDropdown>
  );
}

const adminNav: { to: string; label: string; permission: string }[] = [
  { to: "/admin/dashboard", label: "nav.dashboard", permission: Permissions.ReportView },
  { to: "/admin/exams", label: "nav.exams", permission: Permissions.ExamView },
  { to: "/admin/questions", label: "nav.questions", permission: Permissions.QuestionView },
  { to: "/admin/categories", label: "nav.categories", permission: Permissions.CategoryView },
  { to: "/admin/users", label: "nav.users", permission: Permissions.UserView },
  { to: "/admin/groups", label: "nav.groups", permission: Permissions.GroupView },
  { to: "/admin/roles", label: "nav.roles", permission: Permissions.RoleView },
  { to: "/admin/audit-logs", label: "nav.auditLogs", permission: Permissions.AuditView },
];

export function AdminLayout() {
  const { t } = useTranslation();
  const { hasPermission } = useAuth();
  const items = adminNav.filter((item) => hasPermission(item.permission));

  return (
    <>
      <Navbar bg="dark" data-bs-theme="dark" expand="lg" className="mb-0">
        <Container fluid>
          <Navbar.Brand as={NavLink} to="/admin">
            {t("app.name")} · {t("nav.admin")}
          </Navbar.Brand>
          <Navbar.Toggle aria-controls="admin-nav" />
          <Navbar.Collapse id="admin-nav">
            <Nav className="me-auto d-lg-none">
              {items.map((item) => (
                <Nav.Link key={item.to} as={NavLink} to={item.to}>
                  {t(item.label)}
                </Nav.Link>
              ))}
            </Nav>
            <Nav className="ms-auto">
              <UserMenu />
            </Nav>
          </Navbar.Collapse>
        </Container>
      </Navbar>
      <div className="d-flex">
        <nav className="admin-sidebar d-none d-lg-block border-end bg-body-tertiary p-2" aria-label="Menu quản trị">
          <Nav className="flex-column" variant="pills">
            {items.map((item) => (
              <Nav.Link key={item.to} as={NavLink} to={item.to}>
                {t(item.label)}
              </Nav.Link>
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
  return (
    <>
      <Navbar bg="primary" data-bs-theme="dark" expand="md">
        <Container>
          <Navbar.Brand as={NavLink} to="/student/exams">
            {t("app.name")}
          </Navbar.Brand>
          <Navbar.Toggle aria-controls="student-nav" />
          <Navbar.Collapse id="student-nav">
            <Nav className="me-auto">
              <Nav.Link as={NavLink} to="/student/exams">
                {t("nav.studentExams")}
              </Nav.Link>
              <Nav.Link as={NavLink} to="/student/history">
                {t("nav.history")}
              </Nav.Link>
            </Nav>
            <Nav>
              <UserMenu />
            </Nav>
          </Navbar.Collapse>
        </Container>
      </Navbar>
      <Container as="main" className="py-3">
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
  const { t } = useTranslation();
  return (
    <div className="min-vh-100 d-flex align-items-center bg-body-tertiary">
      <Container style={{ maxWidth: 440 }}>
        <h1 className="h3 text-center mb-1">{t("app.name")}</h1>
        <p className="text-center text-secondary mb-4">{t("app.tagline")}</p>
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
