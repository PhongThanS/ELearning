import type { AttemptStatus, ExamAvailability, ExamStatus } from "../types/api";

export const examStatusVariant: Record<ExamStatus, string> = { DRAFT: "secondary", PUBLISHED: "success", CLOSED: "dark" };

export const attemptStatusVariant: Record<AttemptStatus, string> = {
  IN_PROGRESS: "warning",
  SUBMITTED: "success",
  AUTO_SUBMITTED: "info",
  CANCELLED: "secondary",
};

export const availabilityVariant: Record<ExamAvailability, string> = {
  NOT_STARTED: "secondary",
  AVAILABLE: "success",
  IN_PROGRESS: "warning",
  NO_ATTEMPTS_LEFT: "dark",
  ENDED: "dark",
  CLOSED: "dark",
};
