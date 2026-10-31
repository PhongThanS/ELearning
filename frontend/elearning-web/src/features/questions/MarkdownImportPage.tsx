import { useMemo, useState } from "react";
import { Alert, Badge, Button, Card, Col, Form, Row, Spinner, Table } from "react-bootstrap";
import { Link, useNavigate } from "react-router";
import { useQuery } from "@tanstack/react-query";
import { categoriesApi, examsApi, mediaApi, questionsApi } from "../../services/api";
import { PageHeader } from "../../components/common/DataTable";
import { MarkdownView } from "../../components/common/MarkdownView";
import { useToast } from "../../components/common/toast";
import { describeError } from "../../utils/errors";
import { formatNumber } from "../../utils/format";
import type { DocxSkippedImage, MarkdownImportQuestion, MarkdownImportResult, MediaUrlMap } from "../../types/api";
import { newPendingId, readAsDataUrl, referencedPending, replacePendingIds, validateImage, type PendingImage } from "./pendingImages";
import { normalizeMarkdown, questionText, replaceQuestion, setQuestionDifficulty, type Difficulty } from "./markdownBlocks";

const DIFFICULTY_LABEL: Record<Difficulty, string> = { EASY: "Dễ", MEDIUM: "Trung bình", HARD: "Khó" };

const TYPE_LABEL: Record<string, string> = {
  SINGLE_CHOICE: "Chọn một",
  MULTIPLE_CHOICE: "Chọn nhiều",
  TRUE_FALSE: "Đúng / Sai",
  FILL_IN: "Trả lời ngắn",
  ESSAY: "Tự luận",
};

