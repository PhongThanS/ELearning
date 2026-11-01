import { SearchableSelect, type SearchableOption } from "./SearchableSelect";

/**
 * Chọn nhiều phần tử: các mục đã chọn hiện thành thẻ (bấm × để bỏ), ô bên dưới tìm theo mã / tên để thêm.
 * Ô thêm chỉ liệt kê các mục chưa chọn.
 */
export function MultiSearchableSelect({
  id,
  value,
  onChange,
  options,
  placeholder = "-- Thêm --",
  searchPlaceholder,
  emptyText,
}: {
  id?: string;
  value: string[];
  onChange: (value: string[]) => void;
  options: SearchableOption[];
  placeholder?: string;
  searchPlaceholder?: string;
  /** Hiện khi chưa chọn gì. */
  emptyText?: string;
}) {
  const selected = value.map((v) => options.find((o) => o.value === v) ?? { value: v, label: v });
  const remaining = options.filter((o) => !value.includes(o.value));

  return (
    <div>
      <div className="d-flex flex-wrap align-items-center gap-1 mb-2" aria-live="polite">
        {selected.length === 0 && emptyText && <span className="small text-secondary">{emptyText}</span>}
        {selected.map((o) => (
          <span key={o.value} className="badge rounded-pill text-bg-primary d-inline-flex align-items-center gap-1 fw-normal">
            {o.code && <span className="opacity-75">{o.code}</span>}
            <span>{o.label}</span>
            <button
              type="button"
              className="btn-close btn-close-white ms-1"
              style={{ fontSize: "0.5rem" }}
              aria-label={`Bỏ ${o.label}`}
              onClick={() => onChange(value.filter((v) => v !== o.value))}
            />
          </span>
        ))}
        {selected.length > 1 && (
          <button type="button" className="btn btn-link btn-sm p-0 small" onClick={() => onChange([])}>
            Bỏ chọn hết
          </button>
        )}
      </div>
      <SearchableSelect
        id={id}
        value=""
        onChange={(v) => {
          if (v && !value.includes(v)) onChange([...value, v]);
        }}
        options={remaining}
        placeholder={remaining.length === 0 && options.length > 0 ? "Đã chọn tất cả" : placeholder}
        searchPlaceholder={searchPlaceholder}
        clearable={false}
        disabled={remaining.length === 0}
      />
    </div>
  );
}
