import { useState } from "react";
import { Badge, Button, Card, Col, Form, Row, Table } from "react-bootstrap";
import { useTranslation } from "react-i18next";
import { examsApi } from "../../services/api";
import { useActiveCategories } from "../../hooks/useCategories";
import { formatNumber } from "../../utils/format";
import type { PoolRuleInput, QuestionDifficulty, QuestionType, VersionDetail } from "../../types/api";

const DIFFICULTIES: QuestionDifficulty[] = ["EASY", "MEDIUM", "HARD"];
const TYPES: QuestionType[] = ["SINGLE_CHOICE", "MULTIPLE_CHOICE", "TRUE_FALSE", "FILL_IN", "ESSAY"];
const emptyRule = (): PoolRuleInput => ({ categoryId: null, difficulty: null, tag: null, questionType: null, drawCount: 5, scorePerQuestion: 1 });

/**
 * Pool ngẫu nhiên (docs/02-nghiep-vu.md mục 4.8): mỗi lượt thi bốc drawCount câu trong các câu ứng viên của quy tắc.
 * Câu ứng viên được snapshot khi thêm quy tắc; "Làm mới" để lấy lại theo ngân hàng hiện tại.
 */
export function PoolRulesCard({
  examId,
  version,
  canEdit,
  change,
}: {
  examId: string;
  version: VersionDetail;
  canEdit: boolean;
  change: { mutate: (run: () => Promise<VersionDetail>) => void; isPending: boolean };
}) {
  const { t } = useTranslation();
  const categories = useActiveCategories();
  const [rule, setRule] = useState<PoolRuleInput>(emptyRule);
  const rules = version.poolRules;

  if (!canEdit && rules.length === 0) {
    return null;
  }

  const describe = (r: VersionDetail["poolRules"][number]) =>
    [
      r.categoryName && `Danh mục ${r.categoryName}`,
      r.difficulty && t(`enums.difficulty.${r.difficulty}`),
      r.tag && `tag “${r.tag}”`,
      r.questionType && t(`enums.questionType.${r.questionType}`),
    ].filter(Boolean).join(" · ") || "Toàn bộ ngân hàng";

  return (
    <Card className="mb-3">
      <Card.Body>
        <h2 className="h6">Pool ngẫu nhiên</h2>
        <p className="small text-secondary mb-2">
          Mỗi lượt thi có câu cố định bên dưới cộng thêm số câu bốc ngẫu nhiên theo từng quy tắc; hai học viên có thể nhận bộ câu khác nhau.
        </p>
        {rules.length > 0 && (
          <Table size="sm" className="mb-2 align-middle">
            <thead>
              <tr>
                <th>#</th>
                <th>Tiêu chí</th>
                <th className="text-end">Bốc</th>
                <th className="text-end">Điểm / câu</th>
                {canEdit && <th aria-label={t("common.actions")} />}
              </tr>
            </thead>
            <tbody>
              {rules.map((r) => (
                <tr key={r.id}>
                  <td>{r.order}</td>
                  <td className="small">
                    {describe(r)}
                    <div>
                      <Badge bg={r.candidateCount >= r.drawCount ? "success-subtle" : "danger"} text={r.candidateCount >= r.drawCount ? "dark" : undefined}>
                        {r.candidateCount} câu ứng viên
                      </Badge>
                    </div>
                  </td>
                  <td className="text-end">{r.drawCount}</td>
                  <td className="text-end">{formatNumber(r.scorePerQuestion)}</td>
                  {canEdit && (
                    <td className="text-end text-nowrap">
                      <Button size="sm" variant="link" className="p-0 me-2" disabled={change.isPending} aria-label={`Làm mới quy tắc ${r.order}`}
                        onClick={() => change.mutate(() => examsApi.refreshPoolRule(examId, version.id, r.id))}>
                        Làm mới
                      </Button>
                      <Button size="sm" variant="link" className="p-0 text-danger" disabled={change.isPending} aria-label={`Xóa quy tắc ${r.order}`}
                        onClick={() => change.mutate(() => examsApi.removePoolRule(examId, version.id, r.id))}>
                        {t("common.delete")}
                      </Button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </Table>
        )}
        {canEdit && (
          <Form
            onSubmit={(e) => {
              e.preventDefault();
              change.mutate(() => examsApi.addPoolRule(examId, version.id, { ...rule, tag: rule.tag?.trim() || null }));
              setRule(emptyRule());
            }}
          >
            <Row className="g-2 align-items-end">
              <Col md={4}>
                <Form.Label htmlFor="pool-category" className="small mb-0">{t("nav.categories")}</Form.Label>
                <Form.Select id="pool-category" size="sm" value={rule.categoryId ?? ""} onChange={(e) => setRule({ ...rule, categoryId: e.target.value || null })}>
                  <option value="">Mọi danh mục</option>
                  {categories.data?.items.map((c) => (
                    <option key={c.id} value={c.id}>{c.name}</option>
                  ))}
                </Form.Select>
              </Col>
              <Col md={4}>
                <Form.Label htmlFor="pool-difficulty" className="small mb-0">Độ khó</Form.Label>
                <Form.Select id="pool-difficulty" size="sm" value={rule.difficulty ?? ""} onChange={(e) => setRule({ ...rule, difficulty: (e.target.value || null) as QuestionDifficulty | null })}>
                  <option value="">Mọi độ khó</option>
                  {DIFFICULTIES.map((d) => (
                    <option key={d} value={d}>{t(`enums.difficulty.${d}`)}</option>
                  ))}
                </Form.Select>
              </Col>
              <Col md={4}>
                <Form.Label htmlFor="pool-type" className="small mb-0">Loại câu</Form.Label>
                <Form.Select id="pool-type" size="sm" value={rule.questionType ?? ""} onChange={(e) => setRule({ ...rule, questionType: (e.target.value || null) as QuestionType | null })}>
                  <option value="">Mọi loại</option>
                  {TYPES.map((type) => (
                    <option key={type} value={type}>{t(`enums.questionType.${type}`)}</option>
                  ))}
                </Form.Select>
              </Col>
              <Col md={4}>
                <Form.Label htmlFor="pool-tag" className="small mb-0">Tag</Form.Label>
                <Form.Control id="pool-tag" size="sm" value={rule.tag ?? ""} placeholder="(không lọc)" onChange={(e) => setRule({ ...rule, tag: e.target.value })} />
              </Col>
              <Col md={3}>
                <Form.Label htmlFor="pool-draw" className="small mb-0">Số câu bốc</Form.Label>
                <Form.Control id="pool-draw" size="sm" type="number" min={1} max={500} required value={rule.drawCount}
                  onChange={(e) => setRule({ ...rule, drawCount: Number(e.target.value) })} />
              </Col>
              <Col md={3}>
                <Form.Label htmlFor="pool-score" className="small mb-0">Điểm mỗi câu</Form.Label>
                <Form.Control id="pool-score" size="sm" type="number" min={0.25} max={100} step={0.25} required value={rule.scorePerQuestion}
                  onChange={(e) => setRule({ ...rule, scorePerQuestion: Number(e.target.value) })} />
              </Col>
              <Col md={2}>
                <Button type="submit" size="sm" className="w-100" disabled={change.isPending}>Thêm quy tắc</Button>
              </Col>
            </Row>
          </Form>
        )}
      </Card.Body>
    </Card>
  );
}
