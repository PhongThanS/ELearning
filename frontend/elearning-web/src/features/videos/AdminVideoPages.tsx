import { useState, useId, useMemo } from "react";
import {
  Badge,
  Button,
  Card,
  Col,
  Form,
  Modal,
  Pagination,
  Row,
  Spinner,
} from "react-bootstrap";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { videosApi, categoriesApi, classesApi } from "../../services/api";
import { SearchableSelect, type SearchableOption } from "../../components/common/SearchableSelect";
import type { VideoLesson, CreateVideoInput, UpdateVideoInput } from "../../types/api";
import { extractYoutubeId, isDirectVideo, lessonKind, lessonThumbnail, parseDriveLink } from "./mediaLinks";
import { SplitView } from "./SplitView";

/** Ảnh bìa thẻ bài giảng: YouTube / trang đầu tệp Drive; lỗi tải ảnh (tệp riêng tư) thì hiện biểu tượng. */
export function LessonThumbnail({ video }: { video: VideoLesson }) {
  const [failed, setFailed] = useState(false);
  const kind = lessonKind(video.videoUrl, video.youtubeVideoId);
  const thumb = lessonThumbnail(video.videoUrl, video.youtubeVideoId, video.thumbnailUrl);
  const isDoc = kind === "drive";
  return (
    <>
      {thumb && !failed ? (
        <img
          src={thumb}
          alt={video.title}
          className={`w-100 h-100 ${isDoc ? "object-fit-cover object-position-top bg-white" : "object-fit-cover"}`}
          loading="lazy"
          referrerPolicy="no-referrer"
          onError={() => setFailed(true)}
        />
      ) : (
        <div className={`d-flex align-items-center justify-content-center h-100 ${isDoc ? "bg-primary-subtle text-primary" : "text-white-50"}`}>
          <i className={`bi ${isDoc ? "bi-file-earmark-text" : "bi-camera-video"} fs-1`}></i>
        </div>
      )}
      <div className="position-absolute top-50 start-50 translate-middle video-play-overlay">
        <div
          className={`btn ${isDoc ? "btn-primary" : "btn-danger"} btn-sm rounded-circle d-flex align-items-center justify-content-center shadow`}
          style={{ width: 46, height: 46 }}
        >
          <i className={`bi ${isDoc ? "bi-eye-fill fs-5" : "bi-play-fill fs-4"}`}></i>
        </div>
      </div>
      {isDoc && (
        <span className="position-absolute bottom-0 start-0 m-2 badge bg-primary shadow-sm">
          <i className="bi bi-google me-1"></i>Tài liệu Drive
        </span>
      )}
      {!isDoc && video.documentUrl && (
        <span className="position-absolute bottom-0 start-0 m-2 badge bg-primary shadow-sm">
          <i className="bi bi-file-earmark-pdf me-1"></i>Kèm tài liệu PDF
        </span>
      )}
    </>
  );
}

