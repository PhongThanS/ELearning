import { useState } from "react";
import {
  Badge,
  Card,
  Col,
  Container,
  Form,
  Pagination,
  Row,
  Spinner,
} from "react-bootstrap";
import { useQuery } from "@tanstack/react-query";
import { videosApi, studentApi } from "../../services/api";
import type { VideoLesson } from "../../types/api";
import { VideoPlayerModal } from "./AdminVideoPages";

export function StudentVideoPages() {
  const [page, setPage] = useState(1);
  const [keyword, setKeyword] = useState("");
  const [selectedCategory, setSelectedCategory] = useState<string>("");
  const [selectedClassroom, setSelectedClassroom] = useState<string>("");
  const [playingVideo, setPlayingVideo] = useState<VideoLesson | null>(null);

  // Danh mục chuyên đề (từ mục chuyên đề)
  const { data: categoriesData } = useQuery({
    queryKey: ["student-categories"],
    queryFn: () => studentApi.categories(),
    staleTime: 60_000,
  });

  // Lớp học của tôi (từ mục lớp học)
  const { data: myClassesData } = useQuery({
    queryKey: ["student-classes"],
    queryFn: () => studentApi.classes(),
    staleTime: 60_000,
  });

  // Danh sách video bài giảng cho học sinh
  const { data: videosData, isLoading } = useQuery({
    queryKey: ["student-videos", page, keyword, selectedCategory, selectedClassroom],
    queryFn: () =>
      videosApi.listForStudent({
        page,
        pageSize: 12,
        keyword: keyword || undefined,
        categoryId: selectedCategory || undefined,
        classroomId: selectedClassroom || undefined,
      }),
  });

  const categories = categoriesData ?? [];
  const myClasses = myClassesData ?? [];
  const videos: VideoLesson[] = videosData?.items ?? [];
  const totalPages = videosData?.totalPages ?? 1;

  return (
    <Container className="py-4">
      {/* Header Banner */}
      <div className="p-4 p-md-5 mb-4 rounded-4 bg-primary text-white shadow-sm sclass-video-hero">
        <div className="col-md-9 px-0">
          <Badge bg="light" text="primary" className="mb-2 fw-semibold px-2 py-1">
            🎬 HỌC LIỆU TRỰC TUYẾN
          </Badge>
          <h1 className="display-6 fw-bold mb-2">Kho Video Bài Giảng & Hướng Dẫn</h1>
          <p className="lead mb-0 text-white-50">
            Xem lại các bài giảng, video hướng dẫn giải đề thi và tài liệu học tập của thầy cô mọi lúc, mọi nơi.
          </p>
        </div>
      </div>

      {/* Filter Toolbar */}
      <Card className="border-0 shadow-sm mb-4 bg-light-subtle rounded-3">
        <Card.Body className="p-3">
          <Row className="g-2">
            <Col xs={12} md={5}>
              <Form.Control
                type="search"
                placeholder="🔍 Tìm kiếm video theo tiêu đề, nội dung..."
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
                    📁 {c.name}
                  </option>
                ))}
              </Form.Select>
            </Col>
            <Col xs={6} md={4}>
              <Form.Select
                value={selectedClassroom}
                onChange={(e) => {
                  setSelectedClassroom(e.target.value);
                  setPage(1);
                }}
              >
                <option value="">-- Tất cả lớp của tôi --</option>
                {myClasses.map((c) => (
                  <option key={c.id} value={c.id}>
                    🏫 {c.name} {c.schoolYear ? `(${c.schoolYear})` : ""}
                  </option>
                ))}
              </Form.Select>
            </Col>
          </Row>
        </Card.Body>
      </Card>

      {/* Video Content Grid */}
      {isLoading ? (
        <div className="text-center py-5">
          <Spinner animation="border" variant="primary" />
          <div className="text-secondary mt-2 small">Đang tải danh sách bài giảng...</div>
        </div>
      ) : videos.length === 0 ? (
        <Card className="border-0 shadow-sm text-center py-5 rounded-4">
          <Card.Body>
            <div className="display-4 text-muted mb-3">🎥</div>
            <h5 className="fw-bold text-dark">Chưa có video bài giảng nào</h5>
            <p className="text-secondary small mb-0">
              Hiện tại chưa có video nào phù hợp với bộ lọc tìm kiếm. Hãy chọn chuyên đề khác hoặc thử lại sau!
            </p>
          </Card.Body>
        </Card>
      ) : (
        <Row className="g-3">
          {videos.map((v) => {
            const ytId = v.youtubeVideoId;
            const thumb = v.thumbnailUrl || (ytId ? `https://img.youtube.com/vi/${ytId}/mqdefault.jpg` : null);

            return (
              <Col key={v.id} xs={12} sm={6} lg={4} xl={3}>
                <Card
                  className="h-100 border-0 shadow-sm video-card hover-shadow transition-all rounded-3 cursor-pointer"
                  onClick={() => setPlayingVideo(v)}
                  style={{ cursor: "pointer" }}
                >
                  {/* Thumbnail Container */}
                  <div
                    className="position-relative bg-dark video-thumbnail-wrap ratio ratio-16x9"
                    style={{ overflow: "hidden", borderTopLeftRadius: "inherit", borderTopRightRadius: "inherit" }}
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
                        <i className="bi bi-play-circle fs-1"></i>
                      </div>
                    )}
                    {/* Play button overlay */}
                    <div className="position-absolute top-50 start-50 translate-middle video-play-overlay">
                      <div
                        className="btn btn-danger btn-sm rounded-circle d-flex align-items-center justify-content-center shadow"
                        style={{ width: 48, height: 48 }}
                      >
                        <i className="bi bi-play-fill fs-3 ms-0.5"></i>
                      </div>
                    </div>
                    {/* Duration badge */}
                    {v.durationMinutes && (
                      <span className="position-absolute bottom-0 end-0 m-2 badge bg-dark bg-opacity-75 text-white font-monospace small">
                        ⏱️ {v.durationMinutes} phút
                      </span>
                    )}
                  </div>

                  <Card.Body className="d-flex flex-column p-3">
                    {/* Title */}
                    <h6
                      className="fw-bold text-dark text-truncate-2 mb-2 line-clamp-2"
                      title={v.title}
                      style={{ minHeight: "2.6rem" }}
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
                          🌐 Bài giảng chung
                        </Badge>
                      )}
                    </div>

                    {/* Description preview */}
                    {v.description && (
                      <p className="text-muted small mb-3 text-truncate-2 line-clamp-2 flex-grow-1" style={{ fontSize: "0.825rem" }}>
                        {v.description}
                      </p>
                    )}

                    {/* Watch now button */}
                    <div className="pt-2 border-top mt-auto text-end">
                      <span className="text-primary fw-semibold small d-inline-flex align-items-center gap-1">
                        <span>Xem bài giảng</span>
                        <i className="bi bi-arrow-right"></i>
                      </span>
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

      {/* Video Player Modal */}
      {playingVideo && (
        <VideoPlayerModal video={playingVideo} onClose={() => setPlayingVideo(null)} />
      )}
    </Container>
  );
}
