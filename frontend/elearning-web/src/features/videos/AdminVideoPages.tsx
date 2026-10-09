import { useState, useId } from "react";
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
import type { VideoLesson, CreateVideoInput, UpdateVideoInput } from "../../types/api";

function extractYoutubeId(url: string): string | null {
  if (!url) return null;
  const trimmed = url.trim();
  const match = trimmed.match(/(?:youtu\.be\/|youtube\.com\/(?:embed\/|v\/|watch\?v=|watch\?.+&v=|shorts\/))([\w-]{11})/i);
  if (match && match[1]) return match[1];
  if (trimmed.length === 11 && /^[\w-]{11}$/.test(trimmed)) return trimmed;
  return null;
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
    staleTime: 60_000,
  });

  const { data: classroomsData } = useQuery({
    queryKey: ["classes", "for-videos"],
    queryFn: () => classesApi.list({ page: 1, pageSize: 100 }),
    staleTime: 60_000,
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
              <Form.Select
                value={selectedCategory}
                onChange={(e) => {
                  setSelectedCategory(e.target.value);
                  setPage(1);
                }}
              >
                <option value="">-- Tất cả chuyên đề --</option>
                {categories.map((c) => (
                  <option key={c.id} value={c.id}>
                    📁 {c.name} ({c.code})
                  </option>
                ))}
              </Form.Select>
            </Col>
            <Col xs={6} md={3}>
              <Form.Select
                value={selectedClassroom}
                onChange={(e) => {
                  setSelectedClassroom(e.target.value);
                  setPage(1);
                }}
              >
                <option value="">-- Tất cả lớp học --</option>
                {classrooms.map((c) => (
                  <option key={c.id} value={c.id}>
                    🏫 {c.name} {c.schoolYear ? `(${c.schoolYear})` : ""}
                  </option>
                ))}
              </Form.Select>
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
            const ytId = v.youtubeVideoId || extractYoutubeId(v.videoUrl);
            const thumb = v.thumbnailUrl || (ytId ? `https://img.youtube.com/vi/${ytId}/mqdefault.jpg` : null);

            return (
              <Col key={v.id} xs={12} sm={6} lg={4} xl={3}>
                <Card className="h-100 border-0 shadow-sm video-card hover-shadow transition-all">
                  {/* Thumbnail Container */}
                  <div
                    className="position-relative bg-dark video-thumbnail-wrap ratio ratio-16x9 cursor-pointer"
                    onClick={() => setPlayingVideo(v)}
                    style={{ cursor: "pointer", overflow: "hidden", borderTopLeftRadius: "inherit", borderTopRightRadius: "inherit" }}
                  >
                    {thumb ? (
                      <img
                        src={thumb}
                        alt={v.title}
                        className="w-100 h-100 object-fit-cover"
                        loading="lazy"
                      />
                    ) : (
                      <div className="d-flex align-items-center justify-content-center text-white-50 h-100">
                        <i className="bi bi-camera-video fs-1"></i>
                      </div>
                    )}
                    {/* Play button overlay */}
                    <div className="position-absolute top-50 start-50 translate-middle video-play-overlay">
                      <div className="btn btn-danger btn-sm rounded-circle d-flex align-items-center justify-content-center shadow" style={{ width: 44, height: 44 }}>
                        <i className="bi bi-play-fill fs-4 ms-0.5"></i>
                      </div>
                    </div>
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
                          title="Xem video"
                          onClick={() => setPlayingVideo(v)}
                        >
                          <i className="bi bi-play-circle"></i>
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
  const descId = useId();
  const catId = useId();
  const classId = useId();
  const durId = useId();
  const orderId = useId();

  const [title, setTitle] = useState(video?.title ?? "");
  const [videoUrl, setVideoUrl] = useState(video?.videoUrl ?? "");
  const [description, setDescription] = useState(video?.description ?? "");
  const [categoryId, setCategoryId] = useState(video?.categoryId ?? "");
  const [classroomId, setClassroomId] = useState(video?.classroomId ?? "");
  const [durationMinutes, setDurationMinutes] = useState<string>(
    video?.durationMinutes ? String(video.durationMinutes) : ""
  );
  const [displayOrder, setDisplayOrder] = useState<number>(video?.displayOrder ?? 0);
  const [errorMsg, setErrorMsg] = useState("");

  const ytId = extractYoutubeId(videoUrl);

  const mutation = useMutation({
    mutationFn: async () => {
      if (isEditing && video) {
        const input: UpdateVideoInput = {
          title: title.trim(),
          videoUrl: videoUrl.trim(),
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
              Đường dẫn link Video (YouTube / URL) <span className="text-danger">*</span>
            </Form.Label>
            <Form.Control
              id={urlId}
              type="text"
              placeholder="https://www.youtube.com/watch?v=... hoặc https://youtu.be/..."
              value={videoUrl}
              onChange={(e) => setVideoUrl(e.target.value)}
              required
            />
            <Form.Text className="text-muted">
              Hệ thống tự động phát hiện video YouTube và nhúng player xem trực tiếp trên web.
            </Form.Text>
          </Form.Group>

          {/* YouTube Preview */}
          {ytId && (
            <div className="mb-3 p-3 bg-light rounded border">
              <div className="small fw-semibold text-secondary mb-2 d-flex align-items-center gap-1">
                <i className="bi bi-youtube text-danger fs-5"></i> Xem trước video YouTube đã nhận diện:
              </div>
              <div className="ratio ratio-16x9 rounded overflow-hidden shadow-sm" style={{ maxHeight: 240 }}>
                <iframe
                  src={`https://www.youtube-nocookie.com/embed/${ytId}`}
                  title="YouTube Preview"
                  allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                  allowFullScreen
                ></iframe>
              </div>
            </div>
          )}

          <Row className="g-3 mb-3">
            <Col md={6}>
              <Form.Group>
                <Form.Label htmlFor={catId} className="fw-semibold">
                  Chuyên đề liên quan (từ mục Chuyên đề)
                </Form.Label>
                <Form.Select id={catId} value={categoryId} onChange={(e) => setCategoryId(e.target.value)}>
                  <option value="">-- Không gắn chuyên đề --</option>
                  {categories.map((c) => (
                    <option key={c.id} value={c.id}>
                      📁 {c.name} ({c.code})
                    </option>
                  ))}
                </Form.Select>
              </Form.Group>
            </Col>
            <Col md={6}>
              <Form.Group>
                <Form.Label htmlFor={classId} className="fw-semibold">
                  Lớp học được xem (từ mục Lớp học)
                </Form.Label>
                <Form.Select id={classId} value={classroomId} onChange={(e) => setClassroomId(e.target.value)}>
                  <option value="">-- Mọi học sinh đều xem được (Chung) --</option>
                  {classrooms.map((c) => (
                    <option key={c.id} value={c.id}>
                      🏫 {c.name} {c.code ? `[${c.code}]` : ""} {c.schoolYear ? `(${c.schoolYear})` : ""}
                    </option>
                  ))}
                </Form.Select>
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

export function VideoPlayerModal({ video, onClose }: { video: VideoLesson; onClose: () => void }) {
  const ytId = video.youtubeVideoId || extractYoutubeId(video.videoUrl);

  return (
    <Modal show={true} onHide={onClose} size="xl" centered dialogClassName="modal-video-player">
      <Modal.Header closeButton className="bg-dark text-white border-0 py-2">
        <Modal.Title className="h6 mb-0 text-truncate text-white d-flex align-items-center gap-2">
          <span>🎬</span> {video.title}
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
                🌐 Video chung
              </Badge>
            )}
            {video.durationMinutes && (
              <span className="text-muted small">⏱️ Thời lượng: {video.durationMinutes} phút</span>
            )}
          </div>
          <a
            href={video.videoUrl}
            target="_blank"
            rel="noreferrer"
            className="btn btn-outline-danger btn-sm d-inline-flex align-items-center gap-1"
          >
            <i className="bi bi-youtube"></i> Xem trên YouTube
          </a>
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
