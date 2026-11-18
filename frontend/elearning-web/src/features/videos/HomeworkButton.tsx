import { useState } from "react";
import { Button, Modal } from "react-bootstrap";
import type { VideoLesson } from "../../types/api";
import { parseDriveLink } from "./mediaLinks";

/**
 * Nút "Bài tập về nhà" của video (D-39): mở file PDF bài tập (Google Drive / Docs) trong cửa sổ xem ngay trên trang,
 * kèm nút mở trong Drive để tải / in. Video không có bài tập về nhà thì không hiện.
 */
export function HomeworkButton({ video, size = "sm", variant = "outline-success", className }: {
  video: VideoLesson;
  size?: "sm" | "lg";
  variant?: string;
  className?: string;
}) {
  const [show, setShow] = useState(false);
  const file = video.homeworkUrl ? parseDriveLink(video.homeworkUrl) : null;
  if (!file) {
    return null;
  }

  return (
    // Thẻ video bấm cả thẻ để xem bài: chặn sự kiện của nút và cửa sổ (Modal là portal nhưng sự kiện React vẫn nổi lên thẻ)
    <span className="d-inline-flex" onClick={(e) => e.stopPropagation()}>
      <Button size={size} variant={variant} className={className} title="Bài tập về nhà (PDF)" onClick={() => setShow(true)}>
        <i className="bi bi-file-earmark-pdf me-1" aria-hidden="true" />
        Bài tập về nhà
      </Button>
      <Modal show={show} onHide={() => setShow(false)} size="xl" centered fullscreen="lg-down" dialogClassName="homework-modal">
        <Modal.Header closeButton>
          <Modal.Title className="h6 d-flex align-items-center gap-2 min-w-0">
            <i className="bi bi-file-earmark-pdf text-danger" aria-hidden="true" />
            <span className="text-truncate">Bài tập về nhà — {video.title}</span>
          </Modal.Title>
        </Modal.Header>
        <Modal.Body className="p-0" style={{ height: "80vh" }}>
          <iframe
            src={file.previewUrl}
            title={`Bài tập về nhà — ${video.title}`}
            className="w-100 h-100 border-0 d-block"
            allow="autoplay"
          />
        </Modal.Body>
        <Modal.Footer className="py-2">
          <span className="small text-secondary me-auto">Không xem được? Mở trong Google Drive để tải hoặc in.</span>
          <a href={file.openUrl} target="_blank" rel="noreferrer" className="btn btn-sm btn-outline-primary">
            <i className="bi bi-box-arrow-up-right me-1" aria-hidden="true" />
            Mở trong Drive
          </a>
          <Button size="sm" variant="secondary" onClick={() => setShow(false)}>
            Đóng
          </Button>
        </Modal.Footer>
      </Modal>
    </span>
  );
}
