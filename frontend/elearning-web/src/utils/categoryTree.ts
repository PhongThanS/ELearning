/** Chuyên đề 2 cấp: chuyên đề gốc (có hoặc không có chuyên đề con) và chuyên đề con. */
export interface CategoryNode {
  id: string;
  name: string;
  parentId: string | null;
  parentName?: string | null;
}

/** Xếp theo cây: mỗi chuyên đề gốc rồi tới các chuyên đề con của nó (giữ thứ tự gốc, con theo tên); con mồ côi xếp cuối. */
export function orderCategories<T extends CategoryNode>(items: T[]): T[] {
  const ids = new Set(items.map((c) => c.id));
  const children = new Map<string, T[]>();
  for (const c of items) {
    if (c.parentId && ids.has(c.parentId)) {
      children.set(c.parentId, [...(children.get(c.parentId) ?? []), c]);
    }
  }
  const result: T[] = [];
  for (const c of items) {
    if (c.parentId && ids.has(c.parentId)) continue;
    result.push(c);
    const kids = (children.get(c.id) ?? []).sort((a, b) => a.name.localeCompare(b.name, "vi"));
    result.push(...kids);
  }
  return result;
}

/** Tên hiển thị trong ô chọn: chuyên đề con ghi kèm cha, ví dụ "Hàm số › Đồng biến nghịch biến". */
export function categoryLabel(c: CategoryNode): string {
  return c.parentId && c.parentName ? `${c.parentName} › ${c.name}` : c.name;
}
