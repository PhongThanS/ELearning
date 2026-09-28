import { createContext, useContext } from "react";
import { ApiError } from "../../services/apiClient";
import { describeError } from "../../utils/errors";

export type ToastVariant = "success" | "danger" | "warning" | "info";

export const ToastContext = createContext<{ show: (text: string, variant?: ToastVariant) => void } | null>(null);

export function useToast() {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error("useToast phải nằm trong ToastProvider");
  }
  return {
    success: (text: string) => context.show(text, "success"),
    error: (error: unknown) =>
      context.show(error instanceof ApiError || error instanceof Error ? describeError(error) : String(error), "danger"),
    info: (text: string) => context.show(text, "info"),
  };
}
