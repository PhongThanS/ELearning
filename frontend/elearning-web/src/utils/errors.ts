import { toApiError } from "../services/apiClient";
import { errorMessage } from "../i18n";

/** Thông điệp tiếng Việt cho một lỗi bất kỳ (ưu tiên bản dịch theo mã lỗi API). */
export function describeError(error: unknown): string {
  const apiError = toApiError(error);
  return errorMessage(apiError.code, apiError.message);
}
