import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SplitView, clampSplit, splitTemplate } from "./SplitView";

describe("SplitView", () => {
  it("giới hạn tỉ lệ trong 15–85%", () => {
    expect(clampSplit(3)).toBe(15);
    expect(clampSplit(99)).toBe(85);
    expect(clampSplit(42.345)).toBe(42.3);
  });

  it("mỗi khung có kích thước tối thiểu để kéo nhỏ lại không bị che mất", () => {
    expect(splitTemplate(15, false)).toBe("1fr / minmax(240px, 15fr) auto minmax(240px, 85fr)");
    expect(splitTemplate(85, true)).toBe("minmax(160px, 85fr) auto minmax(160px, 15fr) / 1fr");
  });

  it("co giãn bằng phím mũi tên, Home / End và nhấp đúp để chia đều", () => {
    render(<SplitView label="Thanh chia" first={<p>Video</p>} second={<p>Tài liệu</p>} />);
    const handle = screen.getByRole("separator", { name: "Thanh chia" });
    expect(handle).toHaveAttribute("aria-valuenow", "50");

    fireEvent.keyDown(handle, { key: "ArrowRight" });
    expect(handle).toHaveAttribute("aria-valuenow", "55");
    fireEvent.keyDown(handle, { key: "Home" });
    expect(handle).toHaveAttribute("aria-valuenow", "15");
    fireEvent.doubleClick(handle);
    expect(handle).toHaveAttribute("aria-valuenow", "50");
    expect(screen.getByText("Video")).toBeInTheDocument();
    expect(screen.getByText("Tài liệu")).toBeInTheDocument();
  });
});
