import { expect, test } from "@playwright/test";
import { ApiSession, createCohort, createPublishedExam, type Cohort, type ExamDetail } from "./support/api";
import { STUDENT_PASSWORD } from "./support/env";
import { gotoQuestion, loginUi, nextQuestion, saveStatus } from "./support/ui";

/**
 * Luồng học viên (docs/08-kiem-thu.md mục 6): thấy đề được gán → bắt đầu → trả lời đủ 4 loại câu
 * → tải lại trang vẫn còn → mất mạng vẫn giữ trên máy, có mạng thì lưu → nộp → xem kết quả.
 */
test.describe("học viên làm bài", () => {
  let cohort: Cohort;
  let exam: ExamDetail;

  test.beforeAll(async () => {
    const admin = await ApiSession.admin();
    cohort = await createCohort(admin);
    exam = await createPublishedExam(admin, cohort.groupId);
    await admin.dispose();
  });

  test("làm bài, tải lại, mất mạng, nộp bài và xem kết quả", async ({ page, context }) => {
    await loginUi(page, cohort.userName, STUDENT_PASSWORD);

    // Chỉ thấy đề được gán cho nhóm của mình
    await expect(page).toHaveURL(/\/student\/exams$/);
    const card = page.locator(".card", { hasText: exam.name });
    await expect(card).toBeVisible();
    await expect(page.getByText("C# Basic")).toHaveCount(0);
    await card.getByRole("link", { name: /Bắt đầu làm bài|Chi tiết/ }).click();

    await page.getByRole("button", { name: "Bắt đầu làm bài" }).click();
    await page.getByRole("dialog").getByRole("button", { name: "Xác nhận" }).click();
    await expect(page).toHaveURL(/\/student\/attempts\//);
    await expect(page.getByLabel("Còn lại")).toBeVisible();

    // Câu 1 — chọn một; câu 2 — chọn nhiều
    await page.getByRole("radio", { name: /C Sharp/ }).check();
    await nextQuestion(page);
    await page.getByRole("checkbox", { name: /Số hai/ }).check();
    await page.getByRole("checkbox", { name: /Số bốn/ }).check();
    await expect(saveStatus(page)).toHaveText("Đã lưu");

    // Tải lại trang: câu trả lời lấy lại từ server
    await page.reload();
    await gotoQuestion(page, 1);
    await expect(page.getByRole("radio", { name: /C Sharp/ })).toBeChecked();
    await gotoQuestion(page, 2);
    await expect(page.getByRole("checkbox", { name: /Số hai/ })).toBeChecked();
    await expect(page.getByRole("checkbox", { name: /Số bốn/ })).toBeChecked();
    await expect(page.getByRole("checkbox", { name: /Số ba/ })).not.toBeChecked();

    // Câu 3 — đúng/sai, trả lời lúc mất mạng: giữ trên máy, có mạng lại thì tự lưu
    await nextQuestion(page);
    await context.setOffline(true);
    await page.getByRole("radio", { name: /Đúng/ }).check();
    await expect(page.getByText("Mất kết nối — câu trả lời được giữ trên máy")).toBeVisible();
    await expect(saveStatus(page)).toHaveText("Chưa lưu — đang thử lại");
    await context.setOffline(false);
    await expect(saveStatus(page)).toHaveText("Đã lưu", { timeout: 30_000 });

    // Câu 4 — điền chữ (không dấu vẫn đúng); câu 5 — điền số kiểu Việt Nam
    await nextQuestion(page);
    await page.getByPlaceholder("Nhập câu trả lời").fill("ha noi");
    await nextQuestion(page);
    const numberInput = page.getByPlaceholder("Nhập câu trả lời");
    await numberInput.fill("1,000.5");
    await expect(page.getByText(/Số không hợp lệ/)).toBeVisible();
    await numberInput.fill("3,5");
    await expect(page.getByText(/Số không hợp lệ/)).toBeHidden();
    await expect(page.getByText("Đã trả lời: 5/5")).toBeVisible();

    // Nộp bài
    await page.getByRole("button", { name: "Nộp bài" }).first().click();
    const confirm = page.getByRole("dialog");
    await expect(confirm.getByText("Sau khi nộp bạn không thể sửa câu trả lời.")).toBeVisible();
    await confirm.getByRole("button", { name: "Nộp bài" }).click();

    // Kết quả: điểm công bố ngay, được xem lại bài
    await expect(page).toHaveURL(/\/student\/results\//);
    await expect(page.getByRole("heading", { name: "Kết quả bài thi" })).toBeVisible();
    await expect(page.getByText("5 / 5", { exact: true })).toBeVisible();
    await expect(page.getByText("Đạt", { exact: true })).toBeVisible();
    await expect(page.getByText(/Thủ đô của Việt Nam/)).toBeVisible();

    // Không được bắt đầu lại khi đã hết lượt
    await page.goto(`/student/exams/${exam.id}`);
    await expect(page.getByText("Đã hết lượt")).toBeVisible();
    await expect(page.getByRole("button", { name: "Bắt đầu làm bài" })).toHaveCount(0);
  });
});
