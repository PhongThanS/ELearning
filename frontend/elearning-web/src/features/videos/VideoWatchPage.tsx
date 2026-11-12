import { useEffect } from "react";
import { Badge } from "react-bootstrap";
import { useParams } from "react-router";
import { useQuery } from "@tanstack/react-query";
import { videosApi } from "../../services/api";
import { ErrorAlert, Loading } from "../../components/common/Feedback";
import type { VideoLesson } from "../../types/api";
import { extractYoutubeId, isDirectVideo, parseDriveLink } from "./mediaLinks";
import { SplitView } from "./SplitView";
import { LessonClassBadges } from "./LessonClassBadges";

/** Mở trang xem bài giảng ở tab mới (base: "/admin/videos" hoặc "/student/videos"). */
export function openLessonInNewTab(base: string, id: string): void {
  window.open(`${base}/${id}/watch`, "_blank", "noopener");
}

/** Video (YouTube / file trực tiếp) giữ tỉ lệ 16:9 và co vừa khung; tệp Drive lấp đầy khung. */
function LessonMedia({ video }: { video: VideoLesson }) {
  const ytId = video.youtubeVideoId || extractYoutubeId(video.videoUrl);
  const drive = parseDriveLink(video.videoUrl);
  if (ytId) {
    return (
      <div className="lesson-media-fit">
        <iframe
          src={`https://www.youtube-nocookie.com/embed/${ytId}?autoplay=1&rel=0`}
          title={video.title}
          className="border-0 d-block bg-black"
          allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
          allowFullScreen
        ></iframe>
      </div>
    );
  }
  if (drive) {
    return (
      <iframe
        src={drive.previewUrl}
        title={video.title}
        className="w-100 h-100 border-0 d-block bg-white"
        allow="autoplay; encrypted-media; fullscreen"
        allowFullScreen
      ></iframe>
    );
  }
  if (isDirectVideo(video.videoUrl)) {
    return (
      <div className="lesson-media-fit">
        <video src={video.videoUrl} controls autoPlay className="d-block bg-black">
          Trình duyệt của bạn không hỗ trợ phát thẻ video trực tiếp.
        </video>
      </div>
    );
  }
  return (
    <div className="h-100 d-flex flex-column align-items-center justify-content-center text-white p-3 text-center">
      <p className="mb-3">Không thể nhúng video này trực tiếp.</p>
      <a href={video.videoUrl} target="_blank" rel="noreferrer" className="btn btn-primary">
        <i className="bi bi-box-arrow-up-right me-1"></i> Mở liên kết video
      </a>
    </div>
  );
}

/** Thanh trên: tiêu đề và các nút mở nguồn gốc (YouTube / Drive / link). */
function WatchHeader({ video }: { video: VideoLesson }) {
  const ytId = video.youtubeVideoId || extractYoutubeId(video.videoUrl);
  const drive = parseDriveLink(video.videoUrl);
  const attachment = video.documentUrl ? parseDriveLink(video.documentUrl) : null;
  const isDoc = !ytId && !!drive;
  const linkClass = "btn btn-sm btn-outline-light d-inline-flex align-items-center gap-1 text-nowrap";
  return (
    <header className="d-flex align-items-center gap-2 px-3 py-2 bg-dark text-white">
      <h1 className="h6 mb-0 d-flex align-items-center gap-2 flex-grow-1 min-w-0">
        <i className={`bi ${attachment ? "bi-collection-play" : isDoc ? "bi-file-earmark-text" : "bi-camera-reels"}`} aria-hidden="true"></i>
        <span className="text-truncate">{video.title}</span>
      </h1>
      {ytId ? (
        <a href={`https://www.youtube.com/watch?v=${ytId}`} target="_blank" rel="noreferrer" className={linkClass}>
          <i className="bi bi-youtube" aria-hidden="true"></i>
          <span className="d-none d-md-inline">YouTube</span>
        </a>
      ) : drive ? (
        <a href={drive.openUrl} target="_blank" rel="noreferrer" className={linkClass}>
          <i className="bi bi-box-arrow-up-right" aria-hidden="true"></i>
          <span className="d-none d-md-inline">Mở trong Google Drive</span>
        </a>
      ) : null}
      {attachment && (
        <a href={attachment.openUrl} target="_blank" rel="noreferrer" className={linkClass}>
          <i className="bi bi-box-arrow-up-right" aria-hidden="true"></i>
          <span className="d-none d-md-inline">Mở tài liệu trong Drive</span>
        </a>
      )}
    </header>
  );
}

/** Thông tin bài giảng dưới video (chỉ khi xem video một mình). */
function WatchInfo({ video }: { video: VideoLesson }) {
  return (
    <div className="bg-light p-3 border-top">
      <div className="d-flex flex-wrap gap-2 align-items-center">
        {video.categoryName && (
          <Badge bg="primary-subtle" className="text-primary border border-primary-subtle">
            <i className="bi bi-folder2 me-1" aria-hidden="true" />{video.categoryName}
          </Badge>
        )}
        <LessonClassBadges classrooms={video.classrooms} max={10} />
        {video.durationMinutes && <span className="text-muted small">⏱️ Thời lượng: {video.durationMinutes} phút</span>}
      </div>
      {video.description && (
        <div className="text-secondary small mt-2" style={{ whiteSpace: "pre-line" }}>
          {video.description}
        </div>
      )}
    </div>
  );
}

/** Trang xem bài giảng toàn màn hình: video + tài liệu song song (kéo thanh giữa), hoặc chỉ video / tài liệu. */
export function LessonWatchView({ video }: { video: VideoLesson }) {
  const attachment = video.documentUrl ? parseDriveLink(video.documentUrl) : null;

  useEffect(() => {
    const previous = document.title;
    document.title = video.title;
    return () => {
      document.title = previous;
    };
  }, [video.title]);

  return (
    <div className="lesson-watch">
      <WatchHeader video={video} />
      <div className="lesson-watch-body">
        {attachment ? (
          <SplitView
            label="Thanh chia giữa video và tài liệu"
            first={<LessonMedia video={video} />}
            second={
              <iframe
                src={attachment.previewUrl}
                title={`Tài liệu: ${video.title}`}
                className="w-100 h-100 border-0 d-block bg-white"
                allow="autoplay; encrypted-media; fullscreen"
                allowFullScreen
              ></iframe>
            }
          />
        ) : (
          <div className="w-100">
            <LessonMedia video={video} />
          </div>
        )}
      </div>
      {!attachment && <WatchInfo video={video} />}
    </div>
  );
}

function WatchPage({ load, scope }: { load: (id: string) => Promise<VideoLesson>; scope: string }) {
  const { id = "" } = useParams();
  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ["video-watch", scope, id],
    queryFn: () => load(id),
    enabled: !!id,
  });
  if (isLoading) return <Loading />;
  if (error || !data) {
    return (
      <div className="container py-4">
        <ErrorAlert error={error ?? new Error("Không tìm thấy bài giảng.")} onRetry={() => void refetch()} />
      </div>
    );
  }
  return <LessonWatchView video={data} />;
}

export function AdminVideoWatchPage() {
  return <WatchPage load={videosApi.get} scope="admin" />;
}

export function StudentVideoWatchPage() {
  return <WatchPage load={videosApi.getForStudent} scope="student" />;
}
