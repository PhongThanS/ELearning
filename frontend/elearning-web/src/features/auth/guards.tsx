import type { ReactNode } from "react";
import { Navigate, Outlet, useLocation } from "react-router";
import { Alert } from "react-bootstrap";
import { useTranslation } from "react-i18next";
import { Loading } from "../../components/common/Feedback";
import { Permissions, StudentRole } from "../../constants/permissions";
import { useAuth } from "./useAuth";

/**
 * Bảo vệ route chỉ phục vụ UX; backend luôn kiểm tra quyền (docs/06-frontend.md mục 1).
 * mustChangePassword → mọi route khác chuyển về /change-password.
 */
export function RequireAuth({ children }: { children?: ReactNode }) {
  const { status, user } = useAuth();
  const location = useLocation();

  if (status === "loading") {
    return <Loading />;
  }
  if (status === "anonymous" || !user) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  }
  if (user.mustChangePassword && location.pathname !== "/change-password") {
    return <Navigate to="/change-password" replace />;
  }
  return children ?? <Outlet />;
}

export function RequirePermission({ permission, children }: { permission: string; children?: ReactNode }) {
  const { hasPermission } = useAuth();
  return hasPermission(permission) ? (children ?? <Outlet />) : <Forbidden />;
}

export function RequireStudent({ children }: { children?: ReactNode }) {
  const { hasRole } = useAuth();
  return hasRole(StudentRole) ? (children ?? <Outlet />) : <Forbidden />;
}

export function Forbidden() {
  const { t } = useTranslation();
  return (
    <Alert variant="warning" className="m-3">
      {t("auth.forbidden")}
    </Alert>
  );
}

/** Trang admin mặc định: mục đầu tiên người dùng có quyền. */
export function AdminHome() {
  const { hasPermission } = useAuth();
  const first = [
    [Permissions.ReportView, "/admin/dashboard"],
    [Permissions.ExamView, "/admin/exams"],
    [Permissions.QuestionView, "/admin/questions"],
    [Permissions.UserView, "/admin/users"],
  ].find(([permission]) => hasPermission(permission!));
  return <Navigate to={first?.[1] ?? "/profile"} replace />;
}

/** Trang gốc: chuyển tới khu vực phù hợp với người dùng. */
export function HomeRedirect() {
  const { status, user, hasRole } = useAuth();
  if (status === "loading") {
    return <Loading />;
  }
  if (!user) {
    return <Navigate to="/login" replace />;
  }
  if (user.permissions.length > 0) {
    return <Navigate to="/admin" replace />;
  }
  return <Navigate to={hasRole(StudentRole) ? "/student/exams" : "/profile"} replace />;
}