export function AdminVideoPages() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [keyword, setKeyword] = useState("");
  const [selectedCategory, setSelectedCategory] = useState<string>("");
  const [selectedClassroom, setSelectedClassroom] = useState<string>("");
  const [activeFilter, setActiveFilter] = useState<string>("all");

  // Modal thêm / sửa
  const [showEditModal, setShowEditModal] = useState(false);
  const [editingVideo, setEditingVideo] = useState<VideoLesson | null>(null);

  // Modal xem video
  const [playingVideo, setPlayingVideo] = useState<VideoLesson | null>(null);

  // Modal xóa
  const [deletingVideo, setDeletingVideo] = useState<VideoLesson | null>(null);

  // Queries - Dữ liệu lấy trực tiếp từ mục Chuyên đề và mục Lớp học
  const { data: categoriesData } = useQuery({
    queryKey: ["categories", "for-videos"],
    queryFn: () => categoriesApi.list({ page: 1, pageSize: 100 }),
    staleTime: 5 * 60_000,
  });

  const { data: classroomsData } = useQuery({
    queryKey: ["classes", "for-videos"],
    queryFn: () => classesApi.list({ page: 1, pageSize: 100 }),
    staleTime: 5 * 60_000,
  });

  const { data: videosData, isLoading } = useQuery({
    queryKey: ["admin-videos", page, keyword, selectedCategory, selectedClassroom, activeFilter],
    queryFn: () =>
      videosApi.list({
        page,
        pageSize: 12,
        keyword: keyword || undefined,
        categoryId: selectedCategory || undefined,
        classroomId: selectedClassroom || undefined,
        isActive: activeFilter === "all" ? undefined : activeFilter === "true",
      }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => videosApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-videos"] });
      setDeletingVideo(null);
    },
  });

  const toggleStatusMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      videosApi.setStatus(id, isActive),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-videos"] });
    },
  });

  const categories = categoriesData?.items ?? [];
  const classrooms = classroomsData?.items ?? [];
  const videos: VideoLesson[] = videosData?.items ?? [];
  const totalPages = videosData?.totalPages ?? 1;

  const categoryOptions = useMemo<SearchableOption[]>(() => {
    return categories.map((c) => ({
      value: c.id,
      label: c.name,
      code: c.code,
    }));
  }, [categories]);

  const classroomOptions = useMemo<SearchableOption[]>(() => {
    return classrooms.map((c) => ({
      value: c.id,
      label: c.name + (c.schoolYear ? ` (${c.schoolYear})` : ""),
      code: c.code,
    }));
  }, [classrooms]);

  const handleOpenAdd = () => {
    setEditingVideo(null);
    setShowEditModal(true);
  };

  const handleOpenEdit = (v: VideoLesson) => {
    setEditingVideo(v);
    setShowEditModal(true);
  };

  return (
    <div className="container-fluid px-0">
      {/* Header */}
      <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 mb-4">
        <div>
          <h1 className="h3 fw-bold text-dark mb-1 d-flex align-items-center gap-2">
            <span>🎥</span> Kho Video bài giảng
          </h1>
          <p className="text-secondary mb-0 small">
            Quản lý danh sách video bài học, hướng dẫn giải đề bằng link YouTube theo chuyên đề hoặc lớp học.
          </p>
        </div>
        <Button variant="primary" className="d-flex align-items-center gap-2 shadow-sm" onClick={handleOpenAdd}>
          <i className="bi bi-plus-lg"></i>
          <span>Thêm video mới</span>
        </Button>
      </div>

      {/* Filter Bar */}
      <Card className="border-0 shadow-sm mb-4 bg-light-subtle">
        <Card.Body className="p-3">
          <Row className="g-2">
            <Col xs={12} md={4}>
              <Form.Control
                type="search"
                placeholder="🔍 Tìm kiếm theo tiêu đề, mô tả..."
                value={keyword}
                onChange={(e) => {
                  setKeyword(e.target.value);
                  setPage(1);
                }}
              />
            </Col>
            <Col xs={6} md={3}>
              <SearchableSelect
                value={selectedCategory}
                onChange={(val) => {
                  setSelectedCategory(val);
                  setPage(1);
                }}
                options={categoryOptions}
                placeholder="-- Tất cả chuyên đề --"
                searchPlaceholder="Tìm theo mã hoặc tên chuyên đề..."
                clearable={true}
              />
            </Col>
            <Col xs={6} md={3}>
              <SearchableSelect
                value={selectedClassroom}
                onChange={(val) => {
                  setSelectedClassroom(val);
                  setPage(1);
                }}
                options={classroomOptions}
                placeholder="-- Tất cả lớp học --"
                searchPlaceholder="Tìm theo mã hoặc tên lớp..."
                clearable={true}
              />
            </Col>
            <Col xs={12} md={2}>
              <Form.Select
                value={activeFilter}
                onChange={(e) => {
                  setActiveFilter(e.target.value);
                  setPage(1);
                }}
              >
                <option value="all">Tất cả trạng thái</option>
                <option value="true">Đang hiển thị</option>
                <option value="false">Đã ẩn</option>
              </Form.Select>
            </Col>
          </Row>
        </Card.Body>
      </Card>

      {/* Video Grid */}
      {isLoading ? (
        <div className="text-center py-5">
          <Spinner animation="border" variant="primary" />
          <div className="text-secondary mt-2 small">Đang tải danh sách video bài giảng...</div>
        </div>
      ) : videos.length === 0 ? (
        <Card className="border-0 shadow-sm text-center py-5">
          <Card.Body>
            <div className="display-4 text-muted mb-3">🎬</div>
            <h5 className="fw-bold text-dark">Chưa có video bài giảng nào</h5>
            <p className="text-secondary small mb-3">
              Hãy thêm video bài học hoặc liên kết video YouTube đầu tiên để học sinh theo dõi.
            </p>
            <Button variant="primary" onClick={handleOpenAdd}>
              <i className="bi bi-plus-lg me-1"></i> Thêm video ngay
            </Button>
          </Card.Body>
        </Card>
      ) : (
        <Row className="g-3">
          {videos.map((v) => {
            return (
              <Col key={v.id} xs={12} sm={6} lg={4} xl={3}>
                <Card className="h-100 border-0 shadow-sm video-card hover-shadow transition-all">
                  {/* Thumbnail Container */}
                  <div
                    className="position-relative bg-dark video-thumbnail-wrap ratio ratio-16x9 cursor-pointer"
                    onClick={() => setPlayingVideo(v)}
                    style={{ cursor: "pointer", overflow: "hidden", borderTopLeftRadius: "inherit", borderTopRightRadius: "inherit" }}
                  >
                    <LessonThumbnail video={v} />
                    {/* Duration badge */}
                    {v.durationMinutes && (
                      <span className="position-absolute bottom-0 end-0 m-2 badge bg-dark bg-opacity-75 text-white font-monospace small">
                        ⏱️ {v.durationMinutes} phút
                      </span>
                    )}
                    {/* Active status badge */}
                    <span className="position-absolute top-0 start-0 m-2">
                      {v.isActive ? (
                        <Badge bg="success" className="shadow-sm">Hiển thị</Badge>
                      ) : (
                        <Badge bg="secondary" className="shadow-sm">Đã ẩn</Badge>
                      )}
                    </span>
                  </div>

                  <Card.Body className="d-flex flex-column p-3">
                    {/* Title */}
                    <h6
                      className="fw-bold text-dark text-truncate-2 mb-2 cursor-pointer line-clamp-2"
                      title={v.title}
                      onClick={() => setPlayingVideo(v)}
                      style={{ minHeight: "2.6rem", cursor: "pointer" }}
                    >
                      {v.title}
                    </h6>

                    {/* Metadata tags */}
                    <div className="d-flex flex-wrap gap-1 mb-2">
                      {v.categoryName && (
                        <Badge bg="primary-subtle" className="text-primary border border-primary-subtle fw-normal">
                          📁 {v.categoryName}
                        </Badge>
                      )}
                      {v.classroomName ? (
                        <Badge bg="info-subtle" className="text-info-emphasis border border-info-subtle fw-normal">
                          🏫 {v.classroomName}
                        </Badge>
                      ) : (
                        <Badge bg="secondary-subtle" className="text-secondary border border-secondary-subtle fw-normal">
                          🌐 Chung cho tất cả
                        </Badge>
                      )}
                    </div>

                    {/* Description preview */}
                    {v.description && (
                      <p className="text-muted small mb-3 text-truncate-2 line-clamp-2 flex-grow-1" style={{ fontSize: "0.825rem" }}>
                        {v.description}
                      </p>
                    )}

                    {/* Footer Actions */}
                    <div className="d-flex align-items-center justify-content-between pt-2 border-top mt-auto">
                      <Form.Check
                        type="switch"
                        id={`switch-${v.id}`}
                        checked={v.isActive}
                        title={v.isActive ? "Đang hiển thị - bấm để ẩn" : "Đang ẩn - bấm để hiển thị"}
                        onChange={(e) =>
                          toggleStatusMutation.mutate({ id: v.id, isActive: e.target.checked })
                        }
                        label={<span className="small text-secondary">{v.isActive ? "Bật" : "Tắt"}</span>}
                      />
                      <div className="d-flex gap-1">
                        <Button
                          variant="outline-primary"
                          size="sm"
                          className="px-2 py-1"
                          title="Xem"
                          onClick={() => setPlayingVideo(v)}
                        >
                          <i className="bi bi-eye"></i>
                        </Button>
                        <Button
                          variant="outline-secondary"
                          size="sm"
                          className="px-2 py-1"
                          title="Chỉnh sửa"
                          onClick={() => handleOpenEdit(v)}
                        >
                          <i className="bi bi-pencil"></i>
                        </Button>
                        <Button
                          variant="outline-danger"
                          size="sm"
                          className="px-2 py-1"
                          title="Xóa video"
                          onClick={() => setDeletingVideo(v)}
                        >
                          <i className="bi bi-trash"></i>
                        </Button>
                      </div>
                    </div>
                  </Card.Body>
                </Card>
              </Col>
            );
          })}
        </Row>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="d-flex justify-content-center mt-4">
          <Pagination>
            <Pagination.Prev disabled={page === 1} onClick={() => setPage((p) => p - 1)} />
            {Array.from({ length: totalPages }, (_, i) => (
              <Pagination.Item key={i + 1} active={i + 1 === page} onClick={() => setPage(i + 1)}>
                {i + 1}
              </Pagination.Item>
            ))}
            <Pagination.Next disabled={page === totalPages} onClick={() => setPage((p) => p + 1)} />
          </Pagination>
        </div>
      )}

      {/* Modal Thêm / Chỉnh sửa */}
      {showEditModal && (
        <VideoEditModal
          video={editingVideo}
          categories={categories}
          classrooms={classrooms}
          onClose={() => setShowEditModal(false)}
          onSuccess={() => {
            setShowEditModal(false);
            queryClient.invalidateQueries({ queryKey: ["admin-videos"] });
          }}
        />
      )}

      {/* Modal Xem Video Player */}
      {playingVideo && (
        <VideoPlayerModal video={playingVideo} onClose={() => setPlayingVideo(null)} />
      )}

      {/* Modal Xác nhận xóa */}
      {deletingVideo && (
        <Modal show={true} onHide={() => setDeletingVideo(null)} centered>
          <Modal.Header closeButton>
            <Modal.Title className="h5 fw-bold text-danger">Xác nhận xóa video</Modal.Title>
          </Modal.Header>
          <Modal.Body>
            Bạn có chắc chắn muốn xóa video <strong>{deletingVideo.title}</strong>? Thao tác này không thể hoàn tác.
          </Modal.Body>
          <Modal.Footer>
            <Button variant="secondary" onClick={() => setDeletingVideo(null)}>
              Hủy bỏ
            </Button>
            <Button
              variant="danger"
              disabled={deleteMutation.isPending}
              onClick={() => deleteMutation.mutate(deletingVideo.id)}
            >
              {deleteMutation.isPending ? <Spinner size="sm" animation="border" /> : "Xóa ngay"}
            </Button>
          </Modal.Footer>
        </Modal>
      )}
    </div>
  );
}

