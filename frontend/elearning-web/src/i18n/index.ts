import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import vi from "./vi.json";

// Mọi chuỗi hiển thị nằm trong i18n/*.json (docs/06-frontend.md mục 7). Mặc định tiếng Việt.
void i18n.use(initReactI18next).init({
  resources: { vi: { translation: vi } },
  lng: "vi",
  fallbackLng: "vi",
  interpolation: { escapeValue: false },
  returnNull: false,
});

export default i18n;

/** Thông điệp tiếng Việt cho mã lỗi API; không có bản dịch thì dùng message của server. */
export function errorMessage(code: string, fallback: string): string {
  const key = `errors.${code}`;
  return i18n.exists(key) ? i18n.t(key) : fallback;
}
