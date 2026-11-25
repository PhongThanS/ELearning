import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { LessonWatchView } from "./VideoWatchPage";
import type { VideoLesson } from "../../types/api";

const lesson = {
  id: "v1",
  title: "Giới hạn hàm số",
  videoUrl: "https://youtu.be/abcdefghijk",
  documentUrl: "https://drive.google.com/file/d/1234567890abcdef/view",
  classrooms: [],
} as unknown as VideoLesson;

describe("LessonWatchView", () => {
  it("khung tài liệu luôn có liên kết mở trực tiếp (điện thoại có thể không xem được bản nhúng)", () => {
    render(
      <MemoryRouter>
        <LessonWatchView video={lesson} />
      </MemoryRouter>,
    );

    const link = screen.getByRole("link", { name: "Mở tài liệu trong tab mới" });
    expect(link).toHaveAttribute("href", "https://drive.google.com/file/d/1234567890abcdef/view");
    expect(link).toHaveAttribute("target", "_blank");
    expect(screen.getByTitle("Tài liệu: Giới hạn hàm số")).toHaveAttribute("src", "https://drive.google.com/file/d/1234567890abcdef/preview");
    // Nạp không kèm cookie Google để không bị chuyển sang trang đăng nhập
    expect(screen.getByTitle("Tài liệu: Giới hạn hàm số")).toHaveAttribute("credentialless");
  });
});
