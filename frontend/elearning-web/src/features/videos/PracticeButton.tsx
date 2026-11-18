import { useState } from "react";
import { Button, Spinner } from "react-bootstrap";
import { useNavigate } from "react-router";
import { useQueryClient } from "@tanstack/react-query";
import { videosApi } from "../../services/api";
import { ConfirmDialog } from "../../components/common/Feedback";
import { useToast } from "../../components/common/toast";
import type { VideoLesson, VideoPractice } from "../../types/api";

/**
 * Nút "Luyện đề" của học viên ở video có đề luyện (D-38). Bấm: đề tự thêm vào danh sách bài tập rồi mở trang đề.
 * Đã làm đề này rồi thì hỏi có làm lại không; đồng ý thì kết quả cũ bị hủy (ẩn khỏi học viên) và làm lượt mới.
 */
export function PracticeButton({ video, size = "sm", variant = "warning", className }: {
  video: VideoLesson;
  size?: "sm" | "lg";
  variant?: string;
  className?: string;
}) {
  const navigate = useNavigate();
  const toast = useToast();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);
  const [ask, setAsk] = useState<VideoPractice | null>(null);

  if (!video.practiceExamId) {
    return null;
  }

  const start = async (restart: boolean) => {
    setBusy(true);
    try {
      const result = await videosApi.startPractice(video.id, restart);
      if (!result.ready && result.alreadyAttempted) {
        setAsk(result);
        return;
      }
      setAsk(null);
      void queryClient.invalidateQueries({ queryKey: ["student-exams"] });
      void queryClient.invalidateQueries({ queryKey: ["student-exam", result.examId] });
      if (restart) toast.success("Đã xóa kết quả cũ. Bắt đầu làm lại đề luyện.");
      navigate(`/student/exams/${result.examId}`);
    } catch (e) {
      toast.error(e);
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <Button
        size={size}
        variant={variant}
        className={className}
        disabled={busy}
        title={video.practiceExamName ? `Luyện đề: ${video.practiceExamName}` : "Luyện đề"}
        onClick={(e) => {
          // Thẻ video bấm cả thẻ để xem bài: nút này không mở video
          e.stopPropagation();
          void start(false);
        }}
      >
        {busy ? <Spinner size="sm" className="me-1" /> : <i className="bi bi-pencil-square me-1" aria-hidden="true" />}
        Luyện đề
      </Button>
      <ConfirmDialog
        show={ask !== null}
        title="Làm lại đề luyện?"
        body={
          <>
            Bạn đã làm đề <strong>{ask?.examName}</strong> {ask?.attemptCount} lần. Làm lại sẽ <strong>xóa kết quả cũ</strong> và bắt đầu
            một lượt mới.
          </>
        }
        confirmText="Làm lại"
        variant="warning"
        busy={busy}
        onCancel={() => setAsk(null)}
        onConfirm={() => void start(true)}
      />
    </>
  );
}
