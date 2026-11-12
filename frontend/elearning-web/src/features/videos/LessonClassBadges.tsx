import { Badge } from "react-bootstrap";
import type { VideoClassroom } from "../../types/api";

/** Nhãn lớp được xem của bài giảng; không gắn lớp nào = "Chung cho tất cả". Quá `max` lớp thì gộp thành "+N lớp". */
export function LessonClassBadges({ classrooms, max = 3 }: { classrooms: VideoClassroom[]; max?: number }) {
  if (classrooms.length === 0) {
    return (
      <Badge bg="secondary-subtle" className="text-secondary border border-secondary-subtle fw-normal">
        Chung cho tất cả
      </Badge>
    );
  }
  const shown = classrooms.slice(0, max);
  const hidden = classrooms.slice(max);
  return (
    <>
      {shown.map((c) => (
        <Badge key={c.id} bg="info-subtle" className="text-info-emphasis border border-info-subtle fw-normal" title={c.code}>
          {c.name}
        </Badge>
      ))}
      {hidden.length > 0 && (
        <Badge bg="info-subtle" className="text-info-emphasis border border-info-subtle fw-normal" title={hidden.map((c) => c.name).join(", ")}>
          +{hidden.length} lớp
        </Badge>
      )}
    </>
  );
}
