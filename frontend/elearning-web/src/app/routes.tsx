import { Suspense, lazy, type ComponentType, type ReactNode } from "react";
import { Navigate, type RouteObject } from "react-router";
import { Permissions } from "../constants/permissions";
import { Features } from "../constants/features";
import { AdminLayout, PlayerLayout, PublicLayout, StudentLayout } from "../layouts/Layouts";
import { ChangePasswordPage, ForgotPasswordPage, LoginPage, ProfilePage, RegisterPage } from "../features/auth/AuthPages";
import { AdminHome, HomeRedirect, RequireAuth, RequirePermission, RequireStudent } from "../features/auth/guards";
import { Loading } from "../components/common/Feedback";
import { withChunkReload } from "./chunkReload";
import { pageLoaders } from "./pageLoaders";

/** Tải trang theo route (tách bundle admin / học viên). Module xuất nhiều trang theo tên. File trang cũ sau deploy: tự tải lại. */
function page<M>(loader: () => Promise<M>, name: keyof M): ReactNode {
  const Component = lazy(() => withChunkReload(loader)().then((m) => ({ default: m[name] as ComponentType })));
  return (
    <Suspense fallback={<Loading />}>
      <Component />
    </Suspense>
  );
}

const {
  identity,
  classes,
  questions,
  markdownImport,
  exams,
  examPreview,
  builder,
  results,
  dashboard,
  student,
  player,
  videos,
  studentVideos,
  studentAttendance,
  videoWatch,
  attendance,
} = pageLoaders;

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
              { index: true, element: <Navigate to="/student/attendance" replace /> },
              { path: "attendance", element: page(studentAttendance, "StudentAttendancePage") },
              { path: "exams", element: page(student, "StudentExamsPage") },
              { path: "exams/:examId", element: page(student, "StudentExamDetailPage") },
              { path: "results/:attemptId", element: page(student, "ResultPage") },
              { path: "history", element: page(student, "HistoryPage") },
              { path: "classes", element: page(student, "MyClassesPage") },
              { path: "classes/:classId", element: page(student, "MyClassDetailPage") },
              { path: "videos", element: page(studentVideos, "StudentVideoPages") },
            ],
          },
          { element: <PlayerLayout />, children: [{ path: "attempts/:attemptId", element: page(player, "ExamPlayerPage") }] },
          // Xem bài giảng toàn màn hình, mở ở tab mới
          { element: <PlayerLayout />, children: [{ path: "videos/:id/watch", element: page(videoWatch, "StudentVideoWatchPage") }] },
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
          // Nhóm người dùng đang ẩn (Features.userGroups): đường dẫn cũ quay về trang quản trị
          ...(Features.userGroups
            ? [
                { path: "groups", element: guard(Permissions.GroupView, page(identity, "GroupsPage")) },
                { path: "groups/:id", element: guard(Permissions.GroupView, page(identity, "GroupDetailPage")) },
              ]
            : [{ path: "groups/*", element: <Navigate to="/admin" replace /> }]),
          { path: "classes", element: guard(Permissions.ClassView, page(classes, "ClassesPage")) },
          { path: "classes/:id", element: guard(Permissions.ClassView, page(classes, "ClassDetailPage")) },
          { path: "roles", element: guard(Permissions.RoleView, page(identity, "RolesPage")) },
          { path: "categories", element: guard(Permissions.CategoryView, page(questions, "CategoriesPage")) },
          { path: "questions", element: guard(Permissions.QuestionView, page(questions, "QuestionsPage")) },
          { path: "questions/import-markdown", element: guard(Permissions.QuestionCreate, page(markdownImport, "MarkdownImportPage")) },
          { path: "questions/create", element: guard(Permissions.QuestionCreate, page(questions, "QuestionEditorPage")) },
          { path: "questions/:id/edit", element: guard(Permissions.QuestionView, page(questions, "QuestionEditorPage")) },
          { path: "exams", element: guard(Permissions.ExamView, page(exams, "ExamsPage")) },
          { path: "exams/:id", element: guard(Permissions.ExamView, page(exams, "ExamDetailPage")) },
          { path: "exams/:id/preview", element: guard(Permissions.ExamView, page(examPreview, "ExamPreviewPage")) },
          { path: "exams/:id/versions/:versionId", element: guard(Permissions.ExamView, page(builder, "VersionBuilderPage")) },
          { path: "exams/:id/results", element: guard(Permissions.ResultView, page(results, "ExamResultsPage")) },
          { path: "attempts/:attemptId", element: guard(Permissions.AttemptView, page(results, "AttemptAdminPage")) },
          { path: "audit-logs", element: guard(Permissions.AuditView, page(results, "AuditLogsPage")) },
          { path: "videos", element: guard(Permissions.VideoView, page(videos, "AdminVideoPages")) },
          { path: "attendance", element: guard(Permissions.AttendanceTake, page(attendance, "AttendancePage")) },
        ],
      },
      {
        // Xem bài giảng toàn màn hình (không có menu admin), mở ở tab mới
        element: <PlayerLayout />,
        children: [
          { path: "/admin/videos/:id/watch", element: guard(Permissions.VideoView, page(videoWatch, "AdminVideoWatchPage")) },
        ],
      },
    ],
  },
  { path: "*", element: <Navigate to="/" replace /> },
];
