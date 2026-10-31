import { useCallback, useEffect, useRef, useState, type KeyboardEvent, type PointerEvent, type ReactNode } from "react";

const MIN = 15;
const MAX = 85;
const STEP = 5;

export function clampSplit(percent: number): number {
  return Math.min(MAX, Math.max(MIN, Math.round(percent * 10) / 10));
}

/** Màn hẹp (điện thoại, máy tính bảng dọc): xếp trên / dưới thay vì trái / phải. */
function useStacked(): boolean {
  const query = "(max-width: 767.98px)";
  const supported = typeof window.matchMedia === "function";
  const [stacked, setStacked] = useState(() => supported && window.matchMedia(query).matches);
  useEffect(() => {
    if (!supported) return;
    const media = window.matchMedia(query);
    const onChange = () => setStacked(media.matches);
    media.addEventListener("change", onChange);
    return () => media.removeEventListener("change", onChange);
  }, [supported]);
  return stacked;
}

/**
 * Hai khung đặt cạnh nhau, kéo thanh ở giữa để co giãn (chuột, cảm ứng; phím mũi tên khi thanh đang được chọn;
 * nhấp đúp để chia đều). Khi kéo, lớp phủ trong suốt che hai khung để iframe (YouTube, Drive) không "nuốt" sự kiện chuột.
 */
export function SplitView({
  first,
  second,
  label,
  initial = 50,
}: {
  first: ReactNode;
  second: ReactNode;
  /** Mô tả thanh kéo cho trình đọc màn hình. */
  label: string;
  initial?: number;
}) {
  const stacked = useStacked();
  const [percent, setPercent] = useState(() => clampSplit(initial));
  const [dragging, setDragging] = useState(false);
  const container = useRef<HTMLDivElement>(null);

  const moveTo = useCallback(
    (clientX: number, clientY: number) => {
      const box = container.current?.getBoundingClientRect();
      if (!box) return;
      const value = stacked ? ((clientY - box.top) / box.height) * 100 : ((clientX - box.left) / box.width) * 100;
      setPercent(clampSplit(value));
    },
    [stacked],
  );

  const onPointerDown = (e: PointerEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.currentTarget.setPointerCapture(e.pointerId);
    setDragging(true);
  };
  const onPointerMove = (e: PointerEvent<HTMLDivElement>) => {
    if (dragging) moveTo(e.clientX, e.clientY);
  };
  const onPointerUp = (e: PointerEvent<HTMLDivElement>) => {
    if (e.currentTarget.hasPointerCapture(e.pointerId)) e.currentTarget.releasePointerCapture(e.pointerId);
    setDragging(false);
  };
  const onKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    const back = stacked ? "ArrowUp" : "ArrowLeft";
    const forward = stacked ? "ArrowDown" : "ArrowRight";
    if (e.key === back || e.key === forward) {
      e.preventDefault();
      setPercent((p) => clampSplit(p + (e.key === forward ? STEP : -STEP)));
    } else if (e.key === "Home" || e.key === "End") {
      e.preventDefault();
      setPercent(e.key === "Home" ? MIN : MAX);
    }
  };

  return (
    <div
      ref={container}
      className={`split-view ${stacked ? "split-view-stacked" : ""} ${dragging ? "split-view-dragging" : ""}`}
      style={{ gridTemplate: stacked ? `${percent}fr auto ${100 - percent}fr / 1fr` : `1fr / ${percent}fr auto ${100 - percent}fr` }}
    >
      <div className="split-view-pane">{first}</div>
      <div
        role="separator"
        tabIndex={0}
        aria-label={label}
        aria-orientation={stacked ? "horizontal" : "vertical"}
        aria-valuemin={MIN}
        aria-valuemax={MAX}
        aria-valuenow={Math.round(percent)}
        title="Kéo để co giãn, nhấp đúp để chia đều"
        className="split-view-handle"
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
        onPointerCancel={onPointerUp}
        onKeyDown={onKeyDown}
        onDoubleClick={() => setPercent(50)}
      >
        <span className="split-view-grip" aria-hidden="true" />
      </div>
      <div className="split-view-pane">{second}</div>
    </div>
  );
}
