import { expect, test } from "@playwright/test";
import { ApiSession, createCohort, createPublishedExam } from "./support/api";
import { STUDENT_PASSWORD } from "./support/env";
import { loginUi } from "./support/ui";

/** Hết giờ (docs/08-kiem-thu.md mục 6): đề 1 phút, bắt đầu rồi không làm gì → lượt thi tự nộp, hiện kết quả. */
test("hết giờ thì tự nộp bài và hiển thị kết quả", async ({ page }) => {
  test.setTimeout(240_000);
  const admin = await ApiSession.admin();
  const cohort = await createCohort(admin);
  const exam = await createPublishedExam(admin, cohort.groupId, { durationMinutes: 1 });
  await admin.dispose();

  await loginUi(page, cohort.userName, STUDENT_PASSWORD);
  await page.goto(`/student/exams/${exam.id}`);
  await page.getByRole("button", { name: "Bắt đầu làm bài" }).click();
  await page.getByRole("dialog").getByRole("button", { name: "Xác nhận" }).click();
  await expect(page).toHaveURL(/\/student\/attempts\//);

  // Hết giờ: player tự nộp (trong thời gian ân hạn nên là SUBMITTED — docs/02-nghiep-vu.md) rồi chuyển sang kết quả
  await expect(page).toHaveURL(/\/student\/results\//, { timeout: 120_000 });
  await expect(page.getByText(/Thời gian làm bài: 01:0\d/)).toBeVisible();
  await expect(page.getByText("0 / 5", { exact: true })).toBeVisible();
  await expect(page.getByText("Chưa đạt", { exact: true })).toBeVisible();

  // Lịch sử ghi nhận lượt thi đã kết thúc
  await page.goto(`/student/exams/${exam.id}`);
  await expect(page.getByRole("cell", { name: /Tự động nộp|Đã nộp/ })).toBeVisible();
});

/**
 * Học viên đóng trình duyệt giữa giờ: job nền (AttemptExpirationWorker) tự nộp sau khi hết giờ + ân hạn.
 * Backend E2E chạy job mỗi 5 giây (e2e/scripts/start-api.mjs).
 */
test("đóng trình duyệt giữa giờ thì job nền tự nộp bài", async ({ page }) => {
  test.setTimeout(240_000);
  const admin = await ApiSession.admin();
  const cohort = await createCohort(admin);
  const exam = await createPublishedExam(admin, cohort.groupId, { durationMinutes: 1 });
  await admin.dispose();

  const student = await ApiSession.login(cohort.userName, STUDENT_PASSWORD);
  const attempt = await student.call<{ attemptId: string }>("POST", `/api/student/exams/${exam.id}/start`);
  const status = () =>
    student.call<{ status: string; submitReason: string | null }>("GET", `/api/student/attempts/${attempt.attemptId}/result`).then(
      (r) => `${r.status}/${r.submitReason}`,
      () => "IN_PROGRESS",
    );
  // 60 giây làm bài + 30 giây ân hạn + chu kỳ quét
  await expect.poll(status, { timeout: 150_000, intervals: [5_000] }).toBe("AUTO_SUBMITTED/TIME_EXPIRED");
  await student.dispose();

  await loginUi(page, cohort.userName, STUDENT_PASSWORD);
  await page.goto(`/student/results/${attempt.attemptId}`);
  await expect(page.getByText(/Tự động nộp|Hết giờ/).first()).toBeVisible();
  await expect(page.getByText("0 / 5", { exact: true })).toBeVisible();
});