function VideoEditModal({
  video,
  categories,
  classrooms,
  onClose,
  onSuccess,
}: {
  video: VideoLesson | null;
  categories: { id: string; name: string; code?: string }[];
  classrooms: { id: string; name: string; code?: string; schoolYear?: string | null }[];
  onClose: () => void;
  onSuccess: () => void;
}) {
  const isEditing = Boolean(video);
  const titleId = useId();
  const urlId = useId();
  const docId = useId();
  const descId = useId();
  const catId = useId();
  const classId = useId();
  const durId = useId();
  const orderId = useId();

  const [title, setTitle] = useState(video?.title ?? "");
  const [videoUrl, setVideoUrl] = useState(video?.videoUrl ?? "");
  const [documentUrl, setDocumentUrl] = useState(video?.documentUrl ?? "");
  const attachment = parseDriveLink(documentUrl);
  const [description, setDescription] = useState(video?.description ?? "");
  const [categoryId, setCategoryId] = useState(video?.categoryId ?? "");
  const [classroomId, setClassroomId] = useState(video?.classroomId ?? "");
  const [durationMinutes, setDurationMinutes] = useState<string>(
    video?.durationMinutes ? String(video.durationMinutes) : ""
  );
  const [displayOrder, setDisplayOrder] = useState<number>(video?.displayOrder ?? 0);
  const [errorMsg, setErrorMsg] = useState("");

  const ytId = extractYoutubeId(videoUrl);
  const driveUrl = parseDriveLink(videoUrl)?.previewUrl ?? null;
  const isDirect = isDirectVideo(videoUrl);

  const mutation = useMutation({
    mutationFn: async () => {
      if (isEditing && video) {
        const input: UpdateVideoInput = {
          title: title.trim(),
          videoUrl: videoUrl.trim(),
          documentUrl: documentUrl.trim() || null,
          description: description.trim() || null,
          categoryId: categoryId || null,
          classroomId: classroomId || null,
          durationMinutes: durationMinutes ? parseInt(durationMinutes, 10) : null,
          displayOrder,
          isActive: video.isActive,
          rowVersion: video.rowVersion,
        };
        return videosApi.update(video.id, input);
      } else {
        const input: CreateVideoInput = {
          title: title.trim(),
          videoUrl: videoUrl.trim(),
          documentUrl: documentUrl.trim() || null,
          description: description.trim() || null,
          categoryId: categoryId || null,
          classroomId: classroomId || null,
          durationMinutes: durationMinutes ? parseInt(durationMinutes, 10) : null,
          displayOrder,
        };
        return videosApi.create(input);
      }
    },
    onSuccess,
    onError: (err: { message?: string }) => {
      setErrorMsg(err.message || "Có lỗi xảy ra khi lưu video.");
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) {
      setErrorMsg("Vui lòng nhập tiêu đề video.");
      return;
    }
    if (!videoUrl.trim()) {
      setErrorMsg("Vui lòng nhập đường dẫn liên kết video.");
      return;
    }
    if (documentUrl.trim() && !attachment) {
      setErrorMsg("Link tài liệu phải là link Google Drive (https://drive.google.com/file/d/...) hoặc Google Docs.");
      return;
    }
    setErrorMsg("");
    mutation.mutate();
  };

  return (
    <Modal show={true} onHide={onClose} size="lg" centered backdrop="static">
      <Form onSubmit={handleSubmit}>
        <Modal.Header closeButton>
          <Modal.Title className="h5 fw-bold text-dark">
            {isEditing ? "✏️ Chỉnh sửa video bài giảng" : "➕ Thêm video bài giảng mới"}
          </Modal.Title>
        </Modal.Header>
        <Modal.Body className="p-4">
          {errorMsg && <div className="alert alert-danger py-2 small mb-3">{errorMsg}</div>}

          <Form.Group className="mb-3">
            <Form.Label htmlFor={titleId} className="fw-semibold">
              Tiêu đề video <span className="text-danger">*</span>
            </Form.Label>
            <Form.Control
              id={titleId}
              type="text"
              placeholder="Ví dụ: Bài 01: Giới thiệu lập trình C# cơ bản"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              required
            />
          </Form.Group>

          <Form.Group className="mb-3">
            <Form.Label htmlFor={urlId} className="fw-semibold">
              Đường dẫn (YouTube / Google Drive / link video) <span className="text-danger">*</span>
            </Form.Label>
            <Form.Control
              id={urlId}
              type="text"
              placeholder="https://www.youtube.com/watch?v=...  hoặc  https://drive.google.com/file/d/.../view"
              value={videoUrl}
              onChange={(e) => setVideoUrl(e.target.value)}
              required
            />
            <Form.Text className="text-muted">
              Hệ thống tự nhận video YouTube và tệp Google Drive (PDF, Word, Excel, PowerPoint, ảnh, video…; cả Google Docs / Sheets / Slides) để xem ngay trên web.
              Với Drive: chia sẻ tệp ở chế độ <strong>"Bất kỳ ai có đường liên kết"</strong> thì học viên mới xem được.
            </Form.Text>
          </Form.Group>

          <Form.Group className="mb-3">
            <Form.Label htmlFor={docId} className="fw-semibold">
              Tài liệu PDF đi kèm (Google Drive)
            </Form.Label>
            <Form.Control
              id={docId}
              type="text"
              placeholder="https://drive.google.com/file/d/.../view"
              value={documentUrl}
              onChange={(e) => setDocumentUrl(e.target.value)}
              isInvalid={!!documentUrl.trim() && !attachment}
              isValid={!!attachment}
            />
            <Form.Control.Feedback type="invalid">Chỉ nhận link Google Drive / Google Docs.</Form.Control.Feedback>
            <Form.Text className="text-muted">
              Không bắt buộc. Có cả video và tài liệu thì khi xem, video và tài liệu hiện <strong>song song</strong>, kéo thanh giữa
              để co giãn hai bên. Chia sẻ tệp ở chế độ <strong>"Bất kỳ ai có đường liên kết"</strong>.
            </Form.Text>
          </Form.Group>

          {/* Video Preview */}
          {(ytId || driveUrl || isDirect) && (
            <div className="mb-3 p-3 bg-light rounded border">
              <div className="small fw-semibold text-secondary mb-2 d-flex align-items-center gap-1">
                {ytId ? (
                  <>
                    <i className="bi bi-youtube text-danger fs-5"></i> Xem trước video YouTube đã nhận diện:
                  </>
                ) : driveUrl ? (
                  <>
                    <i className="bi bi-google text-primary fs-5"></i> Xem trước tệp Google Drive:
                  </>
                ) : (
                  <>
                    <i className="bi bi-file-earmark-play-fill text-info fs-5"></i> Xem trước video tệp trực tiếp:
                  </>
                )}
              </div>
              <div
                className={`rounded overflow-hidden shadow-sm ${driveUrl && !ytId ? "" : "ratio ratio-16x9"}`}
                style={driveUrl && !ytId ? { height: 360 } : { maxHeight: 240 }}
              >
                {ytId ? (
                  <iframe
                    src={`https://www.youtube-nocookie.com/embed/${ytId}`}
                    title="YouTube Preview"
                    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                    allowFullScreen
                  ></iframe>
                ) : driveUrl ? (
                  <iframe
                    src={driveUrl}
                    title="Drive Preview"
                    className="w-100 h-100 border-0"
                    allow="autoplay"
                    allowFullScreen
                  ></iframe>
                ) : (
                  <video src={videoUrl} controls className="w-100 h-100 bg-black">
                    Trình duyệt không hỗ trợ thẻ video.
                  </video>
                )}
              </div>
            </div>
          )}

          <Row className="g-3 mb-3">
            <Col md={6}>
              <Form.Group>
                <Form.Label htmlFor={catId} className="fw-semibold">
                  Chuyên đề liên quan (từ mục Chuyên đề)
                </Form.Label>
                <SearchableSelect
                  id={catId}
                  value={categoryId}
                  onChange={(val) => setCategoryId(val)}
                  options={categories.map((c) => ({
                    value: c.id,
                    label: c.name,
                    code: c.code,
                  }))}
                  placeholder="-- Không gắn chuyên đề --"
                  searchPlaceholder="Tìm theo mã hoặc tên chuyên đề..."
                  clearable={true}
                />
              </Form.Group>
            </Col>
            <Col md={6}>
              <Form.Group>
                <Form.Label htmlFor={classId} className="fw-semibold">
                  Lớp học được xem (từ mục Lớp học)
                </Form.Label>
                <SearchableSelect
                  id={classId}
                  value={classroomId}
                  onChange={(val) => setClassroomId(val)}
                  options={classrooms.map((c) => ({
                    value: c.id,
                    label: c.name + (c.schoolYear ? ` (${c.schoolYear})` : ""),
                    code: c.code,
                  }))}
                  placeholder="-- Mọi học sinh đều xem được (Chung) --"
                  searchPlaceholder="Tìm theo mã hoặc tên lớp..."
                  clearable={true}
                />
              </Form.Group>
            </Col>
          </Row>

          <Row className="g-3 mb-3">
            <Col md={6}>
              <Form.Group>
                <Form.Label htmlFor={durId} className="fw-semibold">
                  Thời lượng ước tính (phút)
                </Form.Label>
                <Form.Control
                  id={durId}
                  type="number"
                  min="0"
                  placeholder="Ví dụ: 45"
                  value={durationMinutes}
                  onChange={(e) => setDurationMinutes(e.target.value)}
                />
              </Form.Group>
            </Col>
            <Col md={6}>
              <Form.Group>
                <Form.Label htmlFor={orderId} className="fw-semibold">
                  Thứ tự sắp xếp
                </Form.Label>
                <Form.Control
                  id={orderId}
                  type="number"
                  value={displayOrder}
                  onChange={(e) => setDisplayOrder(parseInt(e.target.value, 10) || 0)}
                />
              </Form.Group>
            </Col>
          </Row>

          <Form.Group>
            <Form.Label htmlFor={descId} className="fw-semibold">
              Mô tả / Ghi chú nội dung
            </Form.Label>
            <Form.Control
              id={descId}
              as="textarea"
              rows={3}
              placeholder="Tóm tắt nội dung bài giảng, tài liệu tham khảo hoặc câu hỏi cần chú ý..."
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </Form.Group>
        </Modal.Body>
        <Modal.Footer>
          <Button variant="secondary" onClick={onClose}>
            Hủy
          </Button>
          <Button variant="primary" type="submit" disabled={mutation.isPending}>
            {mutation.isPending ? <Spinner size="sm" animation="border" className="me-1" /> : null}
            {isEditing ? "Lưu thay đổi" : "Tạo video mới"}
          </Button>
        </Modal.Footer>
      </Form>
    </Modal>
  );
}

/** Video (YouTube / Drive / file trực tiếp) lấp đầy khung chứa. */
function LessonMedia({ video }: { video: VideoLesson }) {
  const ytId = video.youtubeVideoId || extractYoutubeId(video.videoUrl);
  const drive = parseDriveLink(video.videoUrl);
  if (ytId) {
    return (
      <iframe
        src={`https://www.youtube-nocookie.com/embed/${ytId}?rel=0`}
        title={video.title}
        className="w-100 h-100 border-0 d-block bg-black"
        allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
        allowFullScreen
      ></iframe>
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
      <video src={video.videoUrl} controls className="w-100 h-100 d-block bg-black">
        Trình duyệt của bạn không hỗ trợ phát thẻ video trực tiếp.
      </video>
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

export function VideoPlayerModal({ video, onClose }: { video: VideoLesson; onClose: () => void }) {
  const ytId = video.youtubeVideoId || extractYoutubeId(video.videoUrl);
  const drive = parseDriveLink(video.videoUrl);
  const driveUrl = drive?.previewUrl ?? null;
  const isDirect = isDirectVideo(video.videoUrl);
  const isDoc = !ytId && !!drive;
  const attachment = video.documentUrl ? parseDriveLink(video.documentUrl) : null;

  // Video + tài liệu: toàn màn hình, video một bên, tài liệu một bên, thanh giữa kéo để co giãn
  if (attachment) {
    return (
      <Modal show={true} onHide={onClose} fullscreen dialogClassName="modal-doc-viewer">
        <Modal.Header closeButton closeVariant="white" className="bg-dark text-white border-0 py-2 gap-2">
          <Modal.Title className="h6 mb-0 text-white d-flex align-items-center gap-2 flex-grow-1 min-w-0">
            <i className="bi bi-collection-play" aria-hidden="true"></i>
            <span className="text-truncate">{video.title}</span>
          </Modal.Title>
          {ytId && (
            <a
              href={`https://www.youtube.com/watch?v=${ytId}`}
              target="_blank"
              rel="noreferrer"
              className="btn btn-sm btn-outline-light d-inline-flex align-items-center gap-1 text-nowrap"
            >
              <i className="bi bi-youtube" aria-hidden="true"></i>
              <span className="d-none d-md-inline">YouTube</span>
            </a>
          )}
          <a
            href={attachment.openUrl}
            target="_blank"
            rel="noreferrer"
            className="btn btn-sm btn-outline-light d-inline-flex align-items-center gap-1 text-nowrap"
          >
            <i className="bi bi-box-arrow-up-right" aria-hidden="true"></i>
            <span className="d-none d-md-inline">Mở tài liệu trong Drive</span>
          </a>
        </Modal.Header>
        <Modal.Body className="p-0 bg-dark d-flex">
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
        </Modal.Body>
      </Modal>
    );
  }

  // Tài liệu Drive: xem toàn màn hình như trang xem của Google Drive (thanh trên gọn, tài liệu chiếm hết phần còn lại)
  if (isDoc && drive) {
    return (
      <Modal show={true} onHide={onClose} fullscreen dialogClassName="modal-doc-viewer">
        <Modal.Header closeButton closeVariant="white" className="bg-dark text-white border-0 py-2 gap-2">
          <Modal.Title className="h6 mb-0 text-white d-flex align-items-center gap-2 flex-grow-1 min-w-0">
            <i className="bi bi-file-earmark-text" aria-hidden="true"></i>
            <span className="text-truncate">{video.title}</span>
          </Modal.Title>
          <a
            href={drive.openUrl}
            target="_blank"
            rel="noreferrer"
            className="btn btn-sm btn-outline-light d-inline-flex align-items-center gap-1 text-nowrap"
          >
            <i className="bi bi-box-arrow-up-right" aria-hidden="true"></i>
            <span className="d-none d-sm-inline">Mở trong Google Drive</span>
          </a>
        </Modal.Header>
        <Modal.Body className="p-0 bg-dark d-flex">
          <iframe
            src={drive.previewUrl}
            title={video.title}
            className="w-100 flex-grow-1 border-0 bg-white"
            allow="autoplay; encrypted-media; fullscreen"
            allowFullScreen
          ></iframe>
        </Modal.Body>
      </Modal>
    );
  }

  return (
    <Modal show={true} onHide={onClose} size="xl" centered dialogClassName="modal-video-player">
      <Modal.Header closeButton className="bg-dark text-white border-0 py-2">
        <Modal.Title className="h6 mb-0 text-truncate text-white d-flex align-items-center gap-2">
          <i className={`bi ${isDoc ? "bi-file-earmark-text" : "bi-camera-reels"}`}></i> {video.title}
        </Modal.Title>
      </Modal.Header>
      <Modal.Body className="p-0 bg-black">
        {ytId ? (
          <div className="ratio ratio-16x9">
            <iframe
              src={`https://www.youtube-nocookie.com/embed/${ytId}?autoplay=1&rel=0`}
              title={video.title}
              allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
              allowFullScreen
            ></iframe>
          </div>
        ) : driveUrl ? (
          <div className="bg-white" style={{ height: "75vh" }}>
            <iframe
              src={driveUrl}
              title={video.title}
              className="w-100 h-100 border-0"
              allow="autoplay; encrypted-media; fullscreen"
              allowFullScreen
            ></iframe>
          </div>
        ) : isDirect ? (
          <div className="ratio ratio-16x9 bg-black">
            <video
              src={video.videoUrl}
              controls
              autoPlay
              className="w-100 h-100"
            >
              Trình duyệt của bạn không hỗ trợ phát thẻ video trực tiếp.
            </video>
          </div>
        ) : (
          <div className="p-4 text-center text-white">
            <p className="mb-3">Không thể nhúng video này trực tiếp. Vui lòng mở bằng liên kết:</p>
            <a href={video.videoUrl} target="_blank" rel="noreferrer" className="btn btn-primary">
              <i className="bi bi-box-arrow-up-right me-1"></i> Mở liên kết video
            </a>
          </div>
        )}
      </Modal.Body>
      <div className="bg-light p-3 border-top">
        <div className="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-2">
          <div className="d-flex flex-wrap gap-2 align-items-center">
            {video.categoryName && (
              <Badge bg="primary-subtle" className="text-primary border border-primary-subtle">
                📁 {video.categoryName}
              </Badge>
            )}
            {video.classroomName ? (
              <Badge bg="info-subtle" className="text-info-emphasis border border-info-subtle">
                🏫 {video.classroomName}
              </Badge>
            ) : (
              <Badge bg="secondary-subtle" className="text-secondary border border-secondary-subtle">
                🌐 Bài giảng chung
              </Badge>
            )}
            {video.durationMinutes && (
              <span className="text-muted small">⏱️ Thời lượng: {video.durationMinutes} phút</span>
            )}
          </div>
          {ytId ? (
            <a
              href={`https://www.youtube.com/watch?v=${ytId}`}
              target="_blank"
              rel="noreferrer"
              className="btn btn-outline-danger btn-sm d-inline-flex align-items-center gap-1"
            >
              <i className="bi bi-youtube"></i> Xem trên YouTube
            </a>
          ) : drive ? (
            <a
              href={drive.openUrl}
              target="_blank"
              rel="noreferrer"
              className="btn btn-outline-primary btn-sm d-inline-flex align-items-center gap-1"
            >
              <i className="bi bi-google"></i> Mở trong Google Drive
            </a>
          ) : (
            <a
              href={video.videoUrl}
              target="_blank"
              rel="noreferrer"
              className="btn btn-outline-secondary btn-sm d-inline-flex align-items-center gap-1"
            >
              <i className="bi bi-box-arrow-up-right"></i> Mở liên kết
            </a>
          )}
        </div>
        {video.description && (
          <div className="text-secondary small mt-2" style={{ whiteSpace: "pre-line" }}>
            {video.description}
          </div>
        )}
      </div>
    </Modal>
  );
}
