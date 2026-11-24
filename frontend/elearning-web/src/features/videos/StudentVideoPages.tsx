import { useState, useMemo } from "react";
import {
  Badge,
  Card,
  Col,
  Container,
  Form,
  Row,
  Spinner,
} from "react-bootstrap";
import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { videosApi, studentApi } from "../../services/api";
import { SearchableSelect, type SearchableOption } from "../../components/common/SearchableSelect";
import type { VideoLesson } from "../../types/api";
import { LessonThumbnail } from "./AdminVideoPages";
import { lessonKind } from "./mediaLinks";
import { openLessonInNewTab } from "./VideoWatchPage";
import { PracticeButton } from "./PracticeButton";
import { HomeworkButton } from "./HomeworkButton";
import { LessonClassBadges } from "./LessonClassBadges";
import { InfiniteSentinel } from "../../components/common/InfiniteScroll";

export function StudentVideoPages() {
  const [keyword, setKeyword] = useState("");
  const [selectedCategory, setSelectedCategory] = useState<string>("");
  const [selectedClassroom, setSelectedClassroom] = useState<string>("");
  // Xem bài giảng: mở trang xem ở tab mới
  const watch = (v: VideoLesson) => openLessonInNewTab("/student/videos", v.id);

  // Danh mục chuyên đề (từ mục chuyên đề)
  const { data: categoriesData } = useQuery({
    queryKey: ["student-categories"],
    queryFn: () => studentApi.categories(),
    staleTime: 5 * 60_000,
  });

  // Lớp học của tôi (từ mục lớp học)
  const { data: myClassesData } = useQuery({
    queryKey: ["student-classes"],
    queryFn: () => studentApi.classes(),
    staleTime: 5 * 60_000,
  });

  // Danh sách video bài giảng cho học sinh
  const videosQuery = useInfiniteQuery({
    queryKey: ["student-videos", "scroll", keyword, selectedCategory, selectedClassroom],
    initialPageParam: 1,
    queryFn: ({ pageParam }) =>
      videosApi.listForStudent({
        page: pageParam,
        pageSize: 12,
        keyword: keyword || undefined,
        categoryId: selectedCategory || undefined,
        classroomId: selectedClassroom || undefined,
      }),
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
  });

  const categories = categoriesData ?? [];
  const myClasses = myClassesData ?? [];
  const isLoading = videosQuery.isLoading;
  const videosData = videosQuery.data?.pages[0];
  const videos: VideoLesson[] = videosQuery.data?.pages.flatMap((p) => p.items) ?? [];

  const categoryOptions = useMemo<SearchableOption[]>(() => {
    return categories.map((c) => ({
      value: c.id,
      label: c.name,
      code: c.code,
    }));
  }, [categories]);

  const classOptions = useMemo<SearchableOption[]>(() => {
    return myClasses.map((c) => ({
      value: c.id,
      label: c.name + (c.schoolYear ? ` (${c.schoolYear})` : ""),
      code: c.code,
    }));
  }, [myClasses]);

  return (
    <Container className="py-4">
      {/* Header Banner */}
      <section className="sclass-hero mb-4">
        <div className="sclass-hero-body">
          <div className="sclass-hero-eyebrow">Học liệu trực tuyến</div>
          <h1 className="sclass-hero-title">
            Kho <span className="text-cyan">video bài giảng</span> & hướng dẫn
          </h1>
          <p className="sclass-hero-text mb-0">
            Xem lại các bài giảng, video hướng dẫn giải đề thi và tài liệu học tập của thầy cô mọi lúc, mọi nơi.
          </p>
          {videosData && (
            <div className="sclass-hero-stat mt-3">
              <i className="bi bi-collection-play-fill me-2" aria-hidden="true" />
              {videosData.totalCount} bài giảng
            </div>
          )}
        </div>
        <div className="sclass-hero-art d-none d-md-block" aria-hidden="true">
          <span className="hero-bubble hero-bubble-a"><i className="bi bi-play-btn-fill" /></span>
          <span className="hero-bubble hero-bubble-b"><i className="bi bi-file-earmark-text-fill" /></span>
          <span className="hero-bubble hero-bubble-c"><i className="bi bi-headphones" /></span>
        </div>
      </section>

      {/* Filter Toolbar */}
      <Card className="filter-bar mb-4">
        <Card.Body className="p-3">
          <Row className="g-2">
            <Col xs={12} md={5}>
              <Form.Control
                type="search"
                placeholder="Tìm kiếm video theo tiêu đề, nội dung..."
                value={keyword}
                onChange={(e) => {
                  setKeyword(e.target.value);
                }}
              />
            </Col>
            <Col xs={6} md={3}>
              <SearchableSelect
                value={selectedCategory}
                onChange={(val) => {
                  setSelectedCategory(val);
                }}
                options={categoryOptions}
                placeholder="-- Tất cả chuyên đề --"
                searchPlaceholder="Tìm theo mã hoặc tên chuyên đề..."
                clearable={true}
              />
            </Col>
            <Col xs={6} md={4}>
              <SearchableSelect
                value={selectedClassroom}
                onChange={(val) => {
                  setSelectedClassroom(val);
                }}
                options={classOptions}
                placeholder="-- Tất cả lớp của tôi --"
                searchPlaceholder="Tìm theo mã hoặc tên lớp..."
                clearable={true}
              />
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
            <div className="display-4 text-muted mb-3"><i className="bi bi-camera-video" aria-hidden="true" /></div>
            <h5 className="fw-bold text-dark">Chưa có video bài giảng nào</h5>
            <p className="text-secondary small mb-0">
              Hiện tại chưa có video nào phù hợp với bộ lọc tìm kiếm. Hãy chọn chuyên đề khác hoặc thử lại sau!
            </p>
          </Card.Body>
        </Card>
      ) : (
        <Row className="g-3">
          {videos.map((v) => {
            const isDoc = lessonKind(v.videoUrl, v.youtubeVideoId) === "drive";

            return (
              <Col key={v.id} xs={12} sm={6} lg={4} xl={3}>
                <Card
                  className="h-100 border-0 shadow-sm video-card hover-shadow transition-all rounded-3 cursor-pointer"
                  onClick={() => watch(v)}
                  style={{ cursor: "pointer" }}
                >
                  {/* Thumbnail Container */}
                  <div
                    className="position-relative bg-dark video-thumbnail-wrap ratio ratio-16x9"
                    style={{ overflow: "hidden", borderTopLeftRadius: "inherit", borderTopRightRadius: "inherit" }}
                  >
                    {/* .ratio kéo mọi con trực tiếp phủ kín khung: gói nội dung vào một khối để nhãn giữ kích thước */}
                    <div>
                      <LessonThumbnail video={v} />
                      {/* Duration badge */}
                      {v.durationMinutes && (
                        <span className="position-absolute bottom-0 end-0 m-2 badge bg-dark bg-opacity-75 text-white font-monospace small">
                          ⏱️ {v.durationMinutes} phút
                        </span>
                      )}
                    </div>
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
                          <i className="bi bi-folder2 me-1" aria-hidden="true" />{v.categoryName}
                        </Badge>
                      )}
                      <LessonClassBadges classrooms={v.classrooms} />
                    </div>

                    {/* Description preview */}
                    {v.description && (
                      <p className="text-muted small mb-3 text-truncate-2 line-clamp-2 flex-grow-1" style={{ fontSize: "0.825rem" }}>
                        {v.description}
                      </p>
                    )}

                    {/* Watch now button + Luyện đề (nếu video có đề luyện) */}
                    <div className="pt-2 border-top mt-auto d-flex align-items-center justify-content-between gap-2">
                      <div className="d-flex flex-wrap gap-2">
                        <PracticeButton video={v} variant="outline-warning" className="fw-semibold" />
                        <HomeworkButton video={v} variant="outline-success" className="fw-semibold" />
                      </div>
                      <span className="text-primary fw-semibold small d-inline-flex align-items-center gap-1 ms-auto">
                        <span>{isDoc ? "Xem tài liệu" : v.documentUrl ? "Xem video + tài liệu" : "Xem bài giảng"}</span>
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

      <InfiniteSentinel hasMore={!!videosQuery.hasNextPage} loading={videosQuery.isFetchingNextPage} onReach={() => void videosQuery.fetchNextPage()} />
    </Container>
  );
}