const IMAGE_REF = /(!\[[^\]]*\]\()([^)\s]+)((?:\s+"[^"]*")?\))/g;

/** Tên file trong tham chiếu ảnh (bỏ thư mục, chữ thường) để khớp với ảnh người dùng chọn. */
function baseName(path: string): string {
  return (path.split(/[\\/]/).pop() ?? path).toLowerCase();
}

type ImportImage = PendingImage & { name: string; fromDocx?: boolean };

/** Tên file ảnh (chữ thường) mà file .md tham chiếu. */
function referencedNames(markdown: string): Set<string> {
  return new Set([...markdown.matchAll(IMAGE_REF)].map((m) => baseName(m[2] ?? "")));
}

function fileFromBase64(name: string, type: string, base64: string): File {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return new File([bytes], name, { type });
}

function isDocx(file: File): boolean {
  return file.name.toLowerCase().endsWith(".docx");
}

/** Thay ![..](hinh-1.png) bằng ![..](media:pending-x) cho các ảnh đã chọn; tên chưa có ảnh giữ nguyên. */
function attachImages(markdown: string, images: readonly ImportImage[]): string {
  const byName = new Map(images.map((i) => [i.name, i.id]));
  return markdown.replace(IMAGE_REF, (whole, open: string, target: string, close: string) => {
    if (target.toLowerCase().startsWith("media:")) {
      return whole;
    }
    const id = byName.get(baseName(target));
    return id ? `${open}media:${id}${close}` : whole;
  });
}

/**
 * Nhập đề từ file Markdown "elearning-exam/1" (docs/02-nghiep-vu.md mục 1.5, D-29). Chọn file → server đọc và kiểm tra
 * (dryRun) → xem trước đúng như học viên thấy → nhập. Ảnh chỉ tải lên khi bấm nhập; có thể tạo sẵn đề thi nháp.
 */
export function MarkdownImportPage() {
  const toast = useToast();
  const navigate = useNavigate();
  const [fileName, setFileName] = useState<string | null>(null);
  const [markdown, setMarkdown] = useState<string>("");
  const [images, setImages] = useState<ImportImage[]>([]);
  const [docx, setDocx] = useState<{ fileName: string; skipped: DocxSkippedImage[] } | null>(null);
  const [categoryId, setCategoryId] = useState<string>("");
  const [createExam, setCreateExam] = useState(true);
  const [result, setResult] = useState<MarkdownImportResult | null>(null);
  const [busy, setBusy] = useState<"check" | "import" | "docx" | null>(null);
  const [error, setError] = useState<unknown>(null);

  const categories = useQuery({
    queryKey: ["categories", "import-markdown"],
    queryFn: () => categoriesApi.list({ page: 1, pageSize: 100, isActive: true, sortBy: "code" }),
  });

  const media = useMemo<MediaUrlMap>(() => Object.fromEntries(images.map((i) => [i.id, i.previewUrl])), [images]);

  const check = async (text: string, imgs: typeof images, category: string) => {
    if (!text) {
      return;
    }
    setBusy("check");
    setError(null);
    try {
      setResult(await questionsApi.importMarkdown({ markdown: attachImages(text, imgs), categoryId: category || null, dryRun: true }));
    } catch (e) {
      setResult(null);
      setError(e);
    } finally {
      setBusy(null);
    }
  };

  const onFile = async (file: File | undefined) => {
    if (!file) {
      return;
    }
    const text = normalizeMarkdown(await file.text());
    setFileName(file.name);
    setMarkdown(text);
    setEdited(false);
    await check(text, images, categoryId);
  };

  // Sửa lúc xem trước: đổi thẳng nội dung file .md rồi cho server kiểm tra lại cả file
  const [edited, setEdited] = useState(false);
  const applyEdit = async (next: string) => {
    if (next === markdown) {
      return;
    }
    setMarkdown(next);
    setEdited(true);
    await check(next, images, categoryId);
  };

  /** Đặt độ khó cho mọi câu; sửa từ câu cuối lên để số dòng của các câu phía trên không đổi. */
  const setAllDifficulty = (difficulty: Difficulty) => {
    const lines = [...(result?.questions ?? [])].map((q) => q.line).sort((a, b) => b - a);
    void applyEdit(lines.reduce((text, line) => setQuestionDifficulty(text, line, difficulty), markdown));
  };

  const downloadEdited = () => {
    const url = URL.createObjectURL(new Blob([markdown], { type: "text/markdown;charset=utf-8" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = (fileName ?? "de-thi.md").replace(/(\.[^.]+)?$/, "-da-sua.md");
    link.click();
    URL.revokeObjectURL(url);
  };

  const onImages = async (files: FileList | null) => {
    if (!files) {
      return;
    }
    const added: typeof images = [];
    const all = Array.from(files);
    const word = all.find(isDocx);
    if (word) {
      setBusy("docx");
      try {
        const extracted = await questionsApi.docxImages(word);
        const chosenByHand = new Set(images.filter((i) => !i.fromDocx).map((i) => i.name));
        for (const img of extracted.images) {
          const name = img.name.toLowerCase();
          if (!chosenByHand.has(name)) {
            const file = fileFromBase64(name, img.contentType, img.dataBase64);
            added.push({ id: newPendingId(), file, previewUrl: `data:${img.contentType};base64,${img.dataBase64}`, name, fromDocx: true });
          }
        }
        setDocx({ fileName: word.name, skipped: extracted.skipped });
      } catch (e) {
        toast.error(`${word.name}: ${describeError(e)}`);
      } finally {
        setBusy(null);
      }
    }
    for (const file of all.filter((f) => !isDocx(f))) {
      const problem = validateImage(file);
      if (problem) {
        toast.error(problem === "type" ? `${file.name}: chỉ nhận ảnh PNG, JPEG, GIF, WebP.` : `${file.name}: ảnh tối đa 2 MB.`);
        continue;
      }
      added.push({ id: newPendingId(), file, previewUrl: await readAsDataUrl(file), name: file.name.toLowerCase() });
    }
    // Chọn file Word mới thì bỏ các ảnh lấy từ file Word trước
    const keep = images.filter((i) => !(word && i.fromDocx) && !added.some((a) => a.name === i.name));
    const next = [...keep, ...added];
    setImages(next);
    await check(markdown, next, categoryId);
  };

  const canImport =
    !!result && result.fileIssues.length === 0 && result.missingImages.length === 0 && result.validQuestions === result.totalQuestions;

  const doImport = async () => {
    if (!result) {
      return;
    }
    setBusy("import");
    setError(null);
    try {
      // Chỉ ảnh còn được dùng mới tải lên; mã tạm được thay bằng id thật trước khi nhập
      const attached = attachImages(markdown, images);
      const used = referencedPending([attached], images);
      const replacements: Record<string, string> = {};
      for (const image of used) {
        replacements[image.id] = (await mediaApi.upload(image.file)).id;
      }
      const imported = await questionsApi.importMarkdown({
        markdown: replacePendingIds(attached, replacements),
        categoryId: categoryId || null,
        dryRun: false,
      });
      setResult(imported);
      if (!imported.imported) {
        toast.error("File còn lỗi, chưa nhập câu nào.");
        return;
      }
      toast.success(`Đã nhập ${imported.totalQuestions} câu hỏi.`);

      if (createExam) {
        const exam = imported.exam;
        try {
          const created = await examsApi.create({
            code: exam.code,
            name: exam.title ?? fileName?.replace(/\.[^.]+$/, "") ?? "Đề nhập từ file",
            description: exam.description,
            instructions: exam.instructions,
            startAt: null,
            endAt: null,
            maxAttempts: 1,
            accessMode: "ASSIGNED",
            retakeScoringPolicy: "HIGHEST",
            durationMinutes: exam.durationMinutes ?? 60,
            passPercentage: 50,
            scoreVisibility: "IMMEDIATE",
            reviewPolicy: "NEVER",
            categoryId: imported.categoryId,
          });
          const ids = imported.questions.map((q) => q.questionId).filter((id): id is string => !!id);
          if (created.draftVersionId) {
            await examsApi.addQuestions(created.id, created.draftVersionId, ids);
          }
          toast.success("Đã tạo đề thi nháp. Kiểm tra lại rồi publish.");
          navigate(`/admin/exams/${created.id}`);
          return;
        } catch (e) {
          toast.error(`Câu hỏi đã nhập nhưng chưa tạo được đề thi: ${describeError(e)}`);
        }
      }
      navigate(imported.categoryId ? `/admin/questions?categoryId=${imported.categoryId}` : "/admin/questions");
    } catch (e) {
      setError(e);
    } finally {
      setBusy(null);
    }
  };

  const referenced = useMemo(() => referencedNames(markdown), [markdown]);
  const docxImages = images.filter((i) => i.fromDocx);
  const unusedDocx = markdown ? docxImages.filter((i) => !referenced.has(i.name)) : [];

  const counts = useMemo(() => {
    const map = new Map<string, number>();
    for (const q of result?.questions ?? []) {
      const type = q.question?.questionType ?? "?";
      map.set(type, (map.get(type) ?? 0) + 1);
    }
    return [...map.entries()];
  }, [result]);

  return (
    <>
      <Link to="/admin/questions" className="small">← Ngân hàng câu hỏi</Link>
      <PageHeader title="Nhập đề từ file (Markdown)">
        <span className="text-secondary small">
          Đề Word / PDF / ảnh chụp → nhờ AI chuyển sang file <code>.md</code> theo hướng dẫn → nhập vào đây.
        </span>
      </PageHeader>

      <Row className="g-3 mb-3">
        <Col lg={8}>
          <Card className="h-100">
            <Card.Body>
              <Row className="g-3">
                <Col md={6}>
                  <Form.Group controlId="md-file">
                    <Form.Label className="fw-semibold">1. File đề (.md)</Form.Label>
                    <Form.Control type="file" accept=".md,.markdown,.txt,text/markdown,text/plain" onChange={(e) => void onFile((e.target as HTMLInputElement).files?.[0])} />
                    {fileName && <Form.Text>Đã chọn: {fileName}</Form.Text>}
                  </Form.Group>
                </Col>
                <Col md={6}>
                  <Form.Group controlId="md-images">
                    <Form.Label className="fw-semibold">2. Hình trong đề: file Word gốc hoặc ảnh</Form.Label>
                    <Form.Control
                      type="file"
                      accept=".docx,image/png,image/jpeg,image/gif,image/webp"
                      multiple
                      onChange={(e) => void onImages((e.target as HTMLInputElement).files)}
                    />
                    <Form.Text>
                      Chọn <strong>file Word gốc (.docx)</strong> để tự lấy hình theo câu (<code>hinh-p1-c3.png</code>…), hoặc chọn
                      ảnh có tên trùng tên ghi trong file .md.
                    </Form.Text>
                  </Form.Group>
                </Col>
                <Col md={6}>
                  <Form.Group controlId="md-category">
                    <Form.Label className="fw-semibold">Danh mục</Form.Label>
                    <Form.Select
                      value={categoryId}
                      onChange={(e) => {
                        setCategoryId(e.target.value);
                        void check(markdown, images, e.target.value);
                      }}
                    >
                      <option value="">Theo @category trong file (hoặc không gắn)</option>
                      {categories.data?.items.map((c) => (
                        <option key={c.id} value={c.id}>{c.code} — {c.name}</option>
                      ))}
                    </Form.Select>
                  </Form.Group>
                </Col>
                <Col md={6} className="d-flex align-items-end">
                  <Form.Check
                    id="md-create-exam"
                    checked={createExam}
                    onChange={(e) => setCreateExam(e.target.checked)}
                    label="Tạo sẵn đề thi nháp từ các câu này"
                  />
                </Col>
              </Row>
            </Card.Body>
          </Card>
        </Col>
        <Col lg={4}>
          <Card className="h-100 border-primary-subtle">
            <Card.Body>
              <h2 className="h6"><i className="bi bi-robot me-1" aria-hidden="true" />Chuyển đề sang file .md bằng AI</h2>
              <ol className="small ps-3 mb-3">
                <li>Tải <strong>prompt hướng dẫn</strong> bên dưới.</li>
                <li>
                  Mở Claude / ChatGPT / Gemini, dán prompt và đính kèm <strong>file PDF</strong> của đề (Word → Lưu thành → PDF) hoặc
                  ảnh chụp. <strong>Không gửi file .docx</strong>: AI không đọc được công thức MathType trong đó.
                </li>
                <li>Chỉ chép phần <strong>bên trong khối code</strong> AI trả về, lưu thành file <code>.md</code> và chọn ở bước 1.</li>
                <li>Ở bước 2 chọn file Word gốc để hệ thống tự lấy hình cho từng câu.</li>
              </ol>
              <div className="d-grid gap-2">
                <a className="btn btn-sm btn-primary" href="/huong-dan/prompt-chuyen-de-thi.md" download>
                  <i className="bi bi-download me-1" aria-hidden="true" />Tải prompt cho AI
                </a>
                <a className="btn btn-sm btn-outline-primary" href="/huong-dan/mau-de-thi.md" download>
                  <i className="bi bi-file-earmark-text me-1" aria-hidden="true" />Tải file đề mẫu (.md)
                </a>
              </div>
            </Card.Body>
          </Card>
        </Col>
      </Row>

      {busy === "docx" && <div className="text-secondary small mb-2"><Spinner size="sm" className="me-2" />Đang lấy hình từ file Word…</div>}
      {docx && (
        <Card className="mb-3">
          <Card.Body>
            <div className="fw-semibold mb-2">
              Hình lấy từ <span className="text-primary">{docx.fileName}</span>: {docxImages.length} ảnh
            </div>
            {docxImages.length > 0 && (
              <div className="d-flex flex-wrap gap-2 mb-2">
                {docxImages.map((i) => (
                  <figure key={i.id} className={`border rounded p-1 mb-0 text-center ${referenced.has(i.name) ? "border-success" : "border-warning"}`}>
                    <img src={i.previewUrl} alt={i.name} style={{ height: 64, maxWidth: 140, objectFit: "contain" }} />
                    <figcaption className="small"><code>{i.name}</code></figcaption>
                  </figure>
                ))}
              </div>
            )}
            {unusedDocx.length > 0 && (
              <Alert variant="warning" className="small py-2 mb-2">
                File Word có hình nhưng file .md không dùng:{" "}
                {unusedDocx.map((i) => <code key={i.id} className="me-1">{i.name}</code>)}— câu đó có thể đang thiếu hình. Thêm dòng{" "}
                <code>![mô tả](tên-ảnh)</code> vào đúng câu trong file .md (hoặc bỏ qua nếu là hình không cần thiết).
              </Alert>
            )}
            {docx.skipped.length > 0 && (
              <Alert variant="warning" className="small py-2 mb-0">
                <div className="fw-semibold">Không lấy được, cần chọn ảnh tay:</div>
                <ul className="mb-0 ps-3">
                  {docx.skipped.map((s, index) => (
                    <li key={index}>{s.name ? <code>{s.name}</code> : "Ảnh"}: {s.reason}</li>
                  ))}
                </ul>
              </Alert>
            )}
          </Card.Body>
        </Card>
      )}
      {busy === "check" && <div className="text-secondary small mb-2"><Spinner size="sm" className="me-2" />Đang đọc file…</div>}
      {error != null && <Alert variant="danger">{describeError(error)}</Alert>}

      {result && (
        <>
          <Card className="mb-3">
            <Card.Body className="d-flex flex-wrap align-items-center gap-3">
              <div>
                <div className="fw-semibold">{result.exam.title ?? "(chưa có @title)"}</div>
                <div className="small text-secondary">
                  {result.totalQuestions} câu · tổng {formatNumber(result.totalScore)} điểm
                  {result.exam.durationMinutes ? ` · ${result.exam.durationMinutes} phút` : ""}
                </div>
              </div>
              <div className="d-flex flex-wrap gap-1">
                {counts.map(([type, n]) => (
                  <Badge key={type} bg="info-subtle" text="dark" className="border">{TYPE_LABEL[type] ?? type}: {n}</Badge>
                ))}
              </div>
              <div className="ms-auto d-flex align-items-center gap-2">
                {canImport ? (
                  <Badge bg="success">Hợp lệ {result.validQuestions}/{result.totalQuestions}</Badge>
                ) : result.fileIssues.length === 0 && result.validQuestions === result.totalQuestions ? (
                  <Badge bg="warning" text="dark">Thiếu {result.missingImages.length} ảnh</Badge>
                ) : (
                  <Badge bg="danger">Còn lỗi — hợp lệ {result.validQuestions}/{result.totalQuestions}</Badge>
                )}
                <Button onClick={() => void doImport()} disabled={!canImport || busy !== null || result.imported}>
                  {busy === "import" ? <Spinner size="sm" className="me-1" /> : <i className="bi bi-cloud-upload me-1" aria-hidden="true" />}
                  {result.imported ? "Đã nhập" : createExam ? "Nhập và tạo đề thi" : "Nhập vào ngân hàng câu hỏi"}
                </Button>
              </div>
            </Card.Body>
          </Card>

          {!result.imported && (
            <div className="d-flex flex-wrap align-items-center gap-2 mb-3 small">
              <span className="text-secondary">
                Sửa trước khi nhập: chọn độ khó hoặc bấm <i className="bi bi-pencil" aria-hidden="true" /> ở từng câu.
              </span>
              <Form.Select
                size="sm"
                style={{ width: "auto" }}
                aria-label="Đặt độ khó cho tất cả câu"
                value=""
                disabled={busy !== null}
                onChange={(e) => e.target.value && setAllDifficulty(e.target.value as Difficulty)}
              >
                <option value="">Đặt độ khó cho tất cả câu…</option>
                {(Object.keys(DIFFICULTY_LABEL) as Difficulty[]).map((d) => (
                  <option key={d} value={d}>{DIFFICULTY_LABEL[d]}</option>
                ))}
              </Form.Select>
              {edited && (
                <Button size="sm" variant="outline-secondary" onClick={downloadEdited}>
                  <i className="bi bi-download me-1" aria-hidden="true" />Tải file .md đã sửa
                </Button>
              )}
            </div>
          )}

          {result.fileIssues.length > 0 && (
            <Alert variant="danger">
              <div className="fw-semibold mb-1">Lỗi trong file</div>
              <ul className="mb-0 small">
                {result.fileIssues.map((i, n) => (
                  <li key={n}>{i.line ? <strong>Dòng {i.line}: </strong> : null}{i.message}</li>
                ))}
              </ul>
            </Alert>
          )}
          {result.missingImages.length > 0 && (
            <Alert variant="warning">
              <div className="fw-semibold mb-1">Thiếu ảnh — chọn thêm ở bước 2:</div>
              {result.missingImages.map((name) => <Badge key={name} bg="warning" text="dark" className="me-1">{name}</Badge>)}
            </Alert>
          )}

          <div className="d-grid gap-3">
            {result.questions.map((q) => (
              <PreviewQuestion
                key={`${q.index}-${q.line}`}
                q={q}
                media={media}
                source={result.imported ? null : questionText(markdown, q.line)}
                busy={busy !== null}
                onDifficulty={(d) => void applyEdit(setQuestionDifficulty(markdown, q.line, d))}
                onSave={(text) => applyEdit(replaceQuestion(markdown, q.line, text))}
              />
            ))}
          </div>
        </>
      )}
    </>
  );
}

function PreviewQuestion({
  q,
  media,
  source,
  busy,
  onDifficulty,
  onSave,
}: {
  q: MarkdownImportQuestion;
  media: MediaUrlMap;
  /** Nội dung gốc của câu trong file .md; null = không cho sửa (đã nhập). */
  source: string | null;
  busy: boolean;
  onDifficulty: (difficulty: Difficulty | null) => void;
  onSave: (text: string) => Promise<void>;
}) {
  const input = q.question;
  const isTrueFalse = input?.questionType === "TRUE_FALSE";
  const [draft, setDraft] = useState<string | null>(null);
  const difficulty = (input?.difficulty ?? "") as Difficulty | "";
  return (
    <Card className={q.issues.length > 0 ? "border-danger" : undefined}>
      <Card.Header className="d-flex flex-wrap align-items-center gap-2 bg-transparent">
        <strong>Câu {q.label}</strong>
        {q.part && <span className="small text-secondary">{q.part}</span>}
        {input && <Badge bg="primary-subtle" text="dark" className="border">{TYPE_LABEL[input.questionType] ?? input.questionType}</Badge>}
        {input && <Badge bg="light" text="dark" className="border">{formatNumber(input.defaultScore)} điểm</Badge>}
        {q.code && <code className="small">{q.code}</code>}
        <span className="ms-auto small text-secondary">dòng {q.line}</span>
        {source != null && (
          <>
            <Form.Select
              size="sm"
              style={{ width: "auto" }}
              aria-label={`Độ khó câu ${q.label}`}
              value={difficulty}
              disabled={busy || draft != null}
              onChange={(e) => onDifficulty((e.target.value || null) as Difficulty | null)}
            >
              <option value="">Độ khó: chưa đặt</option>
              {(Object.keys(DIFFICULTY_LABEL) as Difficulty[]).map((d) => (
                <option key={d} value={d}>{DIFFICULTY_LABEL[d]}</option>
              ))}
            </Form.Select>
            <Button
              size="sm"
              variant={draft != null ? "secondary" : "outline-primary"}
              disabled={busy}
              onClick={() => setDraft(draft != null ? null : source)}
              aria-label={`Sửa câu ${q.label}`}
              title="Sửa nội dung câu trong file .md"
            >
              <i className="bi bi-pencil" aria-hidden="true" />
            </Button>
          </>
        )}
      </Card.Header>
      <Card.Body>
        {draft != null && (
          <div className="mb-3">
            <Form.Control
              as="textarea"
              aria-label={`Nội dung câu ${q.label} (định dạng elearning-exam/1)`}
              className="font-monospace small"
              rows={Math.min(24, Math.max(6, draft.split("\n").length + 1))}
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
            />
            <div className="d-flex flex-wrap align-items-center gap-2 mt-2">
              <Button
                size="sm"
                disabled={busy}
                onClick={async () => {
                  await onSave(draft);
                  setDraft(null);
                }}
              >
                {busy ? <Spinner size="sm" className="me-1" /> : <i className="bi bi-check-lg me-1" aria-hidden="true" />}Lưu và kiểm tra lại
              </Button>
              <Button size="sm" variant="outline-secondary" onClick={() => setDraft(null)}>Hủy</Button>
              <span className="small text-secondary">
                Giữ đúng định dạng: <code>@content:</code>, <code>@option A:</code>, <code>@answer:</code>, <code>@difficulty: EASY | MEDIUM | HARD</code>…
              </span>
            </div>
          </div>
        )}
        {q.issues.length > 0 && (
          <Alert variant="danger" className="py-2 small">
            <ul className="mb-0 ps-3">
              {q.issues.map((i, n) => <li key={n}>{i.line ? <strong>Dòng {i.line}: </strong> : null}{i.message}</li>)}
            </ul>
          </Alert>
        )}
        {input && (
          <>
            <MarkdownView content={input.content} media={media} />
            {input.options && input.options.length > 0 && !isTrueFalse && (
              <ul className="list-unstyled mt-2 mb-0">
                {input.options.map((o) => (
                  <li key={o.optionCode} className={`d-flex gap-2 align-items-start p-1 rounded ${o.isCorrect ? "bg-success-subtle" : ""}`}>
                    <strong>{o.optionCode}.</strong>
                    <MarkdownView content={o.content} inline media={media} />
                    {o.isCorrect && <i className="bi bi-check-circle-fill text-success ms-auto" aria-label="Đáp án đúng" />}
                  </li>
                ))}
              </ul>
            )}
            {isTrueFalse && input.options && (
              <Table size="sm" className="mt-2 mb-0">
                <tbody>
                  {input.options.map((o) => (
                    <tr key={o.optionCode}>
                      <td style={{ width: 32 }}><strong>{o.optionCode})</strong></td>
                      <td><MarkdownView content={o.content} inline media={media} /></td>
                      <td style={{ width: 80 }} className="text-end">
                        <Badge bg={o.isCorrect ? "success" : "secondary"}>{o.isCorrect ? "Đúng" : "Sai"}</Badge>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
            {input.questionType === "FILL_IN" && (
              <div className="mt-2 small">
                <span className="text-secondary">Đáp án: </span>
                <strong>
                  {input.answerDataType === "NUMBER"
                    ? `${formatNumber(input.correctAnswerNumber ?? 0)}${input.numericTolerance ? ` (± ${formatNumber(input.numericTolerance)})` : ""}`
                    : (input.acceptedAnswers ?? []).join(" | ")}
                </strong>
              </div>
            )}
            {input.explanation && (
              <details className="mt-2">
                <summary className="small text-primary">Lời giải</summary>
                <div className="small mt-1"><MarkdownView content={input.explanation} media={media} /></div>
              </details>
            )}
          </>
        )}
      </Card.Body>
    </Card>
  );
}
