import { useEffect, useRef } from "react";
import { Spinner } from "react-bootstrap";

/** Phần tử canh cuối danh sách: cuộn tới gần thì gọi onReach để tải trang kế (cuộn vô hạn, không phân trang). */
export function InfiniteSentinel({ hasMore, loading, onReach }: { hasMore: boolean; loading: boolean; onReach: () => void }) {
  const ref = useRef<HTMLDivElement | null>(null);
  const callback = useRef(onReach);
  callback.current = onReach;

  useEffect(() => {
    const el = ref.current;
    if (!el || !hasMore || loading) {
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((e) => e.isIntersecting)) {
          callback.current();
        }
      },
      { rootMargin: "300px" },
    );
    observer.observe(el);
    return () => observer.disconnect();
  }, [hasMore, loading]);

  return (
    <div ref={ref} className="text-center py-3" aria-hidden={!loading}>
      {loading && <Spinner animation="border" size="sm" />}
    </div>
  );
}
