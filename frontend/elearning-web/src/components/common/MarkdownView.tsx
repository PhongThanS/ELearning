import { useMemo } from "react";
import ReactMarkdown, { type Components } from "react-markdown";
import remarkGfm from "remark-gfm";
import { useTranslation } from "react-i18next";
import type { ContentFormat, MediaUrlMap } from "../../types/api";
import { MEDIA_SCHEME, resolveMediaUrl, useMediaUrls } from "./MediaUrls";

/** Chế độ inline: chỉ giữ phần tử inline, đoạn văn render thành span (hợp lệ bên trong label). */
const inlineElements = ["p", "strong", "em", "del", "code", "a", "br", "img"];

type Props = {
  content: string;
  format?: ContentFormat;
  /** Render bên trong phần tử inline (ví dụ label của lựa chọn): không sinh div / p / pre. */
  inline?: boolean;
  /** Bảng ảnh riêng; mặc định lấy từ MediaUrls bao ngoài. */
  media?: MediaUrlMap;
};

/**
 * Component duy nhất render nội dung câu hỏi (D-17): Markdown giới hạn, KHÔNG có HTML thô
 * (không dùng rehype-raw / dangerouslySetInnerHTML). Liên kết chỉ http/https, mở tab mới.
 * Ảnh chỉ nhận `media:<id>` có trong bảng URL đã ký của server (D-27); ảnh ngoài bị bỏ.
 */
export function MarkdownView({ content, format = "MARKDOWN", inline = false, media }: Props) {
  const { t } = useTranslation();
  const contextMedia = useMediaUrls();
  const urls = media ?? contextMedia;

  const components = useMemo<Components>(() => {
    const block: Components = {
      a: ({ href, children }) => (
        <a href={href} target="_blank" rel="noopener noreferrer">
          {children}
        </a>
      ),
      table: ({ children }) => <table className="table table-sm table-bordered w-auto">{children}</table>,
      img: ({ src, alt }) =>
        typeof src === "string" && src ? (
          <img src={src} alt={alt ?? ""} loading="lazy" className="md-image" />
        ) : (
          <span className="md-image-missing text-muted small">{t("media.unavailable")}</span>
        ),
    };
    return inline ? { ...block, p: ({ children }) => <span className="md-inline-p">{children}</span> } : block;
  }, [inline, t]);

  // Ảnh (src) chỉ nhận media:<id> đã ký, không nhận ảnh ngoài; liên kết (href) chỉ http/https.
  const urlTransform = useMemo(
    () => (url: string, key: string) => {
      if (key === "src") {
        return url.startsWith(MEDIA_SCHEME) ? resolveMediaUrl(url, urls) : "";
      }
      return /^https?:\/\//i.test(url) ? url : "";
    },
    [urls],
  );

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
        components={components}
        allowedElements={inline ? inlineElements : undefined}
        unwrapDisallowed={inline}
      >
        {content}
      </ReactMarkdown>
    </Wrapper>
  );
}
