import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes } from "react-router";
import type { ReactNode } from "react";
import { ApiError } from "../../services/apiClient";
import { AuthContext, type AuthContextValue } from "./useAuth";
import { LoginPage } from "./AuthPages";
import { RequireAuth, RequirePermission } from "./guards";

function renderWithAuth(ui: ReactNode, auth: Partial<AuthContextValue>, initialPath = "/") {
  const value: AuthContextValue = {
    status: "anonymous",
    user: null,
    login: vi.fn(),
    logout: vi.fn(),
    applySession: vi.fn(),
    hasPermission: () => false,
    hasRole: () => false,
    ...auth,
  };
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <AuthContext.Provider value={value}>
        <MemoryRouter initialEntries={[initialPath]}>{ui}</MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>,
  );
}

const user = {
  id: "u1",
  userName: "student01",
  email: "s@test.vn",
  fullName: "Học Viên",
  roles: ["STUDENT"],
  permissions: [],
  mustChangePassword: false,
};

describe("LoginPage", () => {
  it("báo lỗi validation khi để trống", async () => {
    renderWithAuth(<LoginPage />, {});
    await userEvent.click(screen.getByRole("button", { name: "Đăng nhập" }));
    expect(await screen.findByText("Vui lòng nhập tên đăng nhập.")).toBeInTheDocument();
  });

  it("hiển thị thông điệp theo mã lỗi INVALID_CREDENTIALS", async () => {
    const login = vi.fn().mockRejectedValue(new ApiError(401, "INVALID_CREDENTIALS", "x", [], null));
    renderWithAuth(<LoginPage />, { login });

    await userEvent.type(screen.getByLabelText("Tên đăng nhập hoặc email"), "student01");
    await userEvent.type(screen.getByLabelText("Mật khẩu"), "sai");
    await userEvent.click(screen.getByRole("button", { name: "Đăng nhập" }));

    expect(login).toHaveBeenCalledWith("student01", "sai");
    expect(await screen.findByText("Tên đăng nhập hoặc mật khẩu không đúng.")).toBeInTheDocument();
  });
});

describe("route guards", () => {
  const routes = (
    <Routes>
      <Route path="/login" element={<div>trang đăng nhập</div>} />
      <Route path="/change-password" element={<div>đổi mật khẩu</div>} />
      <Route element={<RequireAuth />}>
        <Route path="/secret" element={<div>nội dung bí mật</div>} />
        <Route path="/admin" element={<RequirePermission permission="User.View"><div>quản trị</div></RequirePermission>} />
      </Route>
    </Routes>
  );

  it("chưa đăng nhập → chuyển tới /login", () => {
    renderWithAuth(routes, { status: "anonymous" }, "/secret");
    expect(screen.getByText("trang đăng nhập")).toBeInTheDocument();
  });

  it("phải đổi mật khẩu → chuyển tới /change-password", () => {
    renderWithAuth(routes, { status: "authenticated", user: { ...user, mustChangePassword: true } }, "/secret");
    expect(screen.getByText("đổi mật khẩu")).toBeInTheDocument();
  });

  it("thiếu permission → báo không có quyền", () => {
    renderWithAuth(routes, { status: "authenticated", user }, "/admin");
    expect(screen.getByText("Bạn không có quyền truy cập trang này.")).toBeInTheDocument();
  });

  it("đủ quyền → hiển thị trang", () => {
    renderWithAuth(routes, { status: "authenticated", user, hasPermission: (p) => p === "User.View" }, "/admin");
    expect(screen.getByText("quản trị")).toBeInTheDocument();
  });
});
