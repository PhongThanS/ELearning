import { expect, test, type Page } from "@playwright/test";
import { ApiSession, createCohort, takeExamViaApi, type Cohort } from "./support/api";
import { ADMIN_PASSWORD, ADMIN_USER, uniqueId } from "./support/env";
import { loginUi } from "./support/ui";

/**
 * Luồng admin (docs/08-kiem-thu.md mục 6): tạo danh mục → tạo 4 loại câu hỏi → tạo đề → thêm câu → cấu hình
 * → gán nhóm → xem trước → publish → (học viên thi) → xem kết quả → export Excel
 * → sửa đáp án một câu → điểm học viên được cập nhật.
 */
test.describe("admin quản lý đề thi", () => {
  let cohort: Cohort;
  const tag = uniqueId("");

  test.beforeAll(async () => {
    const admin = await ApiSession.admin();
    cohort = await createCohort(admin);
    await admin.dispose();
  });

  test("tạo đề từ đầu, publish, xem kết quả, sửa đáp án và chấm lại", async ({ page }) => {
    test.setTimeout(180_000);
    await loginUi(page, ADMIN_USER, ADMIN_PASSWORD);
    await expect(page).toHaveURL(/\/admin/);

    // 1. Danh mục
    const categoryName = `Danh mục ${tag}`;
    await page.goto("/admin/categories");
    await page.getByRole("button", { name: "Tạo mới" }).click();
    const categoryDialog = page.getByRole("dialog");
    await categoryDialog.getByLabel(/^Mã/).fill(`CAT-${tag}`);
    await categoryDialog.getByLabel(/^Tên/).fill(categoryName);
    await categoryDialog.getByRole("button", { name: "Lưu" }).click();
    await expect(page.getByRole("cell", { name: categoryName })).toBeVisible();

    // 2. Bốn loại câu hỏi
    await createQuestion(page, {
      code: `Q${tag}-1`,
      category: categoryName,
      content: `[${tag}] Ngôn ngữ nào chạy trên .NET?`,
      choices: ["Python thuần", "C Sharp", "Ruby thuần", "Go thuần"],
      correct: ["B"],
    });
    await createQuestion(page, {
      code: `Q${tag}-2`,
      category: categoryName,
      type: "Chọn nhiều",
      content: `[${tag}] Những số nào là số chẵn?`,
      choices: ["Số hai", "Số ba", "Số bốn", "Số năm"],
      correct: ["A", "C"],
    });
    await createQuestion(page, {
      code: `Q${tag}-3`,
      category: categoryName,
      type: "Đúng / Sai",
      content: `[${tag}] SQL Server hỗ trợ transaction.`,
      correct: ["TRUE"],
    });
    await createQuestion(page, {
      code: `Q${tag}-4`,
      category: categoryName,
      type: "Điền đáp án",
      content: `[${tag}] Thủ đô của Việt Nam?`,
      accepted: "Hà Nội",
    });

    // 3. Đề thi + cấu hình
    const examCode = `E2E-ADM-${tag}`;
    const examName = `Đề admin ${tag}`;
    await page.goto("/admin/exams");
    await page.getByRole("button", { name: "Tạo mới" }).click();
    const examDialog = page.getByRole("dialog");
    await examDialog.getByLabel(/^Mã/).fill(examCode);
    await examDialog.getByLabel(/^Tên/).fill(examName);
    await examDialog.getByLabel("Thời lượng (phút)").fill("20");
    await examDialog.getByLabel("Xem lại bài & đáp án").selectOption({ label: "Ngay sau khi nộp" });
    await examDialog.getByRole("button", { name: "Tạo mới" }).click();
    await expect(page).toHaveURL(/\/admin\/exams\/[^/]+\/versions\/[^/]+$/);
    const [, examId, versionId] = /\/admin\/exams\/([^/]+)\/versions\/([^/]+)$/.exec(page.url())!;

    // 4. Thêm câu từ ngân hàng
    await page.getByRole("search").getByLabel("Từ khóa").fill(tag);
    await page.getByRole("search").getByRole("button", { name: "Tìm kiếm" }).click();
    for (let i = 1; i <= 4; i++) {
      await page.getByLabel(`Chọn Q${tag}-${i}`).check();
    }
    await page.getByRole("button", { name: /^Thêm 4 câu/ }).click();
    await expect(page.getByRole("tab", { name: "Câu hỏi (4)" })).toBeVisible();

    // 5. Xem trước như học viên
    await page.getByRole("tab", { name: "Xem trước" }).click();
    const preview = page.getByRole("tabpanel", { name: "Xem trước" });
    await expect(preview.getByText(/Thủ đô của Việt Nam/)).toBeVisible();
    await expect(preview.getByText(/Ngôn ngữ nào chạy trên .NET/)).toBeVisible();

    // 6. Gán nhóm
    await page.goto(`/admin/exams/${examId}`);
    await expect(page.getByText("Chưa gán cho ai — không ai thấy đề này.")).toBeVisible();
    await page.getByLabel("Tìm nhóm").fill(cohort.groupCode);
    await page.getByRole("button", { name: new RegExp(`^\\+ ${cohort.groupCode}`) }).click();
    await expect(page.getByText(`Nhóm ${cohort.groupCode} (1)`)).toBeVisible();

    // 7. Kiểm tra và publish
    await page.goto(`/admin/exams/${examId}/versions/${versionId}`);
    await page.getByRole("tab", { name: "Publish" }).click();
    await page.getByRole("button", { name: "Kiểm tra" }).click();
    await expect(page.getByText("Phiên bản hợp lệ, có thể publish.")).toBeVisible();
    await page.getByRole("button", { name: "Publish", exact: true }).click();
    await page.getByRole("dialog").getByRole("button", { name: "Xác nhận" }).click();
    await expect(page.getByText("Đã publish phiên bản.")).toBeVisible();

    // 8. Học viên thi: câu 1 sai (chọn A), các câu còn lại đúng → 3/4
    await takeExamViaApi(cohort.userName, examId!, [
      { selectedOptions: ["A"] },
      { selectedOptions: ["A", "C"] },
      { selectedOptions: ["TRUE"] },
      { answerText: "ha noi" },
    ]);

    // 9. Kết quả + export Excel
    await page.goto(`/admin/exams/${examId}/results`);
    const row = page.getByRole("row", { name: new RegExp(cohort.userName) });
    await expect(row).toContainText("3 / 4");
    await expect(row).toContainText("Đạt");
    const download = page.waitForEvent("download");
    await page.getByRole("button", { name: "Xuất Excel" }).click();
    expect((await download).suggestedFilename()).toMatch(/\.xlsx$/);

    // 10. Sửa đáp án câu 1 thành A → chấm lại → 4/4
    await page.goto(`/admin/exams/${examId}/versions/${versionId}`);
    await page.getByRole("button", { name: "Sửa đáp án" }).first().click();
    const keyDialog = page.getByRole("dialog");
    await keyDialog.getByLabel("A. Python thuần").check();
    await keyDialog.getByLabel("Lý do *").fill("E2E: đáp án A cũng được chấp nhận");
    await keyDialog.getByRole("button", { name: "Lưu và chấm lại" }).click();
    await expect(keyDialog).toBeHidden();

    await page.goto(`/admin/exams/${examId}/results`);
    await expect(page.getByRole("row", { name: new RegExp(cohort.userName) })).toContainText("4 / 4");

    // Thao tác được ghi audit
    await page.goto("/admin/audit-logs");
    await expect(page.getByRole("cell", { name: "ANSWER_KEY_CORRECTED" }).first()).toBeVisible();
  });
});

