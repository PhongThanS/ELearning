import { Suspense, lazy, type ComponentType, type ReactNode } from "react";
import { Navigate, type RouteObject } from "react-router";
import { Permissions } from "../constants/permissions";
import { AdminLayout, PlayerLayout, PublicLayout, StudentLayout } from "../layouts/Layouts";
import { ChangePasswordPage, ForgotPasswordPage, LoginPage, ProfilePage, RegisterPage } from "../features/auth/AuthPages";
import { AdminHome, HomeRedirect, RequireAuth, RequirePermission, RequireStudent } from "../features/auth/guards";
import { Loading } from "../components/common/Feedback";

/** Tải trang theo route (tách bundle admin / học viên). Module xuất nhiều trang theo tên. */
function page<M>(loader: () => Promise<M>, name: keyof M): ReactNode {
  const Component = lazy(() => loader().then((m) => ({ default: m[name] as ComponentType })));
  return (
    <Suspense fallback={<Loading />}>
      <Component />
    </Suspense>
  );
}

const identity = () => import("../features/admin/IdentityPages");
const questions = () => import("../features/questions/QuestionPages");
const exams = () => import("../features/exams/ExamPages");
const builder = () => import("../features/exams/VersionBuilderPage");
const results = () => import("../features/exams/ResultPages");
const dashboard = () => import("../features/admin/DashboardPage");
const student = () => import("../features/attempts/StudentPages");
const player = () => import("../features/attempts/ExamPlayerPage");

const guard = (permission: string, element: ReactNode) => <RequirePermission permission={permission}>{element}</RequirePermission>;

/** Route theo docs/06-frontend.md mục 1. Bảo vệ route chỉ phục vụ UX; backend luôn kiểm tra quyền. */
export const routes: RouteObject[] = [
  {
    element: <PublicLayout />,
    children: [
      { path: "/login", element: <LoginPage /> },
      { path: "/register", element: <RegisterPage /> },
      { path: "/forgot-password", element: <ForgotPasswordPage /> },
    ],
  },
  {
    element: <RequireAuth />,
    children: [
      { path: "/", element: <HomeRedirect /> },
      {
        element: <StudentLayout />,
        children: [
          { path: "/change-password", element: <ChangePasswordPage /> },
          { path: "/profile", element: <ProfilePage /> },
        ],
      },
      {
        path: "/student",
        element: <RequireStudent />,
        children: [
          {
            element: <StudentLayout />,
            children: [
              { index: true, element: <Navigate to="/student/exams" replace /> },
              { path: "exams", element: page(student, "StudentExamsPage") },
              { path: "exams/:examId", element: page(student, "StudentExamDetailPage") },
              { path: "results/:attemptId", element: page(student, "ResultPage") },
              { path: "history", element: page(student, "HistoryPage") },
            ],
          },
          { element: <PlayerLayout />, children: [{ path: "attempts/:attemptId", element: page(player, "ExamPlayerPage") }] },
        ],
      },
      {
        path: "/admin",
        element: <AdminLayout />,
        children: [
          { index: true, element: <AdminHome /> },
          { path: "dashboard", element: guard(Permissions.ReportView, page(dashboard, "DashboardPage")) },
          { path: "users", element: guard(Permissions.UserView, page(identity, "UsersPage")) },
          { path: "users/:id", element: guard(Permissions.UserView, page(identity, "UserDetailPage")) },
          { path: "groups", element: guard(Permissions.GroupView, page(identity, "GroupsPage")) },
          { path: "groups/:id", element: guard(Permissions.GroupView, page(identity, "GroupDetailPage")) },
          { path: "roles", element: guard(Permissions.RoleView, page(identity, "RolesPage")) },
          { path: "categories", element: guard(Permissions.CategoryView, page(questions, "CategoriesPage")) },
          { path: "questions", element: guard(Permissions.QuestionView, page(questions, "QuestionsPage")) },
          { path: "questions/create", element: guard(Permissions.QuestionCreate, page(questions, "QuestionEditorPage")) },
          { path: "questions/:id/edit", element: guard(Permissions.QuestionView, page(questions, "QuestionEditorPage")) },
          { path: "exams", element: guard(Permissions.ExamView, page(exams, "ExamsPage")) },
          { path: "exams/:id", element: guard(Permissions.ExamView, page(exams, "ExamDetailPage")) },
          { path: "exams/:id/versions/:versionId", element: guard(Permissions.ExamView, page(builder, "VersionBuilderPage")) },
          { path: "exams/:id/results", element: guard(Permissions.ResultView, page(results, "ExamResultsPage")) },
          { path: "attempts/:attemptId", element: guard(Permissions.AttemptView, page(results, "AttemptAdminPage")) },
          { path: "audit-logs", element: guard(Permissions.AuditView, page(results, "AuditLogsPage")) },
        ],
      },
    ],
  },
  { path: "*", element: <Navigate to="/" replace /> },
];
