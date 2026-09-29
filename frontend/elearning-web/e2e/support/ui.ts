import { expect, type Page } from "@playwright/test";

export async function loginUi(page: Page, userName: string, password: string): Promise<void> {
  await page.goto("/login");
  await page.getByLabel("Tên đăng nhập hoặc email").fill(userName);
  await page.getByLabel("Mật khẩu").fill(password);
  await page.getByRole("button", { name: "Đăng nhập" }).click();
  await expect(page).not.toHaveURL(/\/login/);
}

/** Huy hiệu trạng thái lưu trên thanh đầu của player. */
export function saveStatus(page: Page) {
  return page.locator("header").getByText(/^(Đã lưu|Đang lưu…|Chưa lưu — đang thử lại)$/);
}

export async function nextQuestion(page: Page): Promise<void> {
  await page.getByRole("button", { name: "Câu sau →" }).click();
}

export async function gotoQuestion(page: Page, order: number): Promise<void> {
  await page.getByRole("listitem", { name: new RegExp(`^Câu ${order},`) }).first().click();
}
