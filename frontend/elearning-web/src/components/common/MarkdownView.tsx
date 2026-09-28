import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import type { ContentFormat } from "../../types/api";

/**
 * Component duy nhất render nội dung câu hỏi (D-17): Markdown giới hạn, KHÔNG có HTML thô
 * (không dùng rehype-raw / dangerouslySetInnerHTML). Liên kết chỉ http/https, mở tab mới.
 */
export function MarkdownView({ content, format = "MARKDOWN" }: { content: string; format?: ContentFormat }) {
  if (format === "PLAIN") {
    return <div className="md-content" style={{ whiteSpace: "pre-wrap" }}>{content}</div>;
  }

  return (
    <div className="md-content">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        skipHtml
        urlTransform={(url) => (/^https?:\/\//i.test(url) ? url : "")}
        components={{
          a: ({ href, children }) => (
            <a href={href} target="_blank" rel="noopener noreferrer">
              {children}
            </a>
          ),
          table: ({ children }) => <table className="table table-sm table-bordered w-auto">{children}</table>,
          img: () => null,
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
}
