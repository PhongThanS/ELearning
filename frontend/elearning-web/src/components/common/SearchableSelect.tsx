import { useEffect, useId, useMemo, useRef, useState } from "react";
import { Badge, Form, Spinner } from "react-bootstrap";

export interface SearchableOption {
  value: string;
  label: string;
  code?: string;
  sublabel?: string;
  badge?: string;
  badgeVariant?: string;
  disabled?: boolean;
}

export interface SearchableSelectProps {
  id?: string;
  name?: string;
  value: string | null | undefined;
  onChange: (value: string) => void;
  options: SearchableOption[];
  placeholder?: string;
  searchPlaceholder?: string;
  size?: "sm" | "lg";
  isInvalid?: boolean;
  disabled?: boolean;
  clearable?: boolean;
  emptyLabel?: string;
  emptyValue?: string;
  className?: string;
  style?: React.CSSProperties;
  loading?: boolean;
  onSearch?: (keyword: string) => void;
}

/** Chuẩn hóa chuỗi tiếng Việt không dấu để tìm kiếm nhạy bén */
export function normalizeVietnamese(text: string): string {
  return text
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "d")
    .trim();
}

/**
 * Dropdown chọn phần tử hỗ trợ tìm kiếm tức thì theo cả MÃ (code) và TÊN (label/name).
 * Tối ưu truy vấn: Lọc in-memory cực nhanh không gây lag mạng, hỗ trợ cả server search nếu cần.
 */
