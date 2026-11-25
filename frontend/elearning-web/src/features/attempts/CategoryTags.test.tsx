import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { CategoryTags, type CategoryTag } from "./CategoryTags";

const category = (id: string, name: string, parentId: string | null = null): CategoryTag => ({ id, name, parentId });

const parent = category("12", "Toán 12");
const categories = [
  parent,
  category("10", "Toán 10"),
  category("12.XS", "Toán 12 - Xác suất", "12"),
  category("12.HS", "Toán 12 - Hàm số", "12"),
  category("10.VT", "Toán 10 - Vecto", "10"),
];

describe("CategoryTags", () => {
  it("chỉ hiện chuyên đề con của chuyên đề lớn, bỏ tên cha lặp lại", async () => {
    const onChange = vi.fn();
    render(<CategoryTags parent={parent} categories={categories} value="12" onChange={onChange} />);

    const buttons = within(screen.getByRole("group", { name: "Chuyên đề con của Toán 12" })).getAllByRole("button");
    expect(buttons.map((b) => b.textContent)).toEqual(["Tất cả", "Hàm số", "Xác suất"]);
    expect(screen.getByRole("button", { name: "Tất cả" })).toHaveAttribute("aria-pressed", "true");
    await userEvent.click(screen.getByRole("button", { name: "Xác suất" }));
    expect(onChange).toHaveBeenLastCalledWith("12.XS");
  });

  it("'Tất cả' quay về cả chuyên đề lớn", async () => {
    const onChange = vi.fn();
    render(<CategoryTags parent={parent} categories={categories} value="12.XS" onChange={onChange} />);

    expect(screen.getByRole("button", { name: "Xác suất" })).toHaveAttribute("aria-pressed", "true");
    await userEvent.click(screen.getByRole("button", { name: "Tất cả" }));
    expect(onChange).toHaveBeenLastCalledWith("12");
  });

  it("chuyên đề lớn không có con thì không hiện thẻ", () => {
    const { container } = render(<CategoryTags parent={category("cs", "C#")} categories={categories} value="cs" onChange={vi.fn()} />);

    expect(container).toBeEmptyDOMElement();
  });
});