interface QuestionSpec {
  code: string;
  category: string;
  type?: "Chọn nhiều" | "Đúng / Sai" | "Điền đáp án";
  content: string;
  choices?: string[];
  correct?: string[];
  accepted?: string;
}

async function createQuestion(page: Page, spec: QuestionSpec): Promise<void> {
  await page.goto("/admin/questions/create");
  await page.getByLabel("Mã").fill(spec.code);
  if (spec.type) {
    await page.getByLabel("Loại câu hỏi").selectOption({ label: spec.type });
    await page.getByRole("dialog").getByRole("button", { name: "Xác nhận" }).click();
  }
  await page.getByLabel("Danh mục").selectOption({ label: spec.category });
  await page.getByRole("textbox", { name: "Nội dung", exact: true }).fill(spec.content);

  const codes = ["A", "B", "C", "D"];
  for (const [i, choice] of (spec.choices ?? []).entries()) {
    await page.getByLabel(`Nội dung lựa chọn ${codes[i]}`).fill(choice);
  }
  if (spec.type === "Chọn nhiều") {
    // Checkbox: đặt đúng trạng thái từng lựa chọn
    for (const code of codes.slice(0, spec.choices?.length ?? 0)) {
      await page.getByLabel(`Đáp án đúng ${code}`, { exact: true }).setChecked(spec.correct?.includes(code) ?? false);
    }
  } else if (spec.correct) {
    // Radio: chọn đáp án đúng là đủ
    await page.getByLabel(`Đáp án đúng ${spec.correct[0]}`, { exact: true }).check();
  }
  if (spec.accepted) {
    await page.getByLabel("Đáp án chấp nhận 1").fill(spec.accepted);
    await page.getByLabel(/Bỏ qua dấu tiếng Việt/).check();
    // Thử đáp án ngay trên editor trước khi lưu
    await page.getByLabel("Thử đáp án").fill("ha noi");
    await expect(page.locator("#q-try").locator("..").getByText("Đúng", { exact: true })).toBeVisible();
  }

  await page.getByRole("button", { name: "Lưu", exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/questions\/[^/]+\/edit$/);
  await expect(page.getByRole("heading", { name: new RegExp(`Sửa câu hỏi ${spec.code}`) })).toBeVisible();
}
