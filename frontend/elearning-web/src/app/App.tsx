import { useState } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider, createBrowserRouter } from "react-router";
import { AuthProvider } from "../features/auth/AuthContext";
import { ToastProvider } from "../components/common/Feedback";
import { ApiError } from "../services/apiClient";
import { routes } from "./routes";

const router = createBrowserRouter(routes);

function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        refetchOnWindowFocus: false,
        // Không thử lại lỗi nghiệp vụ / quyền; chỉ thử lại lỗi mạng và 5xx
        retry: (count, error) => count < 2 && (!(error instanceof ApiError) || error.status === 0 || error.status >= 500),
      },
      mutations: { retry: false },
    },
  });
}

export function App() {
  const [queryClient] = useState(createQueryClient);
  return (
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <AuthProvider>
          <RouterProvider router={router} />
        </AuthProvider>
      </ToastProvider>
    </QueryClientProvider>
  );
}
