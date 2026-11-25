import { Button } from "react-bootstrap";
import { useTranslation } from "react-i18next";
import type { Category } from "../../types/api";

export type CategoryTag = Pick<Category, "id" | "name" | "parentId">;

export const byName = (a: CategoryTag, b: CategoryTag) => a.name.localeCompare(b.name, "vi", { numeric: true });

/** Tên chuyên đề con bỏ phần lặp tên cha: "Toán 12 - Xác suất" dưới "Toán 12" hiện "Xác suất". */
function childLabel(child: CategoryTag, parent: CategoryTag): string {
  const prefix = `${parent.name} - `;
  return child.name.startsWith(prefix) ? child.name.slice(prefix.length) : child.name;
}

/**
 * Thẻ lọc dưới tiêu đề một chuyên đề lớn: "Tất cả" và các chuyên đề con của nó.
 * `value` là id đang lọc: id chuyên đề lớn (tất cả) hoặc id một chuyên đề con.
 */
export function CategoryTags({
  parent,
  categories,
  value,
  onChange,
}: {
  parent: CategoryTag;
  categories: CategoryTag[];
  value: string;
  onChange: (id: string) => void;
}) {
  const { t } = useTranslation();
  const children = categories.filter((c) => c.parentId === parent.id).sort(byName);
  if (children.length === 0) {
    return null;
  }

  const tag = (id: string, label: string) => (
    <Button
      key={id}
      variant={value === id ? "primary" : "outline-primary"}
      className="rounded-pill"
      aria-pressed={value === id}
      onClick={() => onChange(id)}
    >
      {label}
    </Button>
  );

  return (
    <div className="d-flex flex-wrap gap-2 mb-3" role="group" aria-label={t("student.subCategoryFilter", { name: parent.name })}>
      {tag(parent.id, t("common.all"))}
      {children.map((c) => tag(c.id, childLabel(c, parent)))}
    </div>
  );
}
