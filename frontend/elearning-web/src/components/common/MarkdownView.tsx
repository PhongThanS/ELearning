import ReactMarkdown, { type Components } from "react-markdown";
import remarkGfm from "remark-gfm";
import type { ContentFormat } from "../../types/api";

const urlTransform = (url: string) => (/^https?:\/\//i.test(url) ? url : "");

const blockComponents: Components = {
  a: ({ href, children }) => (
    <a href={href} target="_blank" rel="noopener noreferrer">
      {children}
    </a>
  ),
  table: ({ children }) => <table className="table table-sm table-bordered w-auto">{children}</table>,
  img: () => null,
};

/** Chế độ inline: chỉ giữ phần tử inline, đoạn văn render thành span (hợp lệ bên trong label). */
const inlineElements = ["p", "strong", "em", "del", "code", "a", "br"];
const inlineComponents: Components = {
  ...blockComponents,
  p: ({ children }) => <span className="md-inline-p">{children}</span>,
};

type Props = {
  content: string;
  format?: ContentFormat;
  /** Render bên trong phần tử inline (ví dụ label của lựa chọn): không sinh div / p / pre. */
  inline?: boolean;
};

/**
 * Component duy nhất render nội dung câu hỏi (D-17): Markdown giới hạn, KHÔNG có HTML thô
 * (không dùng rehype-raw / dangerouslySetInnerHTML). Liên kết chỉ http/https, mở tab mới.
 */
export function MarkdownView({ content, format = "MARKDOWN", inline = false }: Props) {
  const Wrapper = inline ? "span" : "div";
  const className = inline ? "md-content md-inline" : "md-content";

  if (format === "PLAIN") {
    return <Wrapper className={className} style={{ whiteSpace: "pre-wrap" }}>{content}</Wrapper>;
  }

  return (
    <Wrapper className={className}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        skipHtml
        urlTransform={urlTransform}
        components={inline ? inlineComponents : blockComponents}
        allowedElements={inline ? inlineElements : undefined}
        unwrapDisallowed={inline}
      >
        {content}
      </ReactMarkdown>
    </Wrapper>
  );
}
