import { driveThumbnailUrl, lessonKind, lessonThumbnail, parseDriveLink } from "./mediaLinks";

const ID = "16QH9phhlbP6krMALvnC8UUU-uJ-OLqYX";

describe("mediaLinks — nhận dạng link Google Drive", () => {
  it("link chia sẻ /file/d/<id>/view → trang preview nhúng được", () => {
    const drive = parseDriveLink(`https://drive.google.com/file/d/${ID}/view?usp=sharing`);
    expect(drive).toEqual({
      id: ID,
      previewUrl: `https://drive.google.com/file/d/${ID}/preview`,
      openUrl: `https://drive.google.com/file/d/${ID}/view`,
    });
  });

  it("dạng open?id= và uc?id=", () => {
    expect(parseDriveLink(`https://drive.google.com/open?id=${ID}`)?.id).toBe(ID);
    expect(parseDriveLink(`https://drive.google.com/uc?export=download&id=${ID}`)?.id).toBe(ID);
  });

  it("Google Docs / Sheets / Slides dùng trang preview của chính loại đó", () => {
    expect(parseDriveLink(`https://docs.google.com/document/d/${ID}/edit`)?.previewUrl).toBe(
      `https://docs.google.com/document/d/${ID}/preview`,
    );
    expect(parseDriveLink(`https://docs.google.com/presentation/d/${ID}/edit#slide=id.p`)?.previewUrl).toBe(
      `https://docs.google.com/presentation/d/${ID}/preview`,
    );
  });

  it("không nhận link khác", () => {
    expect(parseDriveLink("https://example.com/file/d/abc")).toBeNull();
    expect(parseDriveLink("")).toBeNull();
  });

  it("phân loại và ảnh bìa", () => {
    expect(lessonKind(`https://drive.google.com/file/d/${ID}/view`)).toBe("drive");
    expect(lessonKind("https://youtu.be/dQw4w9WgXcQ")).toBe("youtube");
    expect(lessonKind("https://cdn.example.com/bai1.mp4")).toBe("video");
    expect(lessonKind("https://example.com/trang")).toBe("link");
    expect(lessonThumbnail(`https://drive.google.com/file/d/${ID}/view`)).toBe(driveThumbnailUrl(ID));
    expect(lessonThumbnail("https://youtu.be/dQw4w9WgXcQ")).toBe("https://img.youtube.com/vi/dQw4w9WgXcQ/mqdefault.jpg");
  });
});