export function SearchableSelect({
  id,
  name,
  value,
  onChange,
  options,
  placeholder = "-- Chọn --",
  searchPlaceholder = "Nhập mã hoặc tên để tìm...",
  size,
  isInvalid = false,
  disabled = false,
  clearable = true,
  emptyLabel,
  emptyValue = "",
  className = "",
  style,
  loading = false,
  onSearch,
}: SearchableSelectProps) {
  const generatedId = useId();
  const selectId = id || generatedId;
  const [isOpen, setIsOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [activeIndex, setActiveIndex] = useState(-1);

  const containerRef = useRef<HTMLDivElement>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLDivElement>(null);

  // Tìm option hiện tại đang được chọn
  const selectedOption = useMemo(() => {
    if (!value || value === emptyValue) return null;
    return options.find((o) => o.value === value) ?? null;
  }, [value, emptyValue, options]);

  // Lọc options theo từ khóa (Mã hoặc Tên)
  const filteredOptions = useMemo(() => {
    if (!search.trim()) return options;

    const query = normalizeVietnamese(search);
    return options.filter((opt) => {
      const matchLabel = normalizeVietnamese(opt.label).includes(query);
      const matchCode = opt.code ? normalizeVietnamese(opt.code).includes(query) : false;
      const matchSub = opt.sublabel ? normalizeVietnamese(opt.sublabel).includes(query) : false;
      return matchLabel || matchCode || matchSub;
    });
  }, [options, search]);

  // Đóng dropdown khi click ra ngoài
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    }
    if (isOpen) {
      document.addEventListener("mousedown", handleClickOutside);
    }
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
    };
  }, [isOpen]);

  // Focus ô tìm kiếm khi mở
  useEffect(() => {
    if (isOpen) {
      setSearch("");
      setActiveIndex(-1);
      setTimeout(() => {
        searchInputRef.current?.focus();
      }, 50);
    }
  }, [isOpen]);

  // Gọi callback onSearch nếu có (hỗ trợ server search)
  useEffect(() => {
    if (onSearch) {
      const timer = setTimeout(() => {
        onSearch(search);
      }, 250);
      return () => clearTimeout(timer);
    }
  }, [search, onSearch]);

  const handleSelect = (val: string) => {
    onChange(val);
    setIsOpen(false);
  };

  const handleClear = (e: React.MouseEvent) => {
    e.stopPropagation();
    onChange(emptyValue);
  };

  // Điều hướng bằng bàn phím
  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (disabled) return;

    if (!isOpen) {
      if (e.key === "Enter" || e.key === " " || e.key === "ArrowDown") {
        e.preventDefault();
        setIsOpen(true);
      }
      return;
    }

    const totalItems = (emptyLabel ? 1 : 0) + filteredOptions.length;

    if (e.key === "ArrowDown") {
      e.preventDefault();
      setActiveIndex((prev) => (prev + 1 < totalItems ? prev + 1 : 0));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActiveIndex((prev) => (prev - 1 >= 0 ? prev - 1 : totalItems - 1));
    } else if (e.key === "Enter") {
      e.preventDefault();
      if (emptyLabel && activeIndex === 0) {
        handleSelect(emptyValue);
      } else {
        const optionIndex = emptyLabel ? activeIndex - 1 : activeIndex;
        if (optionIndex >= 0 && optionIndex < filteredOptions.length) {
          const opt = filteredOptions[optionIndex];
          if (opt && !opt.disabled) {
            handleSelect(opt.value);
          }
        }
      }
    } else if (e.key === "Escape") {
      e.preventDefault();
      setIsOpen(false);
    }
  };

  // Cuộn đến item active khi di chuyển phím
  useEffect(() => {
    if (activeIndex >= 0 && listRef.current) {
      const items = listRef.current.querySelectorAll<HTMLElement>("[data-option-item]");
      if (items[activeIndex]) {
        items[activeIndex].scrollIntoView({ block: "nearest" });
      }
    }
  }, [activeIndex]);

  const isSmall = size === "sm";

  return (
    <div
      ref={containerRef}
      className={`searchable-select-container position-relative ${className}`}
      style={{ minWidth: "180px", ...style }}
      onKeyDown={handleKeyDown}
    >
      {/* Nút trigger dropdown chính */}
      <div
        id={selectId}
        role="combobox"
        aria-expanded={isOpen}
        aria-haspopup="listbox"
        tabIndex={disabled ? -1 : 0}
        onClick={() => !disabled && setIsOpen(!isOpen)}
        className={`form-select d-flex align-items-center justify-content-between cursor-pointer ${
          isSmall ? "form-select-sm" : ""
        } ${isInvalid ? "is-invalid" : ""} ${disabled ? "disabled bg-light" : "bg-white"}`}
        style={{
          cursor: disabled ? "not-allowed" : "pointer",
          userSelect: "none",
          paddingRight: "0.75rem",
        }}
      >
        <div className="text-truncate flex-grow-1 me-2 d-flex align-items-center gap-1">
          {selectedOption ? (
            <>
              {selectedOption.code && (
                <span className="badge bg-light text-primary border font-monospace me-1 py-1">
                  {selectedOption.code}
                </span>
              )}
              <span className="fw-medium text-dark">{selectedOption.label}</span>
              {selectedOption.sublabel && (
                <span className="text-muted small ms-1">({selectedOption.sublabel})</span>
              )}
            </>
          ) : emptyLabel && (!value || value === emptyValue) ? (
            <span className="text-secondary">{emptyLabel}</span>
          ) : (
            <span className="text-muted">{placeholder}</span>
          )}
        </div>

        <div className="d-flex align-items-center gap-1 ms-auto flex-shrink-0">
          {clearable && selectedOption && !disabled && (
            <span
              role="button"
              className="text-muted hover-dark me-1"
              style={{ cursor: "pointer", fontSize: "0.85rem" }}
              title="Xóa lựa chọn"
              onClick={handleClear}
            >
              <i className="bi bi-x-circle-fill" />
            </span>
          )}
          <i
            className={`bi bi-chevron-down text-muted transition-transform ${
              isOpen ? "rotate-180" : ""
            }`}
            style={{
              fontSize: "0.75rem",
              transition: "transform 0.2s ease",
              transform: isOpen ? "rotate(180deg)" : "none",
            }}
          />
        </div>
      </div>

      {name && <input type="hidden" name={name} value={value ?? ""} />}

      {/* Menu dropdown mở ra */}
      {isOpen && (
        <div
          className="searchable-select-menu position-absolute start-0 end-0 mt-1 bg-white rounded border shadow-lg"
          style={{
            zIndex: 1060,
            minWidth: "260px",
            animation: "fadeIn 0.15s ease",
          }}
        >
          {/* Ô tìm kiếm nhanh */}
          <div className="p-2 border-bottom bg-light">
            <div className="position-relative">
              <i
                className="bi bi-search position-absolute top-50 start-0 translate-middle-y ms-2 text-muted"
                style={{ fontSize: "0.8rem" }}
              />
              <Form.Control
                ref={searchInputRef}
                type="text"
                size="sm"
                placeholder={searchPlaceholder}
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="ps-4 pe-4"
                style={{ fontSize: "0.85rem" }}
              />
              {search && (
                <button
                  type="button"
                  className="btn btn-sm btn-link position-absolute top-50 end-0 translate-middle-y text-muted p-0 me-2 text-decoration-none"
                  onClick={() => setSearch("")}
                  title="Xóa tìm kiếm"
                >
                  <i className="bi bi-x" />
                </button>
              )}
            </div>
            <div className="d-flex justify-content-between align-items-center mt-1 px-1 small text-muted" style={{ fontSize: "0.75rem" }}>
              <span>Tìm theo mã hoặc tên</span>
              <span>
                {filteredOptions.length} / {options.length}
              </span>
            </div>
          </div>

          {/* Danh sách options cuộn */}
          <div
            ref={listRef}
            className="searchable-select-list py-1"
            style={{ maxHeight: "250px", overflowY: "auto" }}
            role="listbox"
          >
            {loading && (
              <div className="p-3 text-center text-muted small">
                <Spinner size="sm" animation="border" className="me-2" />
                Đang tìm kiếm...
              </div>
            )}

            {/* Mục emptyLabel (nếu có) */}
            {emptyLabel && !search && (
              <div
                data-option-item
                role="option"
                aria-selected={!value || value === emptyValue}
                className={`px-3 py-2 cursor-pointer d-flex align-items-center justify-content-between border-bottom ${
                  (!value || value === emptyValue)
                    ? "bg-primary-subtle text-primary fw-semibold"
                    : activeIndex === 0
                    ? "bg-light"
                    : "text-secondary"
                }`}
                style={{ cursor: "pointer", fontSize: "0.875rem" }}
                onClick={() => handleSelect(emptyValue)}
              >
                <span>{emptyLabel}</span>
                {(!value || value === emptyValue) && <i className="bi bi-check-lg" />}
              </div>
            )}

            {!loading && filteredOptions.length === 0 ? (
              <div className="p-3 text-center text-muted small">
                <i className="bi bi-search d-block fs-5 text-secondary opacity-50 mb-1" />
                Không tìm thấy kết quả nào khớp với &quot;{search}&quot;.
              </div>
            ) : (
              filteredOptions.map((opt, idx) => {
                const itemIndex = emptyLabel ? idx + 1 : idx;
                const isSelected = opt.value === value;
                const isActive = activeIndex === itemIndex;

                return (
                  <div
                    key={opt.value}
                    data-option-item
                    role="option"
                    aria-selected={isSelected}
                    aria-disabled={opt.disabled}
                    className={`px-3 py-2 cursor-pointer d-flex align-items-center justify-content-between transition-colors ${
                      opt.disabled
                        ? "opacity-50 text-muted"
                        : isSelected
                        ? "bg-primary bg-opacity-10 text-primary fw-medium"
                        : isActive
                        ? "bg-light text-dark"
                        : "text-dark"
                    }`}
                    style={{
                      cursor: opt.disabled ? "not-allowed" : "pointer",
                      fontSize: "0.875rem",
                    }}
                    onMouseEnter={() => !opt.disabled && setActiveIndex(itemIndex)}
                    onClick={() => !opt.disabled && handleSelect(opt.value)}
                  >
                    <div className="d-flex align-items-center gap-2 flex-grow-1 overflow-hidden me-2">
                      {opt.code && (
                        <span
                          className={`badge font-monospace px-1 py-0.5 border ${
                            isSelected
                              ? "bg-primary text-white border-primary"
                              : "bg-light text-dark border-secondary-subtle"
                          }`}
                          style={{ fontSize: "0.75rem", minWidth: "48px", textAlign: "center" }}
                        >
                          {opt.code}
                        </span>
                      )}
                      <span className="text-truncate">{opt.label}</span>
                      {opt.sublabel && (
                        <span className="text-muted small text-truncate">
                          ({opt.sublabel})
                        </span>
                      )}
                      {opt.badge && (
                        <Badge
                          bg={opt.badgeVariant || "secondary"}
                          className="ms-auto"
                          style={{ fontSize: "0.7rem" }}
                        >
                          {opt.badge}
                        </Badge>
                      )}
                    </div>

                    {isSelected && (
                      <i className="bi bi-check-lg text-primary fw-bold flex-shrink-0 ms-2" />
                    )}
                  </div>
                );
              })
            )}
          </div>
        </div>
      )}
    </div>
  );
}
